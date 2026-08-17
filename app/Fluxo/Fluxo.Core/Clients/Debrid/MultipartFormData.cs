using System;
using System.Text;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Builds a one-field multipart/form-data body for uploading a .torrent file.
    ///
    /// Shared because more than one service takes torrents this way, and because
    /// the framing is fiddly enough - exact CRLFs, the trailing "--" on the closing
    /// boundary - that having two copies of it drift apart would be unpleasant.
    /// </summary>
    internal static class MultipartFormData
    {
        public static string NewBoundary() => "----FluxoBoundary" + Guid.NewGuid().ToString("N");

        public static string ContentTypeFor(string boundary) => "multipart/form-data; boundary=" + boundary;

        public static byte[] Build(string boundary, string fieldName, string fileName, byte[] content)
        {
            var header = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"{fieldName}\"; filename=\"{SanitizeFileName(fileName)}\"\r\n" +
                "Content-Type: application/x-bittorrent\r\n\r\n");
            var footer = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

            var body = new byte[header.Length + content.Length + footer.Length];
            Buffer.BlockCopy(header, 0, body, 0, header.Length);
            Buffer.BlockCopy(content, 0, body, header.Length, content.Length);
            Buffer.BlockCopy(footer, 0, body, header.Length + content.Length, footer.Length);
            return body;
        }

        /// <summary>
        /// Quotes and newlines would break out of the Content-Disposition header,
        /// so they are removed rather than escaped.
        /// </summary>
        internal static string SanitizeFileName(string name)
            => string.IsNullOrEmpty(name)
                ? "upload.torrent"
                : name.Replace("\"", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
