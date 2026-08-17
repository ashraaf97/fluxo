using System;
using System.Collections.Generic;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// One auto-download rule: what to match, where it applies, and what has already
    /// been taken because of it.
    ///
    /// Matching itself lives in <see cref="RssRuleMatcher"/> so it can be tested
    /// without a feed, a store or a clock.
    /// </summary>
    public class RssRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Lower runs first. Only decides which rule claims an article when more than
        /// one would match it.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Words the title must carry. Space separated means all of them; '|'
        /// separates alternatives. Empty matches everything, which is how a rule that
        /// takes a whole feed is written.
        /// </summary>
        public string MustContain { get; set; } = string.Empty;

        /// <summary>Any of these in the title rejects the article outright.</summary>
        public string MustNotContain { get; set; } = string.Empty;

        /// <summary>
        /// Treat both expressions as regular expressions rather than as words with
        /// '*' wildcards.
        /// </summary>
        public bool UseRegex { get; set; }

        /// <summary>
        /// Ignore anything published more than this many days ago, so adding a rule
        /// does not pull down a feed's entire back catalogue. 0 disables the window.
        /// </summary>
        public int IgnoreDays { get; set; }

        /// <summary>
        /// Skip an article whose episode has already been taken. Catches the case of
        /// the same episode reposted at a different quality or by another group.
        /// </summary>
        public bool UseSmartFilter { get; set; }

        /// <summary>
        /// Feeds this rule applies to. Empty means every feed, which is the default
        /// and the common case.
        /// </summary>
        public IList<string> FeedIds { get; set; } = new List<string>();

        /// <summary>Where matches are saved. Empty falls back to the usual folder.</summary>
        public string SaveFolder { get; set; } = string.Empty;

        /// <summary>
        /// Episode signatures already taken by this rule, used by the smart filter.
        /// Kept on the rule rather than derived from the download list, which the
        /// user is free to clear.
        /// </summary>
        public IList<string> MatchedEpisodes { get; set; } = new List<string>();

        public DateTime? LastMatch { get; set; }

        /// <summary>True when this rule has anything to say about the given feed.</summary>
        public bool AppliesTo(string feedId)
            => FeedIds == null || FeedIds.Count == 0 || FeedIds.Contains(feedId);
    }
}
