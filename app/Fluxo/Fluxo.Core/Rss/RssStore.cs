using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TraceLog;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// Feeds, rules and article history on disk.
    ///
    /// Held as JSON files under the app data directory rather than in the SQLite
    /// database. There are only ever a handful of feeds and rules, the shapes change
    /// as the feature grows, and keeping them out of the schema avoids a migration
    /// every time a rule gains a field.
    ///
    /// Article history is stored per feed and capped, because it is what stops a rule
    /// downloading the same item twice and therefore has to outlive the feed's own
    /// window without growing forever.
    /// </summary>
    public class RssStore
    {
        private readonly object gate = new();

        public RssStore(string? directory = null)
        {
            Directory = directory ?? Path.Combine(Config.DataDir, "rss");
        }

        public string Directory { get; }

        private string FeedsPath => Path.Combine(Directory, "feeds.json");

        private string RulesPath => Path.Combine(Directory, "rules.json");

        private string ArticlesPath(string feedId)
            => Path.Combine(Directory, "articles", Sanitize(feedId) + ".json");

        // ------------------------------------------------------------ feeds

        public IList<RssFeed> LoadFeeds() => Read<List<RssFeed>>(FeedsPath) ?? new List<RssFeed>();

        public void SaveFeeds(IEnumerable<RssFeed> feeds)
        {
            // Articles live in their own per-feed files, so they are not written out
            // again with every subscription-list change.
            var stripped = feeds.Select(f => new RssFeed
            {
                Id = f.Id,
                Url = f.Url,
                Name = f.Name,
                LastRefreshed = f.LastRefreshed,
                LastError = f.LastError,
                Enabled = f.Enabled
            }).ToList();

            Write(FeedsPath, stripped);
        }

        // ------------------------------------------------------------ rules

        public IList<RssRule> LoadRules() => Read<List<RssRule>>(RulesPath) ?? new List<RssRule>();

        public void SaveRules(IEnumerable<RssRule> rules) => Write(RulesPath, rules.ToList());

        // --------------------------------------------------------- articles

        public IList<RssArticle> LoadArticles(string feedId)
            => Read<List<RssArticle>>(ArticlesPath(feedId)) ?? new List<RssArticle>();

        public void SaveArticles(string feedId, IEnumerable<RssArticle> articles)
        {
            var capped = Cap(articles, Math.Max(10, Config.Instance.RssMaxArticlesPerFeed));
            Write(ArticlesPath(feedId), capped);
        }

        /// <summary>
        /// Keeps the newest entries. Articles with no date sort last rather than
        /// being discarded first, since "undated" is common and does not mean "old".
        /// </summary>
        internal static List<RssArticle> Cap(IEnumerable<RssArticle> articles, int limit)
            => articles
                .OrderByDescending(a => a.Published ?? DateTime.MinValue)
                .Take(limit)
                .ToList();

        public void DeleteArticles(string feedId)
        {
            try
            {
                var path = ArticlesPath(feedId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to delete RSS article history");
            }
        }

        // ------------------------------------------------------------- io

        private T? Read<T>(string path) where T : class
        {
            lock (this.gate)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        return null;
                    }
                    return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    // A corrupt file must not stop the app starting; the feature
                    // degrades to empty rather than throwing on load.
                    Log.Debug(ex, $"Failed to read {path}");
                    return null;
                }
            }
        }

        private void Write<T>(string path, T value)
        {
            lock (this.gate)
            {
                try
                {
                    var folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        System.IO.Directory.CreateDirectory(folder!);
                    }

                    // Written beside the target and moved into place, so an
                    // interrupted write cannot leave a half-file behind.
                    var temp = path + ".tmp";
                    File.WriteAllText(temp, JsonConvert.SerializeObject(value, Formatting.Indented));
                    File.Move(temp, path, overwrite: true);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, $"Failed to write {path}");
                }
            }
        }

        /// <summary>Ids are generated, but they still end up in a file name.</summary>
        private static string Sanitize(string id)
        {
            var safe = id;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }
            return string.IsNullOrWhiteSpace(safe) ? "feed" : safe;
        }
    }
}
