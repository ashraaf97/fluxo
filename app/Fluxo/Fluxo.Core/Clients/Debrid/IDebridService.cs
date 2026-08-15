using System;
using System.Collections.Generic;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// A debrid service turns a torrent into ordinary HTTP downloads: it fetches
    /// the torrent on its own infrastructure and exposes the result over HTTPS.
    /// Only AllDebrid implements this today; the interface exists so another
    /// provider can be added without reworking the callers.
    /// </summary>
    public interface IDebridService
    {
        /// <summary>Display name, for messages and logs.</summary>
        string Name { get; }

        /// <summary>False when the user has not supplied credentials yet.</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Submits a magnet URI, waits for the service to finish fetching it, and
        /// returns its files.
        /// </summary>
        /// <param name="progress">
        /// Called with a human readable status while waiting. May be null.
        /// </param>
        /// <param name="cancelFlag">Polled so the caller can abort a long wait.</param>
        DebridTorrent ResolveMagnet(string magnet, Action<string>? progress, CancelFlag cancelFlag);

        /// <summary>As <see cref="ResolveMagnet"/>, for the contents of a .torrent file.</summary>
        DebridTorrent ResolveTorrentFile(byte[] torrentFile, string fileName, Action<string>? progress, CancelFlag cancelFlag);

        /// <summary>
        /// Converts a restricted link into a direct, downloadable URL. Also accepts
        /// a premium hoster link pasted by the user. The result is time limited, so
        /// call this as late as possible.
        /// </summary>
        DebridLink UnlockLink(string restrictedLink);
    }

    /// <summary>A direct download URL plus whatever metadata the service supplied.</summary>
    public class DebridLink
    {
        public string Url { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public long Size { get; set; }
    }
}
