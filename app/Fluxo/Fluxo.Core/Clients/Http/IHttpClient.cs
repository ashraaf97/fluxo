using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fluxo.Core;

namespace Fluxo.Core.Clients.Http
{
    public interface IHttpClient : IDisposable
    {
        public TimeSpan Timeout { get; set; }

        public HttpRequest CreateGetRequest(Uri uri,
            Dictionary<string, List<string>>? headers = null,
            string? cookies = null,
            AuthenticationInfo? authentication = null);

        public HttpRequest CreatePostRequest(Uri uri,
            Dictionary<string, List<string>>? headers = null,
            string? cookies = null,
            AuthenticationInfo? authentication = null,
            byte[]? body = null);

        /// <summary>
        /// As <see cref="CreatePostRequest"/> but with the PUT verb. Needed because
        /// Real-Debrid only accepts a .torrent upload over PUT.
        /// </summary>
        public HttpRequest CreatePutRequest(Uri uri,
            Dictionary<string, List<string>>? headers = null,
            string? cookies = null,
            AuthenticationInfo? authentication = null,
            byte[]? body = null);

        public HttpResponse Send(HttpRequest request);
        public void Close();
    }
}
