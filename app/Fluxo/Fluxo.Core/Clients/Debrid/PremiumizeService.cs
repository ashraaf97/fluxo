using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using TraceLog;
using Fluxo.Core.Clients.Http;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Premiumize (https://www.premiumize.me) client.
    ///
    /// Built on Fluxo's own <see cref="IHttpClient"/> for the same reason as the
    /// other services: the user's proxy and Fluxo's TLS settings apply.
    ///
    /// Magnets and hoster links both go to /transfer/directdl, which answers in one
    /// step with links that are already direct - there is nothing to poll and
    /// nothing to unlock afterwards. A .torrent file cannot use that endpoint, which
    /// takes a URL only, so it goes the long way round: /transfer/create uploads it,
    /// the transfer is polled until it finishes, and the resulting cloud folder is
    /// walked for its files.
    /// </summary>
    public class PremiumizeService : IDebridService
    {
        private const string BaseUrl = "https://www.premiumize.me/api";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Links this instance has already handed out. /transfer/directdl returns
        /// direct links, but the caller passes every file back through
        /// <see cref="UnlockLink"/> regardless, and re-submitting a direct link
        /// would fail. Remembering them makes that a no-op rather than a round trip.
        /// </summary>
        private readonly HashSet<string> directLinks = new(StringComparer.OrdinalIgnoreCase);

        public string Name => "Premiumize";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Config.Instance.PremiumizeApiKey);

        public bool SupportsTorrents => true;

        public DebridTorrent ResolveMagnet(string magnet, Action<string>? progress, CancelFlag cancelFlag)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(magnet))
            {
                throw new DebridException("Empty magnet link", "MAGNET_INVALID_URI");
            }

            progress?.Invoke("Submitting magnet...");
            var data = DirectDownload(magnet.Trim(), cancelFlag);

            var files = ReadDirectContent(data, this.directLinks);
            if (files.Count == 0)
            {
                throw new DebridException("The torrent contains no downloadable files");
            }

            return new DebridTorrent
            {
                Name = (string?)data["filename"] ?? NameFromMagnet(magnet) ?? string.Empty,
                Files = files
            };
        }

        public DebridTorrent ResolveTorrentFile(byte[] torrentFile, string fileName, Action<string>? progress, CancelFlag cancelFlag)
        {
            EnsureConfigured();
            if (torrentFile == null || torrentFile.Length == 0)
            {
                throw new DebridException("Empty torrent file", "MAGNET_FILE_UPLOAD_FAILED");
            }

            progress?.Invoke("Uploading torrent...");
            var boundary = MultipartFormData.NewBoundary();
            var body = MultipartFormData.Build(boundary, "src", fileName, torrentFile);
            var created = Send($"{BaseUrl}/transfer/create", "POST", body,
                MultipartFormData.ContentTypeFor(boundary), cancelFlag);

            var id = (string?)created["id"];
            if (string.IsNullOrEmpty(id))
            {
                throw new DebridException("Premiumize did not return a transfer id");
            }

            var transfer = WaitUntilReady(id!, progress, cancelFlag);

            progress?.Invoke("Fetching file list...");
            var files = CollectTransferFiles(transfer, cancelFlag);
            if (files.Count == 0)
            {
                throw new DebridException("The torrent contains no downloadable files");
            }

            return new DebridTorrent
            {
                Name = (string?)transfer["name"] ?? string.Empty,
                Files = files
            };
        }

        public DebridLink UnlockLink(string restrictedLink)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(restrictedLink))
            {
                throw new DebridException("Empty link", "LINK_IS_MISSING");
            }

            var link = restrictedLink.Trim();

            // Already downloadable - either this instance produced it, or it points
            // straight at Premiumize's own storage.
            if (this.directLinks.Contains(link) || IsPremiumizeLink(link))
            {
                return new DebridLink { Url = link };
            }

            var data = DirectDownload(link, CancelFlag.None);
            var files = ReadDirectContent(data, this.directLinks);
            if (files.Count == 0)
            {
                throw new DebridException("Premiumize returned no download link", "LINK_HOST_NOT_SUPPORTED");
            }

            var first = files[0];
            return new DebridLink
            {
                Url = first.RestrictedLink,
                FileName = first.FileName,
                Size = first.Size
            };
        }

        // ---------------------------------------------------------------- internals

        private const string FormContentType = "application/x-www-form-urlencoded";

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new DebridException("No Premiumize API key configured", "AUTH_MISSING_APIKEY");
            }
        }

        private JToken DirectDownload(string src, CancelFlag cancelFlag)
        {
            var body = Encoding.UTF8.GetBytes("src=" + Uri.EscapeDataString(src));
            return Send($"{BaseUrl}/transfer/directdl", "POST", body, FormContentType, cancelFlag);
        }

        /// <summary>
        /// Reads the content array /transfer/directdl answers with. Every entry is
        /// already a direct link, so each one is recorded as such.
        /// </summary>
        internal static IList<DebridFile> ReadDirectContent(JToken data, ISet<string>? directLinks = null)
        {
            var results = new List<DebridFile>();
            if (data["content"] is not JArray content)
            {
                return results;
            }

            foreach (var entry in content)
            {
                var link = (string?)entry["link"];
                if (string.IsNullOrEmpty(link))
                {
                    continue;
                }

                directLinks?.Add(link!);
                results.Add(new DebridFile
                {
                    Path = NormalizePath((string?)entry["path"]),
                    Size = (long?)entry["size"] ?? 0,
                    RestrictedLink = link!
                });
            }

            return results;
        }

        /// <summary>
        /// Polls /transfer/list until the transfer stops moving, returning its entry.
        /// </summary>
        private JToken WaitUntilReady(string transferId, Action<string>? progress, CancelFlag cancelFlag)
        {
            var deadline = DateTime.UtcNow + PollTimeout;
            while (true)
            {
                cancelFlag.ThrowIfCancellationRequested();

                var list = Send($"{BaseUrl}/transfer/list", "GET", null, null, cancelFlag);
                var transfer = FindTransfer(list, transferId);
                if (transfer == null)
                {
                    throw new DebridException("Premiumize lost track of this transfer", "MAGNET_PROCESSING_FAILED");
                }

                var status = (string?)transfer["status"] ?? string.Empty;

                // "seeding" means the files are ready and it is still giving back to
                // the swarm, so there is nothing left to wait for.
                if (status == "finished" || status == "seeding")
                {
                    return transfer;
                }
                if (status == "error")
                {
                    throw new DebridException(
                        (string?)transfer["message"] ?? "Premiumize could not fetch this torrent",
                        "MAGNET_PROCESSING_FAILED");
                }

                progress?.Invoke(DescribeProgress(transfer, status));

                if (DateTime.UtcNow > deadline)
                {
                    throw new DebridException(
                        "Timed out waiting for Premiumize to fetch this torrent. It may still be downloading - try again later.",
                        "MAGNET_TIMEOUT");
                }
                Thread.Sleep(PollInterval);
            }
        }

        internal static JToken? FindTransfer(JToken list, string transferId)
        {
            if (list["transfers"] is not JArray transfers)
            {
                return null;
            }

            foreach (var transfer in transfers)
            {
                if (string.Equals((string?)transfer["id"], transferId, StringComparison.Ordinal))
                {
                    return transfer;
                }
            }
            return null;
        }

        /// <summary>
        /// A finished transfer leaves either a folder or, for a single file torrent,
        /// one item. Both shapes end up as the same flat file list.
        /// </summary>
        private IList<DebridFile> CollectTransferFiles(JToken transfer, CancelFlag cancelFlag)
        {
            var folderId = (string?)transfer["folder_id"];
            if (!string.IsNullOrEmpty(folderId))
            {
                var results = new List<DebridFile>();
                WalkFolder(folderId!, string.Empty, results, cancelFlag, 0);
                return results;
            }

            var fileId = (string?)transfer["file_id"];
            if (!string.IsNullOrEmpty(fileId))
            {
                var item = Send($"{BaseUrl}/item/details?id={Uri.EscapeDataString(fileId!)}", "GET", null, null, cancelFlag);
                var link = (string?)item["link"];
                if (!string.IsNullOrEmpty(link))
                {
                    this.directLinks.Add(link!);
                    return new List<DebridFile>
                    {
                        new DebridFile
                        {
                            Path = NormalizePath((string?)item["name"]),
                            Size = (long?)item["size"] ?? 0,
                            RestrictedLink = link!
                        }
                    };
                }
            }

            return new List<DebridFile>();
        }

        /// <summary>
        /// Walks a cloud folder, flattening it into paths. The depth limit is a guard
        /// against a pathological or cyclic structure, not a real expectation.
        /// </summary>
        private void WalkFolder(string folderId, string prefix, IList<DebridFile> results, CancelFlag cancelFlag, int depth)
        {
            if (depth > 16)
            {
                return;
            }

            var data = Send($"{BaseUrl}/folder/list?id={Uri.EscapeDataString(folderId)}", "GET", null, null, cancelFlag);
            if (data["content"] is not JArray content)
            {
                return;
            }

            foreach (var entry in content)
            {
                cancelFlag.ThrowIfCancellationRequested();

                var name = (string?)entry["name"] ?? string.Empty;
                var path = string.IsNullOrEmpty(prefix) ? name : prefix + "/" + name;

                if (string.Equals((string?)entry["type"], "folder", StringComparison.OrdinalIgnoreCase))
                {
                    var childId = (string?)entry["id"];
                    if (!string.IsNullOrEmpty(childId))
                    {
                        WalkFolder(childId!, path, results, cancelFlag, depth + 1);
                    }
                    continue;
                }

                var link = (string?)entry["link"];
                if (string.IsNullOrEmpty(link))
                {
                    continue;
                }

                this.directLinks.Add(link!);
                results.Add(new DebridFile
                {
                    Path = NormalizePath(path),
                    Size = (long?)entry["size"] ?? 0,
                    RestrictedLink = link!
                });
            }
        }

        private static string DescribeProgress(JToken transfer, string status)
        {
            var name = StatusName(status);

            // Documented as a 0.0 - 1.0 fraction rather than a percentage.
            var progress = (double?)transfer["progress"] ?? 0;
            if (status == "running" && progress > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} {1:F1}%", name, progress * 100);
            }
            return name;
        }

        private static string StatusName(string status) => status switch
        {
            "waiting" => "Waiting",
            "queued" => "In queue",
            "running" => "Downloading",
            "finished" => "Ready",
            "seeding" => "Ready",
            "error" => "Error",
            _ => string.IsNullOrEmpty(status) ? "Working" : status
        };

        /// <summary>
        /// Paths arrive slash joined and occasionally rooted; the rest of the torrent
        /// code expects a relative path.
        /// </summary>
        internal static string NormalizePath(string? path)
            => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

        /// <summary>
        /// The display name carried by a magnet, used only to give a multi-file
        /// torrent a folder when its own paths do not supply one.
        /// </summary>
        internal static string? NameFromMagnet(string magnet)
        {
            const string marker = "dn=";
            var start = magnet.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            var end = magnet.IndexOf('&', start);
            var value = end < 0 ? magnet.Substring(start) : magnet.Substring(start, end - start);

            try
            {
                return Uri.UnescapeDataString(value.Replace('+', ' ')).Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static bool IsPremiumizeLink(string link)
            => Uri.TryCreate(link, UriKind.Absolute, out var uri)
               && (uri.Host.Equals("premiumize.me", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.EndsWith(".premiumize.me", StringComparison.OrdinalIgnoreCase));

        private JToken Send(string url, string method, byte[]? body, string? contentType, CancelFlag cancelFlag)
        {
            var headers = new Dictionary<string, List<string>>
            {
                // Never logged: this is a bearer credential.
                ["Authorization"] = new List<string> { "Bearer " + Config.Instance.PremiumizeApiKey }
            };
            if (contentType != null)
            {
                headers["Content-Type"] = new List<string> { contentType };
            }

            string? text;
            try
            {
                using var hc = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
                hc.Timeout = TimeSpan.FromSeconds(Math.Max(30, Config.Instance.NetworkTimeout));

                var uri = new Uri(url);
                var request = method == "GET"
                    ? hc.CreateGetRequest(uri, headers)
                    : hc.CreatePostRequest(uri, headers, null, null, body);

                using var response = hc.Send(request);
                text = response.ReadAsString(cancelFlag);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DebridException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Premiumize request failed");
                throw new DebridException("Could not reach Premiumize: " + ex.Message);
            }

            return Unwrap(text);
        }

        /// <summary>
        /// Premiumize answers HTTP 200 even for failures, so the envelope has to be
        /// inspected rather than the status code.
        /// </summary>
        internal static JToken Unwrap(string? text)
        {
            JObject root;
            try
            {
                root = JObject.Parse(text ?? string.Empty);
            }
            catch (Exception)
            {
                throw new DebridException("Premiumize returned an unreadable response");
            }

            var status = (string?)root["status"];
            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                throw ErrorFor((string?)root["code"], (string?)root["message"]);
            }

            return root;
        }

        internal static DebridException ErrorFor(string? code, string? message)
        {
            var text = string.IsNullOrWhiteSpace(message) ? "Premiumize rejected the request" : message!;

            return (code ?? string.Empty) switch
            {
                "authentication_failed" =>
                    new DebridException("Premiumize rejected the API key. Check it in Settings, Premium hosters.", "AUTH_BAD_APIKEY"),
                "permission_denied" =>
                    new DebridException("Premiumize refused the request. A premium subscription is required.", "AUTH_FORBIDDEN"),
                "not_found" =>
                    new DebridException("Premiumize does not know this transfer or link", "RESOURCE_UNKNOWN"),
                "service_unsupported" =>
                    new DebridException("Premiumize does not support this file host", "LINK_HOST_NOT_SUPPORTED"),
                "account_limit_reached" =>
                    new DebridException("The Premiumize fair use limit is used up", "TRAFFIC_EXHAUSTED"),
                "rate_limit_reached" =>
                    new DebridException("Too many requests to Premiumize, slow down", "SLOW_DOWN",
                        true, TimeSpan.FromMinutes(2)),
                "service_down" =>
                    new DebridException("The file host is unreachable from Premiumize, try again shortly",
                        "SERVICE_UNAVAILABLE", true, TimeSpan.FromMinutes(5)),
                "transient_error" =>
                    new DebridException(text, "TRANSIENT_ERROR", true, TimeSpan.FromMinutes(1)),
                _ => new DebridException(text, string.IsNullOrEmpty(code) ? null : code)
            };
        }
    }
}
