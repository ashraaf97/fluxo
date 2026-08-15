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
    /// AllDebrid (https://alldebrid.com) client.
    ///
    /// Deliberately built on Fluxo's own <see cref="IHttpClient"/> rather than the
    /// AllDebrid.NET package, so the user's proxy settings and Fluxo's TLS
    /// configuration apply. Both are bypassed by anything using a bare
    /// System.Net.Http.HttpClient.
    /// </summary>
    public class AllDebridService : IDebridService
    {
        private const string BaseUrl = "https://api.alldebrid.com/v4";
        private const string BaseUrlV41 = "https://api.alldebrid.com/v4.1";
        private const string Agent = "Fluxo";

        // AllDebrid allows 12 req/s. Torrents can take minutes to cache, so poll
        // gently and give up rather than hammering the API forever.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(10);

        public string Name => "AllDebrid";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Config.Instance.AllDebridApiKey);

        public DebridTorrent ResolveMagnet(string magnet, Action<string>? progress, CancelFlag cancelFlag)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(magnet))
            {
                throw new DebridException("Empty magnet link", "MAGNET_INVALID_URI");
            }

            progress?.Invoke("Submitting magnet...");
            var body = Encoding.UTF8.GetBytes("magnets[]=" + Uri.EscapeDataString(magnet.Trim()));
            var data = Post($"{BaseUrl}/magnet/upload", body, FormContentType, cancelFlag);

            var id = FirstMagnetId(data);
            return WaitAndListFiles(id, progress, cancelFlag);
        }

        public DebridTorrent ResolveTorrentFile(byte[] torrentFile, string fileName, Action<string>? progress, CancelFlag cancelFlag)
        {
            EnsureConfigured();
            if (torrentFile == null || torrentFile.Length == 0)
            {
                throw new DebridException("Empty torrent file", "MAGNET_FILE_UPLOAD_FAILED");
            }

            progress?.Invoke("Uploading torrent...");
            var boundary = "----FluxoBoundary" + Guid.NewGuid().ToString("N");
            var body = BuildMultipartBody(boundary, "files[]", fileName, torrentFile);
            var data = Post($"{BaseUrl}/magnet/upload/file", body,
                "multipart/form-data; boundary=" + boundary, cancelFlag);

            var id = FirstMagnetId(data);
            return WaitAndListFiles(id, progress, cancelFlag);
        }

        public DebridLink UnlockLink(string restrictedLink)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(restrictedLink))
            {
                throw new DebridException("Empty link", "LINK_IS_MISSING");
            }

            var body = Encoding.UTF8.GetBytes("link=" + Uri.EscapeDataString(restrictedLink.Trim()));
            var data = Post($"{BaseUrl}/link/unlock", body, FormContentType, CancelFlag.None);

            var url = (string?)data["link"];
            if (string.IsNullOrEmpty(url))
            {
                // A "delayed" id means AllDebrid is still preparing the file. Rare
                // for torrent links; surface it rather than silently returning null.
                if (data["delayed"] != null && data["delayed"]!.Type != JTokenType.Null)
                {
                    throw new DebridException("AllDebrid is still preparing this link, try again shortly", "LINK_DELAYED");
                }
                throw new DebridException("AllDebrid returned no download link", "LINK_HOST_NOT_SUPPORTED");
            }

            return new DebridLink
            {
                Url = url!,
                FileName = (string?)data["filename"],
                Size = (long?)data["filesize"] ?? 0
            };
        }

        // ---------------------------------------------------------------- internals

        private const string FormContentType = "application/x-www-form-urlencoded";

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new DebridException("No AllDebrid API key configured", "AUTH_MISSING_APIKEY");
            }
        }

        private static long FirstMagnetId(JToken data)
        {
            // /magnet/upload answers under "magnets", /magnet/upload/file under "files".
            var list = data["magnets"] ?? data["files"];
            var first = list?.Type == JTokenType.Array ? list!.First : list;
            if (first == null)
            {
                throw new DebridException("AllDebrid did not return a magnet id");
            }

            // A per-item error can appear inside an otherwise successful envelope.
            var itemError = first["error"];
            if (itemError != null && itemError.Type != JTokenType.Null)
            {
                throw new DebridException(
                    (string?)itemError["message"] ?? "AllDebrid rejected this torrent",
                    (string?)itemError["code"]);
            }

            var id = (long?)first["id"];
            if (id == null)
            {
                throw new DebridException("AllDebrid did not return a magnet id");
            }
            return id.Value;
        }

        private DebridTorrent WaitAndListFiles(long magnetId, Action<string>? progress, CancelFlag cancelFlag)
        {
            var torrentName = WaitUntilReady(magnetId, progress, cancelFlag);

            progress?.Invoke("Fetching file list...");
            var data = Get($"{BaseUrl}/magnet/files?id[]={magnetId}", cancelFlag);

            var magnets = data["magnets"];
            var first = magnets?.Type == JTokenType.Array ? magnets!.First : magnets;
            var files = first?["files"];

            var results = new List<DebridFile>();
            if (files != null)
            {
                Flatten(files, string.Empty, results);
            }

            if (results.Count == 0)
            {
                throw new DebridException("The torrent contains no downloadable files");
            }

            return new DebridTorrent
            {
                Name = torrentName ?? string.Empty,
                Files = results
            };
        }

        /// <summary>
        /// Polls until the torrent is cached, returning the service's name for it.
        /// </summary>
        private string? WaitUntilReady(long magnetId, Action<string>? progress, CancelFlag cancelFlag)
        {
            var deadline = DateTime.UtcNow + PollTimeout;
            while (true)
            {
                cancelFlag.ThrowIfCancellationRequested();

                var data = Get($"{BaseUrlV41}/magnet/status?id={magnetId}", cancelFlag);
                var magnets = data["magnets"];
                var m = magnets?.Type == JTokenType.Array ? magnets!.First : magnets;
                if (m == null)
                {
                    throw new DebridException("AllDebrid returned no status for this torrent");
                }

                var statusCode = (int?)m["statusCode"] ?? -1;
                if (statusCode == 4)
                {
                    return (string?)m["filename"];
                }
                if (statusCode < 0 || statusCode > 4)
                {
                    throw new DebridException(
                        (string?)m["status"] ?? $"AllDebrid reported status {statusCode}",
                        "MAGNET_PROCESSING_FAILED");
                }

                progress?.Invoke(DescribeProgress(m, statusCode));

                if (DateTime.UtcNow > deadline)
                {
                    throw new DebridException(
                        "Timed out waiting for AllDebrid to fetch this torrent. It may still be downloading - try again later.",
                        "MAGNET_TIMEOUT");
                }
                Thread.Sleep(PollInterval);
            }
        }

        private static string DescribeProgress(JToken m, int statusCode)
        {
            var status = (string?)m["status"] ?? StatusName(statusCode);
            var size = (long?)m["size"] ?? 0;
            var downloaded = (long?)m["downloaded"] ?? 0;
            if (statusCode == 1 && size > 0)
            {
                var pct = downloaded * 100.0 / size;
                return string.Format(CultureInfo.InvariantCulture, "{0} {1:F1}%", status, pct);
            }
            return status;
        }

        private static string StatusName(int statusCode) => statusCode switch
        {
            0 => "In queue",
            1 => "Downloading",
            2 => "Compressing",
            3 => "Uploading",
            4 => "Ready",
            _ => "Unknown"
        };

        /// <summary>
        /// AllDebrid returns a nested tree with terse keys: n = name, s = size,
        /// l = restricted link, e = sub-entries. A node without a link is a folder.
        /// </summary>
        internal static void Flatten(JToken node, string prefix, IList<DebridFile> results)
        {
            if (node.Type == JTokenType.Array)
            {
                foreach (var child in node)
                {
                    Flatten(child, prefix, results);
                }
                return;
            }

            var name = (string?)node["n"] ?? string.Empty;
            var path = string.IsNullOrEmpty(prefix) ? name : prefix + "/" + name;

            var sub = node["e"];
            if (sub != null && sub.Type != JTokenType.Null)
            {
                Flatten(sub, path, results);
                return;
            }

            var link = (string?)node["l"];
            if (string.IsNullOrEmpty(link))
            {
                // Folder with no children, or an entry AllDebrid could not serve.
                return;
            }

            results.Add(new DebridFile
            {
                Path = path,
                Size = (long?)node["s"] ?? 0,
                RestrictedLink = link!
            });
        }

        private static byte[] BuildMultipartBody(string boundary, string fieldName, string fileName, byte[] content)
        {
            var header = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"{fieldName}\"; filename=\"{SanitizeFileName(fileName)}\"\r\n" +
                "Content-Type: application/x-bittorrent\r\n\r\n");
            var footer = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

            var body = new byte[header.Length + content.Length + footer.Length];
            Buffer.BlockCopy(header, 0, body, 0, header.Length);
            Buffer.BlockCopy(content, 0, body, header.Length, content.Length);
            Buffer.BlockCopy(footer, 0, body, header.Length + content.Length, footer.Length);
            return body;
        }

        private static string SanitizeFileName(string name)
            => string.IsNullOrEmpty(name) ? "upload.torrent" : name.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

        private JToken Get(string url, CancelFlag cancelFlag) => Send(url, null, null, cancelFlag);

        private JToken Post(string url, byte[] body, string contentType, CancelFlag cancelFlag)
            => Send(url, body, contentType, cancelFlag);

        private JToken Send(string url, byte[]? body, string? contentType, CancelFlag cancelFlag)
        {
            var separator = url.Contains("?") ? "&" : "?";
            var fullUrl = $"{url}{separator}agent={Agent}";

            var headers = new Dictionary<string, List<string>>
            {
                // Never logged: this is a bearer credential.
                ["Authorization"] = new List<string> { "Bearer " + Config.Instance.AllDebridApiKey }
            };
            if (contentType != null)
            {
                headers["Content-Type"] = new List<string> { contentType };
            }

            string text;
            try
            {
                using var hc = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
                hc.Timeout = TimeSpan.FromSeconds(Math.Max(30, Config.Instance.NetworkTimeout));

                var request = body == null
                    ? hc.CreateGetRequest(new Uri(fullUrl), headers)
                    : hc.CreatePostRequest(new Uri(fullUrl), headers, null, null, body);

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
                Log.Debug(ex, "AllDebrid request failed");
                throw new DebridException("Could not reach AllDebrid: " + ex.Message);
            }

            return Unwrap(text);
        }

        /// <summary>
        /// AllDebrid answers HTTP 200 even for failures, so the envelope has to be
        /// inspected rather than the status code.
        /// </summary>
        internal static JToken Unwrap(string text)
        {
            JObject root;
            try
            {
                root = JObject.Parse(text);
            }
            catch (Exception)
            {
                throw new DebridException("AllDebrid returned an unreadable response");
            }

            var status = (string?)root["status"];
            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                var error = root["error"];
                var code = (string?)error?["code"];
                var message = (string?)error?["message"] ?? "AllDebrid request failed";

                var rateLimited = string.Equals(code, "SLOW_DOWN", StringComparison.OrdinalIgnoreCase)
                    || message.IndexOf("slow_down", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0;

                throw new DebridException(message, code, rateLimited,
                    rateLimited ? TimeSpan.FromMinutes(2) : (TimeSpan?)null);
            }

            var data = root["data"];
            if (data == null)
            {
                throw new DebridException("AllDebrid returned no data");
            }
            return data;
        }
    }
}
