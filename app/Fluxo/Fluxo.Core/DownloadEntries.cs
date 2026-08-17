using Newtonsoft.Json;
using System;
using Fluxo.Core.Downloader;

namespace Fluxo.Core
{
    public abstract class DownloadItemBase : IComparable
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public long Size { get; set; }

        public string? TargetDir { get; set; }

        public DateTime DateAdded { get; set; }

        public string DownloadType { get; set; }

        public FileNameFetchMode FileNameFetchMode { get; set; }

        public string PrimaryUrl { get; set; }

        public string RefererUrl { get; set; }

        public AuthenticationInfo? Authentication { get; set; }

        public ProxyInfo? Proxy { get; set; }

        public int MaxSpeedLimitInKiB { get; set; }

        /// <summary>
        /// The <see cref="DownloadGroup"/> this belongs to, or null for a standalone
        /// download. Members of a group are shown nested under one parent row rather
        /// than as top-level entries.
        /// </summary>
        public string? GroupId { get; set; }

        public int CompareTo(object? obj)
        {
            if (obj == null) return 1;
            if (obj is DownloadItemBase other)
                return this.Name.CompareTo(other.Name);
            else
                throw new ArgumentException("Object is not a DownloadItemBase");
        }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    public class InProgressDownloadItem
        : DownloadItemBase
    {
        public int Progress { get; set; }

        public DownloadStatus Status { get; set; }

        public string? DownloadSpeed { get; set; }

        public string? ETA { get; set; }

        /// <summary>
        /// Torrent only, and deliberately not persisted: upload rate, swarm counts
        /// and share ratio are live readings that mean nothing once the engine has
        /// stopped. They stay empty for every other kind of download.
        /// </summary>
        public string? UploadSpeed { get; set; }

        /// <summary>Connected seeds and peers, already formatted, e.g. "12 / 34".</summary>
        public string? Peers { get; set; }

        public string? Ratio { get; set; }

        public bool IsTorrent => DownloadType == Downloader.DownloadTypes.Torrent;
    }

    public class FinishedDownloadItem : DownloadItemBase
    {
    }

    public enum DownloadStatus
    {
        Downloading, Stopped, Finished, Waiting,

        /// <summary>
        /// A torrent that has finished downloading and is uploading back. Appended
        /// rather than inserted because the values are persisted as integers.
        /// </summary>
        Seeding
    }
}