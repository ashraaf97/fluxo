using System.Linq;
using Fluxo.Core;
using Fluxo.Core.Clients.Debrid;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Premiumize reports failures inside an HTTP 200 envelope, and hands back links
    /// that are already direct - which is the part that does not fit the usual
    /// resolve-then-unlock shape, so it gets the most attention here.
    /// </summary>
    [TestFixture]
    public class PremiumizeServiceTests
    {
        // -------------------------------------------------------------- envelope

        [Test]
        public void Unwrap_ReturnsTheDocumentOnSuccess()
        {
            var data = PremiumizeService.Unwrap("{\"status\":\"success\",\"id\":\"abc\"}");
            Assert.That((string?)data["id"], Is.EqualTo("abc"));
        }

        [Test]
        public void Unwrap_ThrowsOnAnErrorEnvelope()
        {
            var ex = Assert.Throws<DebridException>(() => PremiumizeService.Unwrap(
                "{\"status\":\"error\",\"code\":\"authentication_failed\",\"message\":\"Not logged in\"}"));

            Assert.That(ex!.Code, Is.EqualTo("AUTH_BAD_APIKEY"));
        }

        [Test]
        public void Unwrap_ThrowsOnAnUnreadableBody()
        {
            Assert.Throws<DebridException>(() => PremiumizeService.Unwrap("<html>502</html>"));
        }

        [Test]
        public void ErrorFor_FlagsTheRetryableCodes()
        {
            Assert.That(PremiumizeService.ErrorFor("rate_limit_reached", null).IsRateLimit, Is.True);
            Assert.That(PremiumizeService.ErrorFor("service_down", null).IsRateLimit, Is.True);
            Assert.That(PremiumizeService.ErrorFor("transient_error", "hiccup").IsRateLimit, Is.True);

            Assert.That(PremiumizeService.ErrorFor("service_unsupported", null).IsRateLimit, Is.False);
        }

        [Test]
        public void ErrorFor_KeepsTheServiceMessageForUnknownCodes()
        {
            var ex = PremiumizeService.ErrorFor("something_new", "Something new happened");

            Assert.That(ex.Message, Is.EqualTo("Something new happened"));
            Assert.That(ex.Code, Is.EqualTo("something_new"));
        }

        // ------------------------------------------------------- direct download

        [Test]
        public void ReadDirectContent_ReadsPathSizeAndLink()
        {
            var data = JToken.Parse(@"{
                ""status"": ""success"",
                ""content"": [
                    {""path"":""Season 1/ep1.mkv"",""size"":10,""link"":""https://x.premiumize.me/dl/1""},
                    {""path"":""Season 1/ep2.mkv"",""size"":20,""link"":""https://x.premiumize.me/dl/2""}
                ]
            }");

            var files = PremiumizeService.ReadDirectContent(data);

            Assert.That(files.Select(f => f.Path), Is.EqualTo(new[] { "Season 1/ep1.mkv", "Season 1/ep2.mkv" }));
            Assert.That(files[1].Size, Is.EqualTo(20));
            Assert.That(files[1].RestrictedLink, Is.EqualTo("https://x.premiumize.me/dl/2"));
        }

        [Test]
        public void ReadDirectContent_SkipsEntriesWithoutALink()
        {
            var data = JToken.Parse(@"{
                ""content"": [
                    {""path"":""broken.mkv"",""size"":1},
                    {""path"":""good.mkv"",""size"":2,""link"":""https://x.premiumize.me/dl/ok""}
                ]
            }");

            var files = PremiumizeService.ReadDirectContent(data);

            Assert.That(files.Count, Is.EqualTo(1));
            Assert.That(files[0].FileName, Is.EqualTo("good.mkv"));
        }

        [Test]
        public void ReadDirectContent_RecordsEveryLinkAsAlreadyDirect()
        {
            // This is what stops the caller unlocking a link that needs no unlocking.
            var data = JToken.Parse(@"{""content"":[{""path"":""a.mkv"",""link"":""https://x.premiumize.me/dl/a""}]}");
            var seen = new System.Collections.Generic.HashSet<string>();

            PremiumizeService.ReadDirectContent(data, seen);

            Assert.That(seen, Does.Contain("https://x.premiumize.me/dl/a"));
        }

        [Test]
        public void ReadDirectContent_ToleratesAMissingContentArray()
        {
            Assert.That(PremiumizeService.ReadDirectContent(JToken.Parse("{\"status\":\"success\"}")).Count,
                Is.EqualTo(0));
        }

        [Test]
        public void NormalizePath_StripsARootAndNormalisesSeparators()
        {
            Assert.That(PremiumizeService.NormalizePath("/Folder/file.mkv"), Is.EqualTo("Folder/file.mkv"));
            Assert.That(PremiumizeService.NormalizePath(@"Folder\file.mkv"), Is.EqualTo("Folder/file.mkv"));
            Assert.That(PremiumizeService.NormalizePath(null), Is.EqualTo(string.Empty));
        }

        // ------------------------------------------------------------- transfers

        [Test]
        public void FindTransfer_PicksTheRequestedOne()
        {
            var list = JToken.Parse(@"{
                ""transfers"": [
                    {""id"":""aaa"",""status"":""running""},
                    {""id"":""bbb"",""status"":""finished""}
                ]
            }");

            Assert.That((string?)PremiumizeService.FindTransfer(list, "bbb")!["status"], Is.EqualTo("finished"));
            Assert.That(PremiumizeService.FindTransfer(list, "zzz"), Is.Null);
        }

        [Test]
        public void FindTransfer_ToleratesAMissingList()
        {
            Assert.That(PremiumizeService.FindTransfer(JToken.Parse("{\"status\":\"success\"}"), "aaa"), Is.Null);
        }

        // ------------------------------------------------------------- magnet name

        [Test]
        public void NameFromMagnet_ReadsTheDisplayName()
        {
            Assert.That(
                PremiumizeService.NameFromMagnet("magnet:?xt=urn:btih:abc&dn=Some.Release.2024&tr=udp://x"),
                Is.EqualTo("Some.Release.2024"));
        }

        [Test]
        public void NameFromMagnet_DecodesEscapesAndPluses()
        {
            Assert.That(
                PremiumizeService.NameFromMagnet("magnet:?xt=urn:btih:abc&dn=Some+Release%202024"),
                Is.EqualTo("Some Release 2024"));
        }

        [Test]
        public void NameFromMagnet_ReturnsNullWhenAbsent()
        {
            Assert.That(PremiumizeService.NameFromMagnet("magnet:?xt=urn:btih:abc"), Is.Null);
        }

        // ------------------------------------------------------ link passthrough

        [Test]
        public void IsPremiumizeLink_RecognisesTheirOwnHosts()
        {
            Assert.That(PremiumizeService.IsPremiumizeLink("https://premiumize.me/dl/x"), Is.True);
            Assert.That(PremiumizeService.IsPremiumizeLink("https://node1.premiumize.me/dl/x"), Is.True);

            Assert.That(PremiumizeService.IsPremiumizeLink("https://example.com/f/x"), Is.False);
            Assert.That(PremiumizeService.IsPremiumizeLink("https://notpremiumize.me.evil.com/x"), Is.False);
            Assert.That(PremiumizeService.IsPremiumizeLink("not a url"), Is.False);
        }

        [Test]
        public void UnlockLink_PassesADirectLinkStraightBack()
        {
            var apiKey = Config.Instance.PremiumizeApiKey;
            try
            {
                Config.Instance.PremiumizeApiKey = "a-key";

                // No network call: a link already on their CDN needs no unlocking.
                var link = new PremiumizeService().UnlockLink("https://node1.premiumize.me/dl/abc/movie.mkv");

                Assert.That(link.Url, Is.EqualTo("https://node1.premiumize.me/dl/abc/movie.mkv"));
            }
            finally
            {
                Config.Instance.PremiumizeApiKey = apiKey;
            }
        }

        // ---------------------------------------------------------- configuration

        [Test]
        public void IsConfigured_IsFalseWithoutAnApiKey()
        {
            var apiKey = Config.Instance.PremiumizeApiKey;
            try
            {
                Config.Instance.PremiumizeApiKey = string.Empty;
                Assert.That(new PremiumizeService().IsConfigured, Is.False);

                Config.Instance.PremiumizeApiKey = "   ";
                Assert.That(new PremiumizeService().IsConfigured, Is.False);

                Config.Instance.PremiumizeApiKey = "a-key";
                Assert.That(new PremiumizeService().IsConfigured, Is.True);
            }
            finally
            {
                Config.Instance.PremiumizeApiKey = apiKey;
            }
        }

        [Test]
        public void ResolveMagnet_FailsFastWhenUnconfigured()
        {
            var apiKey = Config.Instance.PremiumizeApiKey;
            try
            {
                Config.Instance.PremiumizeApiKey = string.Empty;

                var ex = Assert.Throws<DebridException>(() =>
                    new PremiumizeService().ResolveMagnet("magnet:?xt=urn:btih:abc", null, CancelFlag.None));
                Assert.That(ex!.Code, Is.EqualTo("AUTH_MISSING_APIKEY"));
            }
            finally
            {
                Config.Instance.PremiumizeApiKey = apiKey;
            }
        }

        [Test]
        public void SupportsTorrents_IsTrue()
        {
            Assert.That(new PremiumizeService().SupportsTorrents, Is.True);
        }
    }
}
