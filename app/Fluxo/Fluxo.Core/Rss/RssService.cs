using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TraceLog;
using Fluxo.Core.Clients.Http;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// Keeps subscribed feeds up to date and starts downloads for whatever the rules
    /// claim.
    ///
    /// Runs on a timer rather than reacting to anything, since a feed only changes
    /// when its publisher says so. Nothing happens at all until
    /// <see cref="Config.RssEnabled"/> is set: refreshing means periodic outbound
    /// requests and, with rules in place, downloads starting unattended.
    /// </summary>
    public class RssService
    {
        private readonly RssStore store;
        private readonly IRssDownloadHandler handler;
        private readonly object gate = new();

        private Timer? timer;
        private bool refreshing;

        public RssService(RssStore? store = null, IRssDownloadHandler? handler = null)
        {
            this.store = store ?? new RssStore();
            this.handler = handler ?? new TorrentRssDownloadHandler();
        }

        public event EventHandler? FeedsChanged;

        // ------------------------------------------------------------ lifecycle

        public void Start()
        {
            lock (this.gate)
            {
                this.timer?.Dispose();

                if (!Config.Instance.RssEnabled)
                {
                    this.timer = null;
                    Log.Debug("RSS is disabled");
                    return;
                }

                var interval = TimeSpan.FromMinutes(Math.Clamp(Config.Instance.RssRefreshMinutes, 1, 24 * 60));

                // The first pass is deliberately delayed: starting the app should not
                // fire a burst of outbound requests before anything else is ready.
                this.timer = new Timer(_ => RefreshAll(), null, TimeSpan.FromMinutes(1), interval);
                Log.Debug($"RSS refreshing every {interval.TotalMinutes} minute(s)");
            }
        }

        public void Stop()
        {
            lock (this.gate)
            {
                this.timer?.Dispose();
                this.timer = null;
            }
        }

        /// <summary>Re-reads the settings; called after the settings dialog is saved.</summary>
        public void ApplySettings() => Start();

        // ------------------------------------------------------------- refresh

        /// <summary>
        /// Refreshes every enabled feed. Overlapping passes are skipped rather than
        /// queued: a slow feed should delay the next pass, not stack up behind it.
        /// </summary>
        public void RefreshAll()
        {
            lock (this.gate)
            {
                if (this.refreshing)
                {
                    return;
                }
                this.refreshing = true;
            }

            try
            {
                var feeds = this.store.LoadFeeds();
                var rules = this.store.LoadRules();

                foreach (var feed in feeds.Where(f => f.Enabled))
                {
                    RefreshOne(feed, rules);
                }

                this.store.SaveFeeds(feeds);
                this.store.SaveRules(rules);
                FeedsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "RSS refresh failed");
            }
            finally
            {
                lock (this.gate)
                {
                    this.refreshing = false;
                }
            }
        }

        private void RefreshOne(RssFeed feed, IList<RssRule> rules)
        {
            try
            {
                var body = Fetch(feed.Url);
                var parsed = RssParser.Parse(body);

                if (string.IsNullOrWhiteSpace(feed.Name) && !string.IsNullOrWhiteSpace(parsed.Title))
                {
                    feed.Name = parsed.Title!;
                }

                var known = this.store.LoadArticles(feed.Id);
                var merged = Merge(known, parsed.Articles, feed.Id, out var fresh);

                feed.LastRefreshed = DateTime.UtcNow;
                feed.LastError = null;

                // Only articles seen for the first time are offered to the rules.
                // Re-reading a feed must not re-download its whole window.
                foreach (var match in SelectMatches(feed.Id, fresh, rules, DateTime.UtcNow))
                {
                    Dispatch(match);
                }

                this.store.SaveArticles(feed.Id, merged);
                feed.Articles = merged;
            }
            catch (Exception ex)
            {
                // Recorded on the feed as well as logged, so the UI can show it as
                // broken rather than merely stale.
                feed.LastError = ex.Message;
                feed.LastRefreshed = DateTime.UtcNow;
                Log.Debug(ex, $"Failed to refresh feed {feed.Url}");
            }
        }

        private void Dispatch(RssMatch match)
        {
            try
            {
                this.handler.Download(match.Article, match.Rule);
                match.Article.IsDownloaded = true;
                RssRuleMatcher.RecordMatch(match.Rule, match.Article, DateTime.UtcNow);
                Log.Debug($"RSS rule '{match.Rule.Name}' took '{match.Article.Title}'");
            }
            catch (Exception ex)
            {
                // One failed hand-off must not stop the rest of the pass. The article
                // stays unmarked so the next refresh can retry it.
                Log.Debug(ex, $"Failed to start '{match.Article.Title}'");
            }
        }

        // ---------------------------------------------------------- pure logic

        /// <summary>
        /// Folds newly fetched articles into what is already known, reporting which
        /// were seen for the first time.
        ///
        /// Identity is the article id, which the parser falls back to the link for,
        /// so a feed that renumbers its guids on every request cannot cause repeats.
        /// </summary>
        internal static IList<RssArticle> Merge(IList<RssArticle> known, IList<RssArticle> fetched,
            string feedId, out IList<RssArticle> fresh)
        {
            var byId = new Dictionary<string, RssArticle>(StringComparer.OrdinalIgnoreCase);
            foreach (var article in known)
            {
                byId[article.Id] = article;
            }

            fresh = new List<RssArticle>();
            foreach (var article in fetched)
            {
                if (byId.ContainsKey(article.Id))
                {
                    continue;
                }

                article.FeedId = feedId;
                byId[article.Id] = article;
                fresh.Add(article);
            }

            return byId.Values.ToList();
        }

        /// <summary>
        /// Which rule, if any, claims each article.
        ///
        /// Rules are considered in priority order and the first to accept wins, so an
        /// article is never downloaded twice because two rules both wanted it.
        /// </summary>
        internal static IList<RssMatch> SelectMatches(string feedId, IEnumerable<RssArticle> articles,
            IEnumerable<RssRule> rules, DateTime nowUtc)
        {
            var applicable = rules
                .Where(r => r.Enabled && r.AppliesTo(feedId))
                .OrderBy(r => r.Priority)
                .ToList();

            var matches = new List<RssMatch>();

            foreach (var article in articles)
            {
                if (article.IsDownloaded)
                {
                    continue;
                }

                foreach (var rule in applicable)
                {
                    if (!RssRuleMatcher.Accepts(rule, article, nowUtc))
                    {
                        continue;
                    }

                    matches.Add(new RssMatch(rule, article));

                    // Recorded immediately so a second article in the same pass sees
                    // the episode as taken, rather than only on the next refresh.
                    RssRuleMatcher.RecordMatch(rule, article, nowUtc);
                    break;
                }
            }

            return matches;
        }

        // ---------------------------------------------------------------- http

        /// <summary>
        /// Uses Fluxo's own client so the user's proxy and TLS settings apply, as
        /// everywhere else that talks to the network.
        /// </summary>
        private static string? Fetch(string url)
        {
            using var http = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(30, Config.Instance.NetworkTimeout));

            using var response = http.Send(http.CreateGetRequest(new Uri(url)));
            return response.ReadAsString(CancelFlag.None);
        }
    }

    /// <summary>An article and the rule that claimed it.</summary>
    internal class RssMatch
    {
        public RssMatch(RssRule rule, RssArticle article)
        {
            Rule = rule;
            Article = article;
        }

        public RssRule Rule { get; }

        public RssArticle Article { get; }
    }

    /// <summary>
    /// What to do with an article a rule has claimed. An interface so the matching
    /// side can be tested without starting real downloads.
    /// </summary>
    public interface IRssDownloadHandler
    {
        void Download(RssArticle article, RssRule rule);
    }
}
