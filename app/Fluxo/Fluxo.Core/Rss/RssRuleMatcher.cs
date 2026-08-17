using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// Decides whether a rule wants an article.
    ///
    /// Deliberately free of feeds, stores and the system clock: every decision is a
    /// function of the rule, the article title and a supplied "now", which is what
    /// makes the whole thing testable without a network or a database.
    /// </summary>
    public static class RssRuleMatcher
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Whether the rule matches the title, ignoring anything stateful. A disabled
        /// rule matches nothing.
        /// </summary>
        public static bool Matches(RssRule rule, string title)
        {
            if (rule == null || !rule.Enabled)
            {
                return false;
            }

            var text = title ?? string.Empty;

            if (!MatchesMustContain(rule, text))
            {
                return false;
            }

            return !MatchesMustNotContain(rule, text);
        }

        /// <summary>
        /// The full decision, including the parts that depend on state: the age
        /// window and the smart episode filter.
        ///
        /// This does not record the match - see <see cref="RecordMatch"/> - so it can
        /// be called from a preview without changing anything.
        /// </summary>
        public static bool Accepts(RssRule rule, RssArticle article, DateTime nowUtc)
        {
            if (article == null || !article.HasLink)
            {
                return false;
            }

            if (!Matches(rule, article.Title))
            {
                return false;
            }

            if (!WithinAgeWindow(rule, article, nowUtc))
            {
                return false;
            }

            if (rule.UseSmartFilter && IsRepeatEpisode(rule, article.Title))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Notes that the rule took the article, so the smart filter can recognise a
        /// repeat of the same episode later.
        /// </summary>
        public static void RecordMatch(RssRule rule, RssArticle article, DateTime nowUtc)
        {
            rule.LastMatch = nowUtc;

            if (!rule.UseSmartFilter)
            {
                return;
            }

            var episode = EpisodeSignature(article.Title);
            if (episode != null && !rule.MatchedEpisodes.Contains(episode))
            {
                rule.MatchedEpisodes.Add(episode);
            }
        }

        // ------------------------------------------------------------ age window

        /// <summary>
        /// Keeps a newly added rule from pulling down a feed's whole back catalogue.
        /// An article with no readable date is let through: refusing it would make a
        /// feed with bad dates silently download nothing.
        /// </summary>
        internal static bool WithinAgeWindow(RssRule rule, RssArticle article, DateTime nowUtc)
        {
            if (rule.IgnoreDays <= 0 || article.Published == null)
            {
                return true;
            }

            return article.Published.Value >= nowUtc.AddDays(-rule.IgnoreDays);
        }

        // -------------------------------------------------------- smart filtering

        private static bool IsRepeatEpisode(RssRule rule, string title)
        {
            var episode = EpisodeSignature(title);
            return episode != null && rule.MatchedEpisodes.Contains(episode);
        }

        /// <summary>
        /// A normalised identifier for the episode a title refers to, or null when it
        /// does not look like an episode at all.
        ///
        /// Recognises "S01E02", "1x02" and a plain date, which between them cover
        /// most of what torrent feeds carry. Matching is on the episode rather than
        /// the whole title so that the same episode reposted at another quality, or
        /// by another group, is recognised as the repeat it is.
        /// </summary>
        internal static string? EpisodeSignature(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var seasonEpisode = Regex.Match(title, @"\bS(\d{1,3})[\s._-]?E(\d{1,4})\b",
                RegexOptions.IgnoreCase, RegexTimeout);
            if (seasonEpisode.Success)
            {
                return Signature(seasonEpisode.Groups[1].Value, seasonEpisode.Groups[2].Value);
            }

            var cross = Regex.Match(title, @"\b(\d{1,3})x(\d{1,4})\b",
                RegexOptions.IgnoreCase, RegexTimeout);
            if (cross.Success)
            {
                return Signature(cross.Groups[1].Value, cross.Groups[2].Value);
            }

            // Daily shows are identified by date rather than by season and episode.
            var dated = Regex.Match(title, @"\b(\d{4})[.\-_](\d{2})[.\-_](\d{2})\b",
                RegexOptions.None, RegexTimeout);
            if (dated.Success)
            {
                return $"{dated.Groups[1].Value}-{dated.Groups[2].Value}-{dated.Groups[3].Value}";
            }

            return null;
        }

        private static string Signature(string season, string episode)
            => $"s{int.Parse(season):D2}e{int.Parse(episode):D2}";

        // ------------------------------------------------------------ expressions

        private static bool MatchesMustContain(RssRule rule, string title)
        {
            // An empty expression takes everything, which is how a rule that claims a
            // whole feed is written.
            if (string.IsNullOrWhiteSpace(rule.MustContain))
            {
                return true;
            }

            return MatchesAnyAlternative(rule.MustContain, title, rule.UseRegex, requireAll: true);
        }

        private static bool MatchesMustNotContain(RssRule rule, string title)
        {
            if (string.IsNullOrWhiteSpace(rule.MustNotContain))
            {
                return false;
            }

            // Any alternative hitting is enough to reject, and within an alternative
            // every word must be present - mirroring how the include side reads.
            return MatchesAnyAlternative(rule.MustNotContain, title, rule.UseRegex, requireAll: true);
        }

        /// <summary>
        /// '|' separates alternatives; whitespace within one separates words that
        /// must all be present. With <paramref name="useRegex"/> each alternative is
        /// a regular expression instead.
        /// </summary>
        private static bool MatchesAnyAlternative(string expression, string title, bool useRegex, bool requireAll)
        {
            foreach (var alternative in expression.Split('|'))
            {
                var trimmed = alternative.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (useRegex)
                {
                    if (IsRegexMatch(trimmed, title))
                    {
                        return true;
                    }
                    continue;
                }

                var words = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var all = requireAll
                    ? words.All(w => ContainsWord(title, w))
                    : words.Any(w => ContainsWord(title, w));

                if (words.Length > 0 && all)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A word match, with '*' and '?' behaving as they do in a shell rather than
        /// as regex metacharacters - which is what someone typing into a filter box
        /// expects.
        /// </summary>
        private static bool ContainsWord(string title, string word)
        {
            if (word.IndexOf('*') < 0 && word.IndexOf('?') < 0)
            {
                return title.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return IsRegexMatch(WildcardToRegex(word), title);
        }

        internal static string WildcardToRegex(string wildcard)
        {
            var builder = new StringBuilder();
            foreach (var c in wildcard)
            {
                builder.Append(c switch
                {
                    '*' => ".*",
                    '?' => ".",
                    _ => Regex.Escape(c.ToString())
                });
            }
            return builder.ToString();
        }

        /// <summary>
        /// A bad expression matches nothing rather than throwing. The expression comes
        /// from a text box, so it is routinely half-typed, and a timeout guards
        /// against a pattern that backtracks catastrophically over a long title.
        /// </summary>
        private static bool IsRegexMatch(string pattern, string title)
        {
            try
            {
                return Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase, RegexTimeout);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
