using Fluxo.Core.Util;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// The display name is the only thing Fluxo can show for a magnet before any
    /// metadata has been fetched, so a row would otherwise sit there unlabelled.
    /// </summary>
    [TestFixture]
    public class MagnetHelperTests
    {
        [Test]
        public void DisplayName_ReadsTheDnParameter()
        {
            Assert.That(
                MagnetHelper.DisplayName("magnet:?xt=urn:btih:abc&dn=Some.Release.2024&tr=udp://x"),
                Is.EqualTo("Some.Release.2024"));
        }

        [Test]
        public void DisplayName_ReadsItAsTheLastParameter()
        {
            Assert.That(
                MagnetHelper.DisplayName("magnet:?xt=urn:btih:abc&dn=Trailing.Name"),
                Is.EqualTo("Trailing.Name"));
        }

        [Test]
        public void DisplayName_DecodesEscapesAndLegacyPluses()
        {
            Assert.That(
                MagnetHelper.DisplayName("magnet:?xt=urn:btih:abc&dn=Some+Release%202024"),
                Is.EqualTo("Some Release 2024"));
        }

        [Test]
        public void DisplayName_IsNullWhenTheMagnetCarriesNone()
        {
            Assert.That(MagnetHelper.DisplayName("magnet:?xt=urn:btih:abc"), Is.Null);
        }

        [Test]
        public void DisplayName_IsNullForAnEmptyName()
        {
            Assert.That(MagnetHelper.DisplayName("magnet:?xt=urn:btih:abc&dn=&tr=udp://x"), Is.Null);
        }

        [Test]
        public void DisplayName_IsNullForNothingAtAll()
        {
            Assert.That(MagnetHelper.DisplayName(null), Is.Null);
            Assert.That(MagnetHelper.DisplayName(string.Empty), Is.Null);
        }
    }
}
