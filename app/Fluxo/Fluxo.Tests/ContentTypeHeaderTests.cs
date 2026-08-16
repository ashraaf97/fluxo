using System;
using System.Net.Http;
using Fluxo.Core.Clients.Http;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Uploading a .torrent file failed with
    ///   "The format of value 'multipart/form-data; boundary=----Fluxo...' is invalid"
    /// because the Content-Type was built with the MediaTypeHeaderValue(string)
    /// constructor, which rejects any value carrying a parameter. Plain form posts
    /// have no parameter, so magnet links worked and only file uploads broke.
    /// </summary>
    [TestFixture]
    public class ContentTypeHeaderTests
    {
        private static HttpContent NewContent() => new ByteArrayContent(new byte[] { 1, 2, 3 });

        [Test]
        public void SetContentType_KeepsMultipartBoundary()
        {
            var content = NewContent();
            var value = "multipart/form-data; boundary=----FluxoBoundarybc9c8b05ffa0423c";

            DotNetHttpClient.SetContentType(content, value);

            Assert.That(content.Headers.ContentType, Is.Not.Null);
            Assert.That(content.Headers.ContentType!.MediaType, Is.EqualTo("multipart/form-data"));
            // Without the boundary the server cannot split the body at all.
            Assert.That(content.Headers.ContentType.ToString(), Does.Contain("boundary=----FluxoBoundarybc9c8b05ffa0423c"));
        }

        [Test]
        public void SetContentType_HandlesAPlainMediaType()
        {
            var content = NewContent();

            DotNetHttpClient.SetContentType(content, "application/x-www-form-urlencoded");

            Assert.That(content.Headers.ContentType!.MediaType, Is.EqualTo("application/x-www-form-urlencoded"));
        }

        [Test]
        public void SetContentType_KeepsCharsetParameter()
        {
            var content = NewContent();

            DotNetHttpClient.SetContentType(content, "text/plain; charset=utf-8");

            Assert.That(content.Headers.ContentType!.MediaType, Is.EqualTo("text/plain"));
            Assert.That(content.Headers.ContentType.CharSet, Is.EqualTo("utf-8"));
        }

        [Test]
        public void SetContentType_DoesNotThrowOnAMalformedValue()
        {
            var content = NewContent();

            // A bad content type must degrade, not abort the upload.
            Assert.DoesNotThrow(() => DotNetHttpClient.SetContentType(content, "not a valid header @@@"));
        }

        [Test]
        public void SetContentType_IgnoresEmptyValues()
        {
            var content = NewContent();

            Assert.DoesNotThrow(() => DotNetHttpClient.SetContentType(content, null));
            Assert.DoesNotThrow(() => DotNetHttpClient.SetContentType(content, "   "));
            Assert.That(content.Headers.ContentType, Is.Null);
        }
    }
}
