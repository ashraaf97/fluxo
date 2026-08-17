using System;
using System.Collections.Generic;
using System.Linq;
using Fluxo.Core.Rss;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Merging and rule selection are what stand between a feed and unattended
    /// downloads. Both failure modes are quiet: re-downloading the same item every
    /// half hour, or two rules each taking the same article.
    /// </summary>
    [TestFixture]
    public class RssServiceTests
    {
        private static readonly DateTime Now = new(2025, 8, 12, 12, 0, 0, DateTimeKind.Utc);

        private static RssArticle Article(string id, string title, DateTime? published = null) => new()
        {
            Id = id,
            Title = title,
            Link = "magnet:?xt=urn:btih:" + id,
            Published = published ?? Now
        };

        private static RssRule Rule(string name, string mustContain, int priority = 0) => new()
        {
            Name = name,
            Enabled = true,
            Priority = priority,
            MustContain = mustContain
        };

        // ----------------------------------------------------------------- merge

        [Test]
        public void Merge_ReportsOnlyArticlesNotSeenBefore()
        {
            // Re-reading a feed must not offer its whole window to the rules again.
            var known = new List<RssArticle> { Article("a", "First") };
            var fetched = new List<RssArticle> { Article("a", "First"), Article("b", "Second") };

            var merged = RssService.Merge(known, fetched, "feed-1", out var fresh);

            Assert.That(merged.Count, Is.EqualTo(2));
            Assert.That(fresh.Select(a => a.Id), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void Merge_KeepsTheStoredCopyOfAKnownArticle()
        {
            // The stored copy carries IsDownloaded; the fetched one never does.
            var known = new List<RssArticle> { Article("a", "First") };
            known[0].IsDownloaded = true;

            var merged = RssService.Merge(known, new List<RssArticle> { Article("a", "First") }, "feed-1", out var fresh);

            Assert.That(fresh, Is.Empty);
            Assert.That(merged.Single(a => a.Id == "a").IsDownloaded, Is.True);
        }

        [Test]
        public void Merge_StampsTheFeedOnNewArticles()
        {
            var merged = RssService.Merge(new List<RssArticle>(),
                new List<RssArticle> { Article("a", "First") }, "feed-9", out _);

            Assert.That(merged[0].FeedId, Is.EqualTo("feed-9"));
        }

        [Test]
        public void Merge_TreatsIdsCaseInsensitively()
        {
            var known = new List<RssArticle> { Article("ABC", "First") };
            var fetched = new List<RssArticle> { Article("abc", "First") };

            RssService.Merge(known, fetched, "feed-1", out var fresh);

            Assert.That(fresh, Is.Empty);
        }

        [Test]
        public void Merge_HandlesAnEmptyHistory()
        {
            var merged = RssService.Merge(new List<RssArticle>(),
                new List<RssArticle> { Article("a", "First"), Article("b", "Second") }, "feed-1", out var fresh);

            Assert.That(merged.Count, Is.EqualTo(2));
            Assert.That(fresh.Count, Is.EqualTo(2));
        }

        // ------------------------------------------------------------- selection

        [Test]
        public void SelectMatches_GivesAnArticleToOneRuleOnly()
        {
            // Two rules both wanting it must not mean two downloads.
            var rules = new[] { Rule("first", "show", 0), Rule("second", "show", 1) };

            var matches = RssService.SelectMatches("feed-1",
                new[] { Article("a", "Show.S01E01") }, rules, Now);

            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].Rule.Name, Is.EqualTo("first"));
        }

        [Test]
        public void SelectMatches_HonoursPriorityOrder()
        {
            var rules = new[] { Rule("low", "show", 10), Rule("high", "show", 1) };

            var matches = RssService.SelectMatches("feed-1",
                new[] { Article("a", "Show.S01E01") }, rules, Now);

            Assert.That(matches[0].Rule.Name, Is.EqualTo("high"));
        }

        [Test]
        public void SelectMatches_SkipsRulesScopedToOtherFeeds()
        {
            var scoped = Rule("scoped", "show");
            scoped.FeedIds = new List<string> { "feed-2" };

            var matches = RssService.SelectMatches("feed-1",
                new[] { Article("a", "Show.S01E01") }, new[] { scoped }, Now);

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void SelectMatches_SkipsDisabledRules()
        {
            var disabled = Rule("off", "show");
            disabled.Enabled = false;

            var matches = RssService.SelectMatches("feed-1",
                new[] { Article("a", "Show.S01E01") }, new[] { disabled }, Now);

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void SelectMatches_SkipsArticlesAlreadyDownloaded()
        {
            var article = Article("a", "Show.S01E01");
            article.IsDownloaded = true;

            var matches = RssService.SelectMatches("feed-1", new[] { article }, new[] { Rule("r", "show") }, Now);

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void SelectMatches_SmartFilterAppliesWithinASinglePass()
        {
            // Two copies of one episode arriving together: the second must be seen as
            // a repeat immediately, not only on the next refresh.
            var rule = Rule("r", "show");
            rule.UseSmartFilter = true;

            var matches = RssService.SelectMatches("feed-1", new[]
            {
                Article("a", "Show.S01E01.720p"),
                Article("b", "Show.S01E01.1080p")
            }, new[] { rule }, Now);

            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].Article.Id, Is.EqualTo("a"));
        }

        [Test]
        public void SelectMatches_TakesEveryMatchingArticleWithoutTheSmartFilter()
        {
            var matches = RssService.SelectMatches("feed-1", new[]
            {
                Article("a", "Show.S01E01"),
                Article("b", "Show.S01E02")
            }, new[] { Rule("r", "show") }, Now);

            Assert.That(matches.Count, Is.EqualTo(2));
        }

        [Test]
        public void SelectMatches_ReturnsNothingWhenNoRulesExist()
        {
            var matches = RssService.SelectMatches("feed-1",
                new[] { Article("a", "Show.S01E01") }, Array.Empty<RssRule>(), Now);

            Assert.That(matches, Is.Empty);
        }

        // -------------------------------------------------------------- history

        [Test]
        public void Cap_KeepsTheNewestArticles()
        {
            var articles = new List<RssArticle>
            {
                Article("old", "Old", Now.AddDays(-10)),
                Article("new", "New", Now),
                Article("mid", "Mid", Now.AddDays(-5))
            };

            var capped = RssStore.Cap(articles, 2);

            Assert.That(capped.Select(a => a.Id), Is.EqualTo(new[] { "new", "mid" }));
        }

        [Test]
        public void Cap_SortsUndatedArticlesLastWithoutDroppingThem()
        {
            // "Undated" is common in real feeds and does not mean "old".
            var undated = Article("undated", "No date");
            undated.Published = null;

            var capped = RssStore.Cap(new List<RssArticle> { undated, Article("new", "New", Now) }, 2);

            Assert.That(capped.Select(a => a.Id), Is.EqualTo(new[] { "new", "undated" }));
        }
    }
}
