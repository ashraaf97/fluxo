using System;
using System.Collections.Generic;
using Fluxo.Core.Rss;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// The matcher decides what gets downloaded without anyone watching, so both
    /// failure directions are expensive: a rule that matches nothing looks like a
    /// quiet feed, and one that matches too much fills a disk.
    /// </summary>
    [TestFixture]
    public class RssRuleMatcherTests
    {
        private static readonly DateTime Now = new(2025, 8, 12, 12, 0, 0, DateTimeKind.Utc);

        private static RssRule Rule(string mustContain = "", string mustNotContain = "") => new()
        {
            Name = "test",
            Enabled = true,
            MustContain = mustContain,
            MustNotContain = mustNotContain
        };

        private static RssArticle Article(string title, DateTime? published = null) => new()
        {
            Title = title,
            Link = "magnet:?xt=urn:btih:abc",
            Published = published
        };

        // ------------------------------------------------------------- inclusion

        [Test]
        public void Matches_RequiresEveryWordInAnAlternative()
        {
            var rule = Rule("show 1080p");

            Assert.That(RssRuleMatcher.Matches(rule, "Show.Name.S01E01.1080p"), Is.True);
            Assert.That(RssRuleMatcher.Matches(rule, "Show.Name.S01E01.720p"), Is.False);
        }

        [Test]
        public void Matches_TreatsPipeAsAlternatives()
        {
            var rule = Rule("1080p|2160p");

            Assert.That(RssRuleMatcher.Matches(rule, "Show.1080p"), Is.True);
            Assert.That(RssRuleMatcher.Matches(rule, "Show.2160p"), Is.True);
            Assert.That(RssRuleMatcher.Matches(rule, "Show.720p"), Is.False);
        }

        [Test]
        public void Matches_AnEmptyExpressionTakesEverything()
        {
            // How a rule that claims a whole feed is written.
            Assert.That(RssRuleMatcher.Matches(Rule(), "Literally anything"), Is.True);
        }

        [Test]
        public void Matches_IsCaseInsensitive()
        {
            Assert.That(RssRuleMatcher.Matches(Rule("SHOW"), "show name"), Is.True);
        }

        [Test]
        public void Matches_SupportsShellStyleWildcards()
        {
            // A filter box is not a regex box; '*' should behave as people expect.
            Assert.That(RssRuleMatcher.Matches(Rule("show*1080p"), "Show.Name.S01E01.1080p"), Is.True);
            Assert.That(RssRuleMatcher.Matches(Rule("show*1080p"), "Show.Name.720p"), Is.False);
        }

        // ------------------------------------------------------------- exclusion

        [Test]
        public void Matches_RejectsOnMustNotContain()
        {
            var rule = Rule("show", "hdcam|ts");

            Assert.That(RssRuleMatcher.Matches(rule, "Show.Name.1080p"), Is.True);
            Assert.That(RssRuleMatcher.Matches(rule, "Show.Name.HDCAM"), Is.False);
        }

        [Test]
        public void Matches_ExclusionWinsOverInclusion()
        {
            var rule = Rule("show", "show");
            Assert.That(RssRuleMatcher.Matches(rule, "Show.Name"), Is.False);
        }

        [Test]
        public void Matches_IgnoresADisabledRule()
        {
            var rule = Rule("show");
            rule.Enabled = false;

            Assert.That(RssRuleMatcher.Matches(rule, "Show.Name"), Is.False);
        }

        // ----------------------------------------------------------------- regex

        [Test]
        public void Matches_UsesRegexWhenAsked()
        {
            var rule = Rule(@"^Show\.S0\dE\d\d");
            rule.UseRegex = true;

            Assert.That(RssRuleMatcher.Matches(rule, "Show.S01E05.1080p"), Is.True);
            Assert.That(RssRuleMatcher.Matches(rule, "Other.S01E05"), Is.False);
        }

        [Test]
        public void Matches_ABrokenRegexMatchesNothingRatherThanThrowing()
        {
            // Expressions come from a text box and are routinely half-typed.
            var rule = Rule("Show(unclosed");
            rule.UseRegex = true;

            Assert.DoesNotThrow(() => RssRuleMatcher.Matches(rule, "Show.S01E01"));
            Assert.That(RssRuleMatcher.Matches(rule, "Show.S01E01"), Is.False);
        }

        // ------------------------------------------------------------ age window

        [Test]
        public void Accepts_IgnoresArticlesOlderThanTheWindow()
        {
            // Stops a newly added rule pulling down a whole back catalogue.
            var rule = Rule("show");
            rule.IgnoreDays = 7;

            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E01", Now.AddDays(-2)), Now), Is.True);
            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E01", Now.AddDays(-30)), Now), Is.False);
        }

        [Test]
        public void Accepts_AllowsAnArticleWithNoReadableDate()
        {
            // Refusing it would make a feed with bad dates silently download nothing.
            var rule = Rule("show");
            rule.IgnoreDays = 7;

            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E01"), Now), Is.True);
        }

        [Test]
        public void Accepts_AWindowOfZeroDisablesTheCheck()
        {
            var rule = Rule("show");
            rule.IgnoreDays = 0;

            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E01", Now.AddYears(-5)), Now), Is.True);
        }

        [Test]
        public void Accepts_RejectsAnArticleWithNothingToDownload()
        {
            var article = Article("Show.S01E01");
            article.Link = string.Empty;

            Assert.That(RssRuleMatcher.Accepts(Rule("show"), article, Now), Is.False);
        }

        // ---------------------------------------------------------- smart filter

        [Test]
        public void EpisodeSignature_RecognisesTheCommonForms()
        {
            Assert.That(RssRuleMatcher.EpisodeSignature("Show.S01E02.1080p"), Is.EqualTo("s01e02"));
            Assert.That(RssRuleMatcher.EpisodeSignature("Show 1x02 HDTV"), Is.EqualTo("s01e02"));
            Assert.That(RssRuleMatcher.EpisodeSignature("Daily.Show.2025.08.12"), Is.EqualTo("2025-08-12"));
        }

        [Test]
        public void EpisodeSignature_NormalisesAcrossFormatting()
        {
            // The same episode written three ways must produce one signature.
            Assert.That(RssRuleMatcher.EpisodeSignature("Show.S1E2"), Is.EqualTo("s01e02"));
            Assert.That(RssRuleMatcher.EpisodeSignature("Show.s01.e02"), Is.EqualTo("s01e02"));
        }

        [Test]
        public void EpisodeSignature_IsNullWhenNotAnEpisode()
        {
            Assert.That(RssRuleMatcher.EpisodeSignature("Some.Movie.2024.1080p"), Is.Null);
            Assert.That(RssRuleMatcher.EpisodeSignature(null), Is.Null);
        }

        [Test]
        public void Accepts_SmartFilterSkipsARepeatOfTheSameEpisode()
        {
            // The point of the filter: the same episode reposted at another quality.
            var rule = Rule("show");
            rule.UseSmartFilter = true;

            var first = Article("Show.S01E01.720p");
            Assert.That(RssRuleMatcher.Accepts(rule, first, Now), Is.True);
            RssRuleMatcher.RecordMatch(rule, first, Now);

            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E01.1080p"), Now), Is.False);
            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E02.1080p"), Now), Is.True);
        }

        [Test]
        public void Accepts_SmartFilterLetsRepeatsThroughWhenDisabled()
        {
            var rule = Rule("show");
            rule.UseSmartFilter = false;

            var first = Article("Show.S01E01.720p");
            RssRuleMatcher.RecordMatch(rule, first, Now);

            Assert.That(RssRuleMatcher.Accepts(rule, Article("Show.S01E01.1080p"), Now), Is.True);
        }

        [Test]
        public void RecordMatch_StoresNothingForATitleWithNoEpisode()
        {
            var rule = Rule("movie");
            rule.UseSmartFilter = true;

            RssRuleMatcher.RecordMatch(rule, Article("Some.Movie.2024.1080p"), Now);

            Assert.That(rule.MatchedEpisodes, Is.Empty);
            Assert.That(rule.LastMatch, Is.EqualTo(Now));
        }

        // ------------------------------------------------------------ feed scope

        [Test]
        public void AppliesTo_EmptyFeedListMeansEveryFeed()
        {
            Assert.That(Rule().AppliesTo("any-feed"), Is.True);
        }

        [Test]
        public void AppliesTo_RespectsAnExplicitFeedList()
        {
            var rule = Rule();
            rule.FeedIds = new List<string> { "feed-1" };

            Assert.That(rule.AppliesTo("feed-1"), Is.True);
            Assert.That(rule.AppliesTo("feed-2"), Is.False);
        }
    }
}
