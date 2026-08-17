using System;

namespace Fluxo.Core.Downloader.Torrent
{
    /// <summary>
    /// What Fluxo needs to (re)create a torrent download: either a magnet URI or the
    /// bytes of a .torrent file. Exactly one of the two is set.
    ///
    /// The bytes are carried rather than a path because the file the user picked may
    /// be gone by the time the download resumes; the engine keeps its own copy of
    /// the metadata under the app data directory.
    /// </summary>
    public class TorrentDownloadInfo : IRequestData
    {
        /// <summary>
        /// Display name, and the folder a multi-file torrent is saved into. Filled in
        /// from the torrent's own name once metadata is available.
        /// </summary>
        public string File { get; set; } = string.Empty;

        /// <summary>A "magnet:?xt=urn:btih:..." URI, or null when <see cref="TorrentFile"/> is set.</summary>
        public string? MagnetUri { get; set; }

        /// <summary>The contents of a .torrent file, or null when <see cref="MagnetUri"/> is set.</summary>
        public byte[]? TorrentFile { get; set; }

        /// <summary>Where the torrent's files are written. Null falls back to the default download folder.</summary>
        public string? SaveDirectory { get; set; }

        public bool IsMagnet => !string.IsNullOrWhiteSpace(MagnetUri);

        public bool IsValid => IsMagnet || (TorrentFile != null && TorrentFile.Length > 0);
    }
}
