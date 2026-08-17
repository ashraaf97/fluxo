using Fluxo.Core;
using Fluxo.Core.Clients.Debrid;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// premium.to answers failures with HTTP 200 and a JSON body, so the error
    /// envelope is the thing worth pinning down: unrecognised, it would be saved to
    /// disk as though it were the file.
    /// </summary>
    [TestFixture]
    public class PremiumToServiceTests
    {
        // -------------------------------------------------------------- envelope

        [Test]
        public void ErrorFor_ExplainsBadCredentials()
        {
            var ex = PremiumToService.ErrorFor("{\"code\":401,\"message\":\"Invalid API authentication\"}");
            Assert.That(ex.Code, Is.EqualTo("AUTH_BAD_APIKEY"));
        }

        [Test]
        public void ErrorFor_ExplainsAnUnsupportedHost()
        {
            var ex = PremiumToService.ErrorFor("{\"code\":402,\"message\":\"Filehost is not supported\"}");
            Assert.That(ex.Code, Is.EqualTo("LINK_HOST_NOT_SUPPORTED"));
        }

        [Test]
        public void ErrorFor_ExplainsAnExhaustedTrafficBalance()
        {
            var ex = PremiumToService.ErrorFor("{\"code\":403,\"message\":\"Not enough traffic\"}");
            Assert.That(ex.Code, Is.EqualTo("TRAFFIC_EXHAUSTED"));
        }

        [Test]
        public void ErrorFor_FlagsTooManyConnectionsAsRetryable()
        {
            var ex = PremiumToService.ErrorFor("{\"code\":429,\"message\":\"Too many open connections\"}");

            Assert.That(ex.IsRateLimit, Is.True);
            Assert.That(ex.RetryAfter, Is.GreaterThan(System.TimeSpan.Zero));
        }

        [Test]
        public void ErrorFor_FlagsAnExhaustedHostAsRetryable()
        {
            // 500 is "no premium account available for this filehost", which is
            // usually temporary rather than a fault of the request.
            Assert.That(PremiumToService.ErrorFor("{\"code\":500,\"message\":\"x\"}").IsRateLimit, Is.True);
        }

        [Test]
        public void ErrorFor_KeepsTheServiceMessageForUnknownCodes()
        {
            var ex = PremiumToService.ErrorFor("{\"code\":418,\"message\":\"Something odd\"}");

            Assert.That(ex.Message, Is.EqualTo("Something odd"));
            Assert.That(ex.Code, Is.EqualTo("418"));
        }

        [Test]
        public void ErrorFor_ThrowsOnAnUnreadableBody()
        {
            Assert.Throws<DebridException>(() => PremiumToService.ErrorFor("<html>502 bad gateway</html>"));
        }

        // ------------------------------------------------------------- file names

        [Test]
        public void FileNameFrom_TakesTheLastPathSegmentOfTheHosterLink()
        {
            // Without this the file would land as "getfile.php".
            Assert.That(PremiumToService.FileNameFrom("https://example.com/files/movie.mkv"),
                Is.EqualTo("movie.mkv"));
        }

        [Test]
        public void FileNameFrom_IgnoresATrailingSlash()
        {
            Assert.That(PremiumToService.FileNameFrom("https://example.com/files/movie.mkv/"),
                Is.EqualTo("movie.mkv"));
        }

        [Test]
        public void FileNameFrom_UnescapesTheSegment()
        {
            Assert.That(PremiumToService.FileNameFrom("https://example.com/my%20file.zip"),
                Is.EqualTo("my file.zip"));
        }

        [Test]
        public void FileNameFrom_GivesUpOnSomethingThatIsNotAUrl()
        {
            Assert.That(PremiumToService.FileNameFrom("not a url"), Is.Null);
        }

        // ---------------------------------------------------------- capabilities

        [Test]
        public void Torrents_AreRefusedOutright()
        {
            var service = new PremiumToService();

            Assert.That(service.SupportsTorrents, Is.False);

            var magnet = Assert.Throws<DebridException>(() =>
                service.ResolveMagnet("magnet:?xt=urn:btih:abc", null, CancelFlag.None));
            Assert.That(magnet!.Code, Is.EqualTo("TORRENTS_UNSUPPORTED"));

            var file = Assert.Throws<DebridException>(() =>
                service.ResolveTorrentFile(new byte[] { 1 }, "x.torrent", null, CancelFlag.None));
            Assert.That(file!.Code, Is.EqualTo("TORRENTS_UNSUPPORTED"));
        }

        [Test]
        public void IsConfigured_NeedsBothTheUserIdAndTheKey()
        {
            var userId = Config.Instance.PremiumToUserId;
            var apiKey = Config.Instance.PremiumToApiKey;
            try
            {
                Config.Instance.PremiumToUserId = "12345";
                Config.Instance.PremiumToApiKey = string.Empty;
                Assert.That(new PremiumToService().IsConfigured, Is.False);

                Config.Instance.PremiumToUserId = string.Empty;
                Config.Instance.PremiumToApiKey = "a-key";
                Assert.That(new PremiumToService().IsConfigured, Is.False);

                Config.Instance.PremiumToUserId = "12345";
                Config.Instance.PremiumToApiKey = "a-key";
                Assert.That(new PremiumToService().IsConfigured, Is.True);
            }
            finally
            {
                Config.Instance.PremiumToUserId = userId;
                Config.Instance.PremiumToApiKey = apiKey;
            }
        }

        [Test]
        public void UnlockLink_FailsFastWhenUnconfigured()
        {
            var userId = Config.Instance.PremiumToUserId;
            var apiKey = Config.Instance.PremiumToApiKey;
            try
            {
                Config.Instance.PremiumToUserId = string.Empty;
                Config.Instance.PremiumToApiKey = string.Empty;

                var ex = Assert.Throws<DebridException>(() =>
                    new PremiumToService().UnlockLink("https://example.com/f/abc"));
                Assert.That(ex!.Code, Is.EqualTo("AUTH_MISSING_APIKEY"));
            }
            finally
            {
                Config.Instance.PremiumToUserId = userId;
                Config.Instance.PremiumToApiKey = apiKey;
            }
        }
    }
}
