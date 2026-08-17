using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using TraceLog;
using Fluxo.Core.Clients.Http;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Real-Debrid (https://real-debrid.com) client.
    ///
    /// Built on Fluxo's own <see cref="IHttpClient"/> for the same reason as
    /// <see cref="AllDebridService"/>: the user's proxy and Fluxo's TLS settings
    /// apply, which a bare System.Net.Http.HttpClient would bypass.
    ///
    /// The API differs from AllDebrid's in two ways that shape this class. It
    /// reports failures with real HTTP status codes rather than a 200 envelope,
    /// and a torrent is not resolved in one step: files have to be selected before
    /// Real-Debrid will fetch anything, so the poll loop runs in two phases.
    /// </summary>
    public class RealDebridService : IDebridService
    {
        private const string BaseUrl = "https://api.real-debrid.com/rest/1.0";

        // Real-Debrid allows 250 requests per minute. Polling every 2s is well
        // inside that, and torrents can take minutes to cache, so give up rather
        // than waiting forever.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(10);

        public string Name => "Real-Debrid";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Config.Instance.RealDebridApiKey);

        public bool SupportsTorrents => true;

        public DebridTorrent ResolveMagnet(string magnet, Action<string>? progress, CancelFlag cancelFlag)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(magnet))
            {
                throw new DebridException("Empty magnet link", "MAGNET_INVALID_URI");
            }

            progress?.Invoke("Submitting magnet...");
            var body = Encoding.UTF8.GetBytes("magnet=" + Uri.EscapeDataString(magnet.Trim()));
            var data = Send($"{BaseUrl}/torrents/addMagnet", "POST", body, FormContentType, cancelFlag);

            return WaitAndListFiles(TorrentId(data), progress, cancelFlag);
        }

        public DebridTorrent ResolveTorrentFile(byte[] torrentFile, string fileName, Action<string>? progress, CancelFlag cancelFlag)
        {
            EnsureConfigured();
            if (torrentFile == null || torrentFile.Length == 0)
            {
                throw new DebridException("Empty torrent file", "MAGNET_FILE_UPLOAD_FAILED");
            }

            // Unlike AllDebrid this is not a multipart form: the endpoint takes the
            // raw file as the request body, and only over PUT.
            progress?.Invoke("Uploading torrent...");
            var data = Send($"{BaseUrl}/torrents/addTorrent", "PUT", torrentFile,
                "application/x-bittorrent", cancelFlag);

            return WaitAndListFiles(TorrentId(data), progress, cancelFlag);
        }

        public DebridLink UnlockLink(string restrictedLink)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(restrictedLink))
            {
                throw new DebridException("Empty link", "LINK_IS_MISSING");
            }

            var body = Encoding.UTF8.GetBytes("link=" + Uri.EscapeDataString(restrictedLink.Trim()));
            var data = Send($"{BaseUrl}/unrestrict/link", "POST", body, FormContentType, CancelFlag.None);

            // "download" is the direct URL; "link" echoes back what was submitted.
            var url = (string?)data["download"];
            if (string.IsNullOrEmpty(url))
            {
                throw new DebridException("Real-Debrid returned no download link", "LINK_HOST_NOT_SUPPORTED");
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
                throw new DebridException("No Real-Debrid API key configured", "AUTH_MISSING_APIKEY");
            }
        }

        private static string TorrentId(JToken data)
        {
            var id = (string?)data["id"];
            if (string.IsNullOrEmpty(id))
            {
                throw new DebridException("Real-Debrid did not return a torrent id");
            }
            return id!;
        }

        private DebridTorrent WaitAndListFiles(string torrentId, Action<string>? progress, CancelFlag cancelFlag)
        {
            var info = WaitUntilReady(torrentId, progress, cancelFlag);

            var files = ReadFiles(info);
            if (files.Count == 0)
            {
                throw new DebridException("The torrent contains no downloadable files");
            }

            return new DebridTorrent
            {
                Name = (string?)info["filename"] ?? string.Empty,
                Files = files
            };
        }

        /// <summary>
        /// Polls /torrents/info until the torrent is cached, selecting every file
        /// the first time Real-Debrid asks. Returns the final info document.
        /// </summary>
        private JToken WaitUntilReady(string torrentId, Action<string>? progress, CancelFlag cancelFlag)
        {
            var deadline = DateTime.UtcNow + PollTimeout;
            var selected = false;

            while (true)
            {
                cancelFlag.ThrowIfCancellationRequested();

                var info = Send($"{BaseUrl}/torrents/info/{Uri.EscapeDataString(torrentId)}", "GET", null, null, cancelFlag);
                var status = (string?)info["status"] ?? string.Empty;

                switch (status)
                {
                    case "downloaded":
                        return info;

                    // Nothing is fetched until the files are chosen. Selecting them
                    // all matches how the torrent flow presents a torrent: as a whole,
                    // with the picker deciding what actually gets downloaded later.
                    //
                    // Only selected once. The state can lag a moment behind the call,
                    // so seeing this again is not a failure - the deadline below is
                    // what stops a torrent that never moves on.
                    case "waiting_files_selection":
                        if (!selected)
                        {
                            progress?.Invoke("Selecting files...");
                            SelectAllFiles(torrentId, cancelFlag);
                            selected = true;
                        }
                        else
                        {
                            progress?.Invoke(StatusName(status));
                        }
                        break;

                    case "magnet_error":
                    case "error":
                    case "virus":
                    case "dead":
                        throw new DebridException(
                            $"Real-Debrid reported '{StatusName(status)}' for this torrent",
                            "MAGNET_PROCESSING_FAILED");

                    default:
                        progress?.Invoke(DescribeProgress(info, status));
                        break;
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new DebridException(
                        "Timed out waiting for Real-Debrid to fetch this torrent. It may still be downloading - try again later.",
                        "MAGNET_TIMEOUT");
                }
                Thread.Sleep(PollInterval);
            }
        }

        private void SelectAllFiles(string torrentId, CancelFlag cancelFlag)
        {
            var body = Encoding.UTF8.GetBytes("files=all");
            Send($"{BaseUrl}/torrents/selectFiles/{Uri.EscapeDataString(torrentId)}", "POST",
                body, FormContentType, cancelFlag);
        }

        /// <summary>
        /// Pairs the selected files with the restricted links.
        ///
        /// Real-Debrid returns every file the torrent contains but only one link per
        /// *selected* file, matched by position rather than by id, so the unselected
        /// ones have to be filtered out first or every path lands on the wrong link.
        /// </summary>
        internal static IList<DebridFile> ReadFiles(JToken info)
        {
            var results = new List<DebridFile>();

            var links = info["links"] as JArray;
            var files = info["files"] as JArray;
            if (links == null || files == null)
            {
                return results;
            }

            var index = 0;
            foreach (var file in files)
            {
                if ((int?)file["selected"] != 1)
                {
                    continue;
                }
                if (index >= links.Count)
                {
                    // Fewer links than selected files means Real-Debrid could not
                    // serve the rest; the ones already paired are still good.
                    break;
                }

                var link = (string?)links[index];
                index++;
                if (string.IsNullOrEmpty(link))
                {
                    continue;
                }

                results.Add(new DebridFile
                {
                    // Paths arrive rooted, as "/Folder/file.mkv".
                    Path = ((string?)file["path"] ?? string.Empty).TrimStart('/'),
                    Size = (long?)file["bytes"] ?? 0,
                    RestrictedLink = link!
                });
            }

            return results;
        }

        private static string DescribeProgress(JToken info, string status)
        {
            var name = StatusName(status);
            // "progress" is a percentage, and only meaningful while downloading.
            var progress = (double?)info["progress"] ?? 0;
            if (status == "downloading" && progress > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} {1:F1}%", name, progress);
            }
            return name;
        }

        private static string StatusName(string status) => status switch
        {
            "magnet_conversion" => "Reading magnet",
            "waiting_files_selection" => "Waiting for file selection",
            "queued" => "In queue",
            "downloading" => "Downloading",
            "compressing" => "Compressing",
            "uploading" => "Uploading",
            "downloaded" => "Ready",
            "magnet_error" => "Invalid magnet",
            "error" => "Error",
            "virus" => "Flagged as malware",
            "dead" => "No seeders",
            _ => string.IsNullOrEmpty(status) ? "Working" : status
        };

        private JToken Send(string url, string method, byte[]? body, string? contentType, CancelFlag cancelFlag)
        {
            var headers = new Dictionary<string, List<string>>
            {
                // Never logged: this is a bearer credential.
                ["Authorization"] = new List<string> { "Bearer " + Config.Instance.RealDebridApiKey }
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
                var request = method switch
                {
                    "GET" => hc.CreateGetRequest(uri, headers),
                    "PUT" => hc.CreatePutRequest(uri, headers, null, null, body),
                    _ => hc.CreatePostRequest(uri, headers, null, null, body)
                };

                using var response = hc.Send(request);

                // The HTTP client swallows the failure status and disposes the body,
                // so the status code is all there is to go on. Real-Debrid's own
                // error text is unreachable here; the codes it uses are unambiguous
                // enough to explain on their own.
                var status = StatusCodeOf(response);
                if (status == null)
                {
                    throw new DebridException("Could not reach Real-Debrid");
                }
                if ((int)status >= 300)
                {
                    throw ErrorFor(status.Value);
                }

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
                Log.Debug(ex, "Real-Debrid request failed");
                throw new DebridException("Could not reach Real-Debrid: " + ex.Message);
            }

            return Parse(text);
        }

        private static HttpStatusCode? StatusCodeOf(HttpResponse response)
        {
            try
            {
                return response.StatusCode;
            }
            catch (Exception)
            {
                // No response at all - the request never completed.
                return null;
            }
        }

        internal static DebridException ErrorFor(HttpStatusCode status) => status switch
        {
            HttpStatusCode.Unauthorized =>
                new DebridException("Real-Debrid rejected the API key. Check it in Settings, Premium hosters.", "AUTH_BAD_APIKEY"),
            HttpStatusCode.Forbidden =>
                new DebridException("Real-Debrid refused the request. A premium subscription is required.", "AUTH_FORBIDDEN"),
            HttpStatusCode.NotFound =>
                new DebridException("Real-Debrid does not know this torrent or link", "RESOURCE_UNKNOWN"),
            HttpStatusCode.ServiceUnavailable =>
                new DebridException("Real-Debrid is temporarily unavailable, try again shortly", "SERVICE_UNAVAILABLE",
                    true, TimeSpan.FromMinutes(2)),
            (HttpStatusCode)429 =>
                new DebridException("Too many requests to Real-Debrid, slow down", "SLOW_DOWN",
                    true, TimeSpan.FromMinutes(2)),
            _ => new DebridException($"Real-Debrid request failed ({(int)status})")
        };

        /// <summary>
        /// Reads a response body. Some endpoints - selectFiles above all - answer
        /// 204 with nothing at all, which is a success rather than a parse failure.
        /// </summary>
        internal static JToken Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new JObject();
            }

            try
            {
                return JToken.Parse(text!);
            }
            catch (Exception)
            {
                throw new DebridException("Real-Debrid returned an unreadable response");
            }
        }
    }
}
