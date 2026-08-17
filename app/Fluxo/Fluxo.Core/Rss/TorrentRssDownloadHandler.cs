using System;
using System.Linq;
using System.Threading;
using TraceLog;
using Fluxo.Core.Clients.Debrid;
using Fluxo.Core.Clients.Http;
using Fluxo.Core.Downloader;
using Fluxo.Core.Downloader.Torrent;
using Fluxo.Core.Util;

namespace Fluxo.Core.Rss
{
    /// <summary>
    /// Starts a download for an article a rule claimed.
    ///
    /// Feeds carry either a magnet or a link to a .torrent file. The latter is
    /// fetched here rather than handed on as a URL, because what the download path
    /// wants is torrent metadata, not another HTTP download of a small file.
    ///
    /// Routing matches the Add torrent dialog: a debrid service takes it when one is
    /// configured for torrents, and Fluxo's own engine when none is. The dialog and
    /// this share <see cref="DebridTorrentResolver"/> so the two cannot drift.
    /// </summary>
    public class TorrentRssDownloadHandler : IRssDownloadHandler
    {
        public void Download(RssArticle article, RssRule rule)
        {
            var saveFolder = string.IsNullOrWhiteSpace(rule.SaveFolder) ? null : rule.SaveFolder;

            var debrid = DebridSupport.CreateForTorrents();
            if (debrid.IsConfigured)
            {
                DownloadViaDebrid(article, debrid, saveFolder);
                return;
            }

            var info = BuildRequest(article);
            ApplicationContext.CoreService.StartDownload(
                info,
                info.File,
                FileNameFetchMode.None,
                saveFolder,
                Config.Instance.StartDownloadAutomatically,
                null,
                Config.Instance.Proxy,
                null,
                false);
        }

        /// <summary>
        /// Resolving through a debrid service blocks for as long as the service takes
        /// to cache the torrent, which can be minutes. Nobody is waiting on an RSS
        /// pass, but the refresh loop is - so it runs off the pass's thread.
        /// </summary>
        private static void DownloadViaDebrid(RssArticle article, IDebridService debrid, string? saveFolder)
        {
            var link = article.Link.Trim();
            var title = article.Title;

            var thread = new Thread(() =>
            {
                try
                {
                    var resolver = new DebridTorrentResolver(debrid);
                    var resolved = DebridTorrentResolver.IsMagnet(link)
                        ? resolver.ResolveMagnet(link, null)
                        : resolver.ResolveTorrentFile(FetchTorrent(link), title, null);

                    DebridTorrentResolver.Queue(resolved, link, saveFolder);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, $"RSS could not resolve '{title}' through {debrid.Name}");
                }
            })
            { IsBackground = true };

            thread.Start();
        }

        private static TorrentDownloadInfo BuildRequest(RssArticle article)
        {
            var link = article.Link.Trim();

            if (link.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                return new TorrentDownloadInfo
                {
                    MagnetUri = link,
                    File = Name(article, MagnetHelper.DisplayName(link))
                };
            }

            return new TorrentDownloadInfo
            {
                TorrentFile = FetchTorrent(link),
                File = Name(article, null)
            };
        }

        /// <summary>
        /// The article's own title is the better name: a .torrent URL is often an id
        /// with no resemblance to what it contains. The real name replaces this once
        /// the metadata is read.
        /// </summary>
        private static string Name(RssArticle article, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(article.Title))
            {
                return FileHelper.SanitizeFileName(article.Title)!;
            }
            return string.IsNullOrWhiteSpace(fallback) ? "Torrent" : FileHelper.SanitizeFileName(fallback)!;
        }

        private static byte[] FetchTorrent(string url)
        {
            using var http = HttpClientFactory.NewHttpClient(Config.Instance.Proxy);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(30, Config.Instance.NetworkTimeout));

            using var response = http.Send(http.CreateGetRequest(new Uri(url)));
            using var stream = response.GetResponseStream();
            using var buffer = new System.IO.MemoryStream();
            stream.CopyTo(buffer);

            var bytes = buffer.ToArray();
            if (bytes.Length == 0)
            {
                throw new InvalidOperationException("The torrent file was empty");
            }
            return bytes;
        }
    }
}
