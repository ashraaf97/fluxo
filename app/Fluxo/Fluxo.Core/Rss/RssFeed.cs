using System;
using System.Collections.Generic;

namespace Fluxo.Core.Rss
{
    /// <summary>A subscribed feed and what is known about its last refresh.</summary>
    public class RssFeed
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// What to call the feed in the UI. Taken from the feed's own title on first
        /// refresh unless the user has named it themselves.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public DateTime? LastRefreshed { get; set; }

        /// <summary>
        /// Why the last refresh failed, or null when it succeeded. Held rather than
        /// only logged so the UI can show a feed as broken instead of silently stale.
        /// </summary>
        public string? LastError { get; set; }

        public bool Enabled { get; set; } = true;

        public IList<RssArticle> Articles { get; set; } = new List<RssArticle>();

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Url : Name;
    }
}
