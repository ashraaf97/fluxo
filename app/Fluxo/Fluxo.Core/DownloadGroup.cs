using System;

namespace Fluxo.Core
{
    /// <summary>
    /// A set of downloads that came from one source and are shown as a single
    /// expandable row - today that means the files of one torrent.
    ///
    /// The group only carries identity and provenance. Everything shown on the
    /// parent row (size, progress, speed, status) is derived from its members at
    /// display time rather than stored, so it cannot drift out of step with them.
    /// </summary>
    public class DownloadGroup
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>Torrent name, used as the parent row's label.</summary>
        public string Name { get; set; } = string.Empty;

        public DateTime DateAdded { get; set; }

        /// <summary>The magnet URI or .torrent path this came from, for reference.</summary>
        public string? SourceUrl { get; set; }

        /// <summary>
        /// Where the group was saved. Lets "open folder" work on the parent row
        /// without having to inspect a member.
        /// </summary>
        public string? TargetDir { get; set; }

        /// <summary>
        /// True once every member has finished, which is what moves the group from
        /// the in-progress list to the finished one and fires the completion popup.
        /// </summary>
        public bool Completed { get; set; }
    }
}
