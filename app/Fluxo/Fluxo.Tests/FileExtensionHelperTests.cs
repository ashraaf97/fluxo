using Fluxo.Core.Util;
using NUnit.Framework;

namespace Fluxo.Tests
{
    [TestFixture]
    public class FileExtensionHelperTests
    {
        [TestCase(".ts", ".ts")]
        [TestCase(".TS", ".ts")]
        [TestCase(".mp4", ".mp4")]
        [TestCase(".m4s", ".mp4")]
        [TestCase(".fmp4", ".mp4")]
        [TestCase(".webm", ".mkv")]
        [TestCase("", ".mkv")]
        [TestCase(null, ".mkv")]
        public void GuessContainerFormat_MapsSegmentExtensionToContainer(string ext, string expected)
        {
            Assert.That(FileExtensionHelper.GuessContainerFormatFromSegmentExtension(ext),
                Is.EqualTo(expected));
        }

        [TestCase(".ts", ".ts", ".ts")]
        [TestCase(".mp4", ".m4s", ".mp4")]
        // Mixed audio/video containers have to fall back to a container that holds both.
        [TestCase(".ts", ".mp4", ".mkv")]
        [TestCase(".webm", ".ts", ".mkv")]
        public void GuessContainerFormat_FallsBackToMkvWhenStreamsDisagree(
            string audioExt, string videoExt, string expected)
        {
            Assert.That(FileExtensionHelper.GuessContainerFormatFromSegmentExtension(audioExt, videoExt),
                Is.EqualTo(expected));
        }

        [TestCase("video/mp4", ".mp4")]
        [TestCase("application/pdf", ".pdf")]
        public void GetExtensionFromMimeType_ReturnsDottedExtension(string mime, string expected)
        {
            Assert.That(FileExtensionHelper.GetExtensionFromMimeType(mime), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("application/x-not-a-real-type")]
        public void GetExtensionFromMimeType_ReturnsNullWhenUnknown(string mime)
        {
            Assert.That(FileExtensionHelper.GetExtensionFromMimeType(mime), Is.Null);
        }
    }
}
