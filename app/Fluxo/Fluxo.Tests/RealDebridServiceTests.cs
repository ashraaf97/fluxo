using System.Net;
using Fluxo.Core;
using Fluxo.Core.Clients.Debrid;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Covers the parts of the Real-Debrid client that fail silently: the pairing
    /// of files to links (which is positional, so an off-by-one puts every file on
    /// the wrong URL) and the status-code error mapping.
    /// </summary>
    [TestFixture]
    public class RealDebridServiceTests
    {
        // --------------------------------------------------------- files to links

        [Test]
        public void ReadFiles_PairsSelectedFilesWithLinksInOrder()
        {
            var info = JToken.Parse(@"{
                ""files"": [
                    {""id"":1,""path"":""/Season 1/ep1.mkv"",""bytes"":10,""selected"":1},
                    {""id"":2,""path"":""/Season 1/ep2.mkv"",""bytes"":20,""selected"":1}
                ],
                ""links"": [""https://real-debrid.com/d/1"",""https://real-debrid.com/d/2""]
            }");

            var files = RealDebridService.ReadFiles(info);

            Assert.That(files.Count, Is.EqualTo(2));
            Assert.That(files[0].Path, Is.EqualTo("Season 1/ep1.mkv"));
            Assert.That(files[0].RestrictedLink, Is.EqualTo("https://real-debrid.com/d/1"));
            Assert.That(files[1].Path, Is.EqualTo("Season 1/ep2.mkv"));
            Assert.That(files[1].RestrictedLink, Is.EqualTo("https://real-debrid.com/d/2"));
            Assert.That(files[1].Size, Is.EqualTo(20));
        }

        [Test]
        public void ReadFiles_SkipsUnselectedFilesWithoutConsumingALink()
        {
            // The unselected file has no link of its own; taking one would shift
            // every following file onto the wrong URL.
            var info = JToken.Parse(@"{
                ""files"": [
                    {""id"":1,""path"":""/skipped.nfo"",""bytes"":1,""selected"":0},
                    {""id"":2,""path"":""/movie.mkv"",""bytes"":99,""selected"":1}
                ],
                ""links"": [""https://real-debrid.com/d/movie""]
            }");

            var files = RealDebridService.ReadFiles(info);

            Assert.That(files.Count, Is.EqualTo(1));
            Assert.That(files[0].FileName, Is.EqualTo("movie.mkv"));
            Assert.That(files[0].RestrictedLink, Is.EqualTo("https://real-debrid.com/d/movie"));
        }

        [Test]
        public void ReadFiles_StopsWhenLinksRunOut()
        {
            var info = JToken.Parse(@"{
                ""files"": [
                    {""id"":1,""path"":""/a.mkv"",""bytes"":1,""selected"":1},
                    {""id"":2,""path"":""/b.mkv"",""bytes"":2,""selected"":1}
                ],
                ""links"": [""https://real-debrid.com/d/a""]
            }");

            var files = RealDebridService.ReadFiles(info);

            Assert.That(files.Count, Is.EqualTo(1));
            Assert.That(files[0].FileName, Is.EqualTo("a.mkv"));
        }

        [Test]
        public void ReadFiles_ReturnsNothingWhenTheTorrentIsNotReady()
        {
            var info = JToken.Parse(@"{""status"":""downloading"",""files"":[]}");
            Assert.That(RealDebridService.ReadFiles(info).Count, Is.EqualTo(0));
        }

        [Test]
        public void ReadFiles_StripsTheLeadingSlashFromPaths()
        {
            var info = JToken.Parse(@"{
                ""files"": [{""id"":1,""path"":""/movie.mkv"",""bytes"":5,""selected"":1}],
                ""links"": [""https://real-debrid.com/d/x""]
            }");

            Assert.That(RealDebridService.ReadFiles(info)[0].Path, Is.EqualTo("movie.mkv"));
        }

        // ------------------------------------------------------------- responses

        [Test]
        public void Parse_TreatsAnEmptyBodyAsSuccess()
        {
            // selectFiles answers 204 with no content.
            Assert.That(RealDebridService.Parse(string.Empty).Type, Is.EqualTo(JTokenType.Object));
            Assert.That(RealDebridService.Parse(null).Type, Is.EqualTo(JTokenType.Object));
        }

        [Test]
        public void Parse_ThrowsOnUnparseableBody()
        {
            Assert.Throws<DebridException>(() => RealDebridService.Parse("<html>gateway timeout</html>"));
        }

        [Test]
        public void ErrorFor_ExplainsABadApiKey()
        {
            var ex = RealDebridService.ErrorFor(HttpStatusCode.Unauthorized);
            Assert.That(ex.Code, Is.EqualTo("AUTH_BAD_APIKEY"));
        }

        [Test]
        public void ErrorFor_FlagsRateLimitingAsRetryable()
        {
            var ex = RealDebridService.ErrorFor((HttpStatusCode)429);

            Assert.That(ex.IsRateLimit, Is.True);
            Assert.That(ex.RetryAfter, Is.GreaterThan(System.TimeSpan.Zero));
        }

        [Test]
        public void ErrorFor_FlagsAnOutageAsRetryable()
        {
            Assert.That(RealDebridService.ErrorFor(HttpStatusCode.ServiceUnavailable).IsRateLimit, Is.True);
        }

        // ---------------------------------------------------------- configuration

        [Test]
        public void IsConfigured_IsFalseWithoutAnApiKey()
        {
            var original = Config.Instance.RealDebridApiKey;
            try
            {
                Config.Instance.RealDebridApiKey = string.Empty;
                Assert.That(new RealDebridService().IsConfigured, Is.False);

                Config.Instance.RealDebridApiKey = "   ";
                Assert.That(new RealDebridService().IsConfigured, Is.False);

                Config.Instance.RealDebridApiKey = "a-real-key";
                Assert.That(new RealDebridService().IsConfigured, Is.True);
            }
            finally
            {
                Config.Instance.RealDebridApiKey = original;
            }
        }

        [Test]
        public void ResolveMagnet_FailsFastWhenUnconfigured()
        {
            var original = Config.Instance.RealDebridApiKey;
            try
            {
                Config.Instance.RealDebridApiKey = string.Empty;
                var ex = Assert.Throws<DebridException>(() =>
                    new RealDebridService().ResolveMagnet("magnet:?xt=urn:btih:abc", null, CancelFlag.None));
                Assert.That(ex!.Code, Is.EqualTo("AUTH_MISSING_APIKEY"));
            }
            finally
            {
                Config.Instance.RealDebridApiKey = original;
            }
        }
    }
}
