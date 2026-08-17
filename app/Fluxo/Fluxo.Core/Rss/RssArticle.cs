using System;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// One item from a feed.
    ///
    /// The only field that really matters is <see cref="Link"/>: a torrent feed
    /// carries the magnet or .torrent URL in an enclosure, in the link, or
    /// occasionally in both, and the parser is what settles which one to use.
    /// </summary>
    public class RssArticle
    {
        /// <summary>
        /// The feed's own identifier for the item - its guid or Atom id. Feeds that
        /// supply neither fall back to the link, which is what stops an article being
        /// downloaded twice when a feed is re-read.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        /// <summary>The magnet URI or .torrent URL to hand to the download path.</summary>
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// When the feed says the item was published. Feeds that omit it, or state it
        /// in a form that cannot be read, leave this at default - callers treat that
        /// as "unknown" rather than as the epoch.
        /// </summary>
        public DateTime? Published { get; set; }

        public string? Description { get; set; }

        /// <summary>Set by the store, not the parser.</summary>
        public string FeedId { get; set; } = string.Empty;

        public bool IsDownloaded { get; set; }

        public bool HasLink => !string.IsNullOrWhiteSpace(Link);
    }
}
