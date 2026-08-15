using System;
using System.IO;
using Fluxo.Core;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Settings are written by a hand-maintained serializer: every field needs a
    /// case in DeserializeConfig, a Write call in SerializeConfig, and a matching
    /// count++ that is back-patched into the header. Miss any one and the setting
    /// silently reverts on restart, which is exactly what happened to
    /// AllowInvalidCertificates. These tests round-trip through disk to catch it.
    /// </summary>
    [TestFixture]
    public class ConfigPersistenceTests
    {
        private string dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            this.dir = Path.Combine(Path.GetTempPath(), "fluxo-cfg-" + Guid.NewGuid());
            Config.LoadConfig(this.dir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(this.dir))
                {
                    Directory.Delete(this.dir, true);
                }
            }
            catch (IOException)
            {
                // Best effort only.
            }
        }

        [Test]
        public void AllDebridApiKey_SurvivesSaveAndReload()
        {
            Config.Instance.AllDebridApiKey = "test-key-12345";
            Config.SaveConfig();

            Config.LoadConfig(this.dir);

            Assert.That(Config.Instance.AllDebridApiKey, Is.EqualTo("test-key-12345"));
        }

        [Test]
        public void AllowInvalidCertificates_SurvivesSaveAndReload()
        {
            Assert.That(Config.Instance.AllowInvalidCertificates, Is.False,
                "certificate validation must be on by default");

            Config.Instance.AllowInvalidCertificates = true;
            Config.SaveConfig();

            Config.LoadConfig(this.dir);

            Assert.That(Config.Instance.AllowInvalidCertificates, Is.True,
                "opting out of certificate validation must persist across restarts");
        }

        [Test]
        public void UnrelatedSettingsStillRoundTrip()
        {
            // Guards the field count: a bad count++ truncates everything written
            // after the offending field, so check one on each side of the additions.
            Config.Instance.AllDebridApiKey = "key";
            Config.Instance.MaxSegments = 6;
            Config.Instance.FallbackUserAgent = "FluxoTest/1.0";
            Config.SaveConfig();

            Config.LoadConfig(this.dir);

            Assert.Multiple(() =>
            {
                Assert.That(Config.Instance.MaxSegments, Is.EqualTo(6));
                Assert.That(Config.Instance.FallbackUserAgent, Is.EqualTo("FluxoTest/1.0"));
                Assert.That(Config.Instance.AllDebridApiKey, Is.EqualTo("key"));
            });
        }
    }
}
