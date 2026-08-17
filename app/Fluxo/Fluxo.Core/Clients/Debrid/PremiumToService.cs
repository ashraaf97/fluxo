using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TraceLog;
using Fluxo.Core.Clients.Http;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// premium.to (https://premium.to) client.
    ///
    /// A link unlocker only - the API has no torrent or magnet support at all, so
    /// <see cref="SupportsTorrents"/> is false and torrents are routed elsewhere.
    ///
    /// It also works differently from the other services. There is no endpoint that
    /// hands back a direct URL as JSON: getfile.php *is* the download, streaming the
    /// file (or redirecting to a server that does). So the unlocked URL Fluxo
    /// downloads from is a getfile.php call, credentials and all.
    ///
    /// Failures arrive as JSON with HTTP 200, which would otherwise be saved to disk
    /// as if it were the file. <see cref="UnlockLink"/> therefore probes the URL
    /// first and only lets it through once the response looks like a file.
    /// </summary>
    public class PremiumToService : IDebridService
    {
        private const string BaseUrl = "https://api.premium.to/api/2";

        public string Name => "premium.to";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Config.Instance.PremiumToUserId)
            && !string.IsNullOrWhiteSpace(Config.Instance.PremiumToApiKey);

        public bool SupportsTorrents => false;

        public DebridTorrent ResolveMagnet(string magnet, Action<string>? progress, CancelFlag cancelFlag)
            => throw Unsupported();

        public DebridTorrent ResolveTorrentFile(byte[] torrentFile, string fileName, Action<string>? progress, CancelFlag cancelFlag)
            => throw Unsupported();

        public DebridLink UnlockLink(string restrictedLink)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(restrictedLink))
            {
                throw new DebridException("Empty link", "LINK_IS_MISSING");
            }

            var url = DownloadUrl(restrictedLink.Trim());

            // The probe is a real GET, but the body is never read: the response is
            // disposed as soon as the headers have been inspected, which aborts the
            // transfer. What it buys is the error check plus the name and size,
            // neither of which this API exposes any other way.
            try
            {
                using var hc = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
                hc.Timeout = TimeSpan.FromSeconds(Math.Max(30, Config.Instance.NetworkTimeout));

                using var response = hc.Send(hc.CreateGetRequest(new Uri(url)));

                if (LooksLikeError(response))
                {
                    throw ErrorFor(response.ReadAsString(CancelFlag.None));
                }

                return new DebridLink
                {
                    Url = url,
                    FileName = response.ContentDispositionFileName ?? FileNameFrom(restrictedLink),
                    Size = response.ContentLength > 0 ? response.ContentLength : 0
                };
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
                Log.Debug(ex, "premium.to request failed");
                throw new DebridException("Could not reach premium.to: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- internals

        private static DebridException Unsupported()
            => new DebridException("premium.to does not support torrents or magnet links", "TORRENTS_UNSUPPORTED");

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new DebridException("No premium.to user ID and API key configured", "AUTH_MISSING_APIKEY");
            }
        }

        /// <summary>
        /// The URL the file is actually fetched from. Note that it carries the
        /// credentials: this API offers no way to obtain a link without them.
        /// </summary>
        private static string DownloadUrl(string restrictedLink)
            => $"{BaseUrl}/getfile.php" +
               $"?userid={Uri.EscapeDataString(Config.Instance.PremiumToUserId)}" +
               $"&apikey={Uri.EscapeDataString(Config.Instance.PremiumToApiKey)}" +
               $"&link={Uri.EscapeDataString(restrictedLink)}";

        /// <summary>
        /// A JSON content type means the envelope, not the file. Every failure comes
        /// back that way, with HTTP 200.
        /// </summary>
        private static bool LooksLikeError(HttpResponse response)
        {
            var contentType = response.ContentType;
            return contentType != null
                && contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Turns the error envelope into something worth showing. The service's own
        /// message is kept, since it is specific and already in English.
        /// </summary>
        internal static DebridException ErrorFor(string? body)
        {
            int code;
            string message;
            try
            {
                var root = JObject.Parse(body ?? string.Empty);
                code = (int?)root["code"] ?? 0;
                message = (string?)root["message"] ?? "premium.to rejected this link";
            }
            catch (Exception)
            {
                throw new DebridException("premium.to returned an unreadable response");
            }

            return code switch
            {
                401 => new DebridException("premium.to rejected the user ID or API key", "AUTH_BAD_APIKEY"),
                402 => new DebridException("premium.to does not support this file host", "LINK_HOST_NOT_SUPPORTED"),
                403 => new DebridException("premium.to traffic balance is used up", "TRAFFIC_EXHAUSTED"),
                404 => new DebridException("The file is gone from the host", "LINK_NOT_FOUND"),
                429 => new DebridException("Too many open premium.to connections, try again shortly",
                    "SLOW_DOWN", true, TimeSpan.FromMinutes(2)),
                500 => new DebridException("premium.to has no premium account available for this file host",
                    "LINK_HOST_UNAVAILABLE", true, TimeSpan.FromMinutes(5)),
                _ => new DebridException(message, code == 0 ? null : code.ToString())
            };
        }

        /// <summary>
        /// Fallback name, taken from the hoster link. Without it the file would be
        /// saved as "getfile.php", since that is what the download URL ends in.
        /// </summary>
        internal static string? FileNameFrom(string restrictedLink)
        {
            if (!Uri.TryCreate(restrictedLink, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var segments = uri.AbsolutePath.Split('/');
            for (var i = segments.Length - 1; i >= 0; i--)
            {
                var segment = Uri.UnescapeDataString(segments[i]).Trim();
                if (segment.Length > 0)
                {
                    return segment;
                }
            }

            return null;
        }
    }
}
