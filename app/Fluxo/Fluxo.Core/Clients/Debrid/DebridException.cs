using System;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Raised when a debrid service reports a failure. Debrid APIs answer with
    /// HTTP 200 and an error envelope in the body, so this carries the service's
    /// own error code rather than a status code.
    /// </summary>
    public class DebridException : Exception
    {
        /// <summary>Service specific code, e.g. "MAGNET_INVALID_URI".</summary>
        public string? Code { get; }

        /// <summary>
        /// True when the failure is transient and the same call is worth retrying
        /// after <see cref="RetryAfter"/>.
        /// </summary>
        public bool IsRateLimit { get; }

        public TimeSpan RetryAfter { get; }

        public DebridException(string message, string? code = null, bool isRateLimit = false, TimeSpan? retryAfter = null)
            : base(message)
        {
            Code = code;
            IsRateLimit = isRateLimit;
            RetryAfter = retryAfter ?? TimeSpan.Zero;
        }
    }
}
