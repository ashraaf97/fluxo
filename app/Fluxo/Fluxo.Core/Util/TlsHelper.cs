using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using TraceLog;

namespace Fluxo.Core.Util
{
    public static class TlsHelper
    {
        /// <summary>
        /// Applies the process wide TLS settings. Server certificates are validated
        /// normally unless the user has explicitly opted out via
        /// <see cref="Config.AllowInvalidCertificates"/>.
        /// </summary>
        /// <remarks>
        /// This hangs off ServicePointManager, which only gates HttpWebRequest.
        /// That is what the clients under Clients/Http use, so the callback below
        /// does run for them (verified on .NET 10). HttpClient ignores it entirely
        /// and applies its own validation instead. If those clients are ever ported
        /// to HttpClient to clear SYSLIB0014, the certificate check stays safe by
        /// default but <see cref="Config.AllowInvalidCertificates"/> silently stops
        /// working, and would need re-implementing on the handler.
        /// </remarks>
        public static void ApplyDefaults()
        {
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            ServicePointManager.DefaultConnectionLimit = 100;

            // Let the OS decide which protocols are acceptable so deprecated versions
            // (SSL 3.0, TLS 1.0/1.1) are not silently re-enabled here.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }

        private static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (Config.Instance.AllowInvalidCertificates)
            {
                Log.Debug($"Ignoring TLS certificate error '{sslPolicyErrors}' because " +
                    $"{nameof(Config.AllowInvalidCertificates)} is enabled.");
                return true;
            }

            Log.Debug($"Rejected connection, TLS certificate validation failed: {sslPolicyErrors}");
            return false;
        }
    }
}
