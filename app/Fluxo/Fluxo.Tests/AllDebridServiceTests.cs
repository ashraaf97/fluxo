using System.Collections.Generic;
using System.Linq;
using Fluxo.Core.Clients.Debrid;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Covers the two things that break silently: AllDebrid's error envelope
    /// (which arrives with HTTP 200) and the nested file tree.
    /// </summary>
    [TestFixture]
    public class AllDebridServiceTests
    {
        // ------------------------------------------------------------ envelope

        [Test]
        public void Unwrap_ReturnsDataOnSuccess()
        {
            var data = AllDebridService.Unwrap("{\"status\":\"success\",\"data\":{\"magnets\":[{\"id\":42}]}}");
            Assert.That((long?)data["magnets"]!.First!["id"], Is.EqualTo(42));
        }

        [Test]
        public void Unwrap_ThrowsWithServiceCode_OnErrorEnvelope()
        {
            var ex = Assert.Throws<DebridException>(() => AllDebridService.Unwrap(
                "{\"status\":\"error\",\"error\":{\"code\":\"MAGNET_INVALID_URI\",\"message\":\"Magnet is not valid\"}}"));

            Assert.That(ex!.Code, Is.EqualTo("MAGNET_INVALID_URI"));
            Assert.That(ex.Message, Is.EqualTo("Magnet is not valid"));
            Assert.That(ex.IsRateLimit, Is.False);
        }

        [Test]
        public void Unwrap_FlagsRateLimitingAsRetryable()
        {
            var ex = Assert.Throws<DebridException>(() => AllDebridService.Unwrap(
                "{\"status\":\"error\",\"error\":{\"code\":\"SLOW_DOWN\",\"message\":\"You are going too fast, slow_down\"}}"));

            Assert.That(ex!.IsRateLimit, Is.True);
            Assert.That(ex.RetryAfter, Is.GreaterThan(System.TimeSpan.Zero));
        }

        [Test]
        public void Unwrap_ThrowsOnUnparseableBody()
        {
            Assert.Throws<DebridException>(() => AllDebridService.Unwrap("<html>gateway timeout</html>"));
        }

        [Test]
        public void Unwrap_ThrowsWhenDataMissing()
        {
            Assert.Throws<DebridException>(() => AllDebridService.Unwrap("{\"status\":\"success\"}"));
        }

        // ----------------------------------------------------------- file tree

        private static IList<DebridFile> Flatten(string json)
        {
            var results = new List<DebridFile>();
            AllDebridService.Flatten(JToken.Parse(json), string.Empty, results);
            return results;
        }

        [Test]
        public void Flatten_ReadsASingleFile()
        {
            var files = Flatten("[{\"n\":\"movie.mkv\",\"s\":1024,\"l\":\"https://alldebrid.com/f/abc\"}]");

            Assert.That(files.Count, Is.EqualTo(1));
            Assert.That(files[0].Path, Is.EqualTo("movie.mkv"));
            Assert.That(files[0].FileName, Is.EqualTo("movie.mkv"));
            Assert.That(files[0].Size, Is.EqualTo(1024));
            Assert.That(files[0].RestrictedLink, Is.EqualTo("https://alldebrid.com/f/abc"));
        }

        [Test]
        public void Flatten_WalksNestedFoldersAndBuildsPaths()
        {
            var files = Flatten(@"[
                {""n"":""Season 1"",""e"":[
                    {""n"":""ep1.mkv"",""s"":10,""l"":""https://alldebrid.com/f/1""},
                    {""n"":""Subs"",""e"":[
                        {""n"":""ep1.srt"",""s"":2,""l"":""https://alldebrid.com/f/2""}
                    ]}
                ]},
                {""n"":""readme.txt"",""s"":3,""l"":""https://alldebrid.com/f/3""}
            ]");

            Assert.That(files.Select(f => f.Path), Is.EqualTo(new[]
            {
                "Season 1/ep1.mkv",
                "Season 1/Subs/ep1.srt",
                "readme.txt"
            }));

            // FileName drops the folder prefix; the picker shows this.
            Assert.That(files[1].FileName, Is.EqualTo("ep1.srt"));
        }

        [Test]
        public void Flatten_SkipsEntriesWithoutADownloadLink()
        {
            // An empty folder, and a file AllDebrid could not serve.
            var files = Flatten(@"[
                {""n"":""Empty"",""e"":[]},
                {""n"":""broken.mkv"",""s"":10},
                {""n"":""good.mkv"",""s"":20,""l"":""https://alldebrid.com/f/ok""}
            ]");

            Assert.That(files.Count, Is.EqualTo(1));
            Assert.That(files[0].FileName, Is.EqualTo("good.mkv"));
        }

        [Test]
        public void Flatten_DefaultsMissingSizeToZero()
        {
            var files = Flatten("[{\"n\":\"a.bin\",\"l\":\"https://alldebrid.com/f/a\"}]");

            Assert.That(files.Count, Is.EqualTo(1));
            Assert.That(files[0].Size, Is.EqualTo(0));
        }

        // -------------------------------------------------------- configuration

        [Test]
        public void IsConfigured_IsFalseWithoutAnApiKey()
        {
            var original = Fluxo.Core.Config.Instance.AllDebridApiKey;
            try
            {
                Fluxo.Core.Config.Instance.AllDebridApiKey = string.Empty;
                Assert.That(new AllDebridService().IsConfigured, Is.False);

                Fluxo.Core.Config.Instance.AllDebridApiKey = "   ";
                Assert.That(new AllDebridService().IsConfigured, Is.False);

                Fluxo.Core.Config.Instance.AllDebridApiKey = "a-real-key";
                Assert.That(new AllDebridService().IsConfigured, Is.True);
            }
            finally
            {
                Fluxo.Core.Config.Instance.AllDebridApiKey = original;
            }
        }

        [Test]
        public void ResolveMagnet_FailsFastWhenUnconfigured()
        {
            var original = Fluxo.Core.Config.Instance.AllDebridApiKey;
            try
            {
                Fluxo.Core.Config.Instance.AllDebridApiKey = string.Empty;
                var ex = Assert.Throws<DebridException>(() =>
                    new AllDebridService().ResolveMagnet("magnet:?xt=urn:btih:abc", null, Fluxo.Core.CancelFlag.None));
                Assert.That(ex!.Code, Is.EqualTo("AUTH_MISSING_APIKEY"));
            }
            finally
            {
                Fluxo.Core.Config.Instance.AllDebridApiKey = original;
            }
        }
    }
}
