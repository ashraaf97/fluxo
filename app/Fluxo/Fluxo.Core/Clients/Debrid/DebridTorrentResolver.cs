using System;
using System.Collections.Generic;
using System.IO;
using TraceLog;
using Translations;
using Fluxo.Core.Downloader;
using Fluxo.Core.Downloader.Progressive.SingleHttp;
using Fluxo.Core.Util;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Turns a magnet, a .torrent or a hoster link into queued downloads by way of a
    /// debrid service.
    ///
    /// Extracted from the Add torrent dialog so that anything else needing the same
    /// journey - RSS auto-download above all - routes through one implementation
    /// rather than a second copy of it. The dialog keeps the decisions that are
    /// genuinely its own: whether to show the file picker, and what to tell the user
    /// when something fails.
    /// </summary>
    public class DebridTorrentResolver
    {
        private readonly IDebridService service;
        private readonly CancelFlag cancelFlag;

        public DebridTorrentResolver(IDebridService service, CancelFlag? cancelFlag = null)
        {
            this.service = service;
            this.cancelFlag = cancelFlag ?? CancelFlag.None;
        }

        /// <summary>
        /// Resolves whatever the user typed. A local .torrent path is read from disk,
        /// a magnet goes through the torrent flow, and anything else is treated as a
        /// premium hoster link.
        /// </summary>
        public ResolveResult ResolveInput(string input, Action<string>? progress)
        {
            if (IsTorrentFilePath(input))
            {
                return ResolveTorrentFile(File.ReadAllBytes(input), Path.GetFileName(input), progress);
            }

            if (IsMagnet(input))
            {
                return ResolveMagnet(input, progress);
            }

            var link = this.service.UnlockLink(input);
            return new ResolveResult
            {
                Requests = new List<IRequestData> { ToRequest(link.Url, link.FileName, link.Size) }
            };
        }

        public ResolveResult ResolveMagnet(string magnet, Action<string>? progress)
            => FromTorrent(this.service.ResolveMagnet(magnet, progress, this.cancelFlag), progress);

        public ResolveResult ResolveTorrentFile(byte[] torrentFile, string fileName, Action<string>? progress)
            => FromTorrent(this.service.ResolveTorrentFile(torrentFile, fileName, progress, this.cancelFlag), progress);

        private ResolveResult FromTorrent(DebridTorrent torrent, Action<string>? progress)
        {
            var requests = Unlock(torrent.Files, progress);

            // One file behaves as it always has, so the picker still offers a
            // destination for the common "grab this one file" case. The picker has
            // nowhere to put a folder prefix, so flatten to the bare name.
            if (torrent.Files.Count < 2)
            {
                foreach (var request in requests)
                {
                    request.File = TorrentPaths.FileNameOf(request.File ?? string.Empty);
                }
                return new ResolveResult { Requests = requests };
            }

            return new ResolveResult
            {
                Requests = requests,
                Folder = TorrentPaths.RootFolderFor(torrent),
                GroupName = torrent.Name
            };
        }

        /// <summary>
        /// Converts the service's restricted links into direct URLs.
        ///
        /// This happens before the file picker rather than after, because the picker
        /// starts downloads itself and offers no hook in between. The cost is
        /// unlocking files the user may then deselect; the alternative would be
        /// modifying the shared selection controller. Direct URLs are time limited,
        /// but comfortably outlive the few seconds between here and the picker.
        /// </summary>
        private IList<IRequestData> Unlock(IList<DebridFile> files, Action<string>? progress)
        {
            var results = new List<IRequestData>(files.Count);
            var index = 0;
            foreach (var file in files)
            {
                this.cancelFlag.ThrowIfCancellationRequested();
                index++;
                progress?.Invoke($"Preparing links {index}/{files.Count}...");

                try
                {
                    var link = this.service.UnlockLink(file.RestrictedLink);

                    // Carry the torrent-relative path, not just the file name, so the
                    // caller can rebuild the folder structure. The single-file path
                    // flattens this back to a bare name before showing the picker.
                    var relativePath = string.IsNullOrEmpty(file.Path)
                        ? (string.IsNullOrEmpty(link.FileName) ? file.FileName : link.FileName!)
                        : file.Path;

                    results.Add(ToRequest(link.Url,
                        relativePath,
                        link.Size > 0 ? link.Size : file.Size));
                }
                catch (DebridException ex)
                {
                    // One bad file should not sink the whole torrent.
                    Log.Debug($"Skipping '{file.FileName}', unlock failed: {ex.Code ?? ex.Message}");
                }
            }

            if (results.Count == 0)
            {
                throw new DebridException(TextResource.GetText("MSG_DEBRID_FAILED"));
            }
            return results;
        }

        // ------------------------------------------------------------- queueing

        /// <summary>
        /// Queues a resolved result without prompting, which is what an unattended
        /// caller such as RSS needs. A multi-file torrent keeps its folder structure;
        /// a single file goes straight into the target folder.
        /// </summary>
        public static void Queue(ResolveResult resolved, string sourceUrl, string? saveFolder)
        {
            if (resolved.Folder != null)
            {
                StartAll(resolved.Requests, saveFolder ?? resolved.Folder, resolved.GroupName, sourceUrl);
                return;
            }

            foreach (var request in resolved.Requests)
            {
                var name = TorrentPaths.FileNameOf(request.File ?? string.Empty);
                request.File = name;

                ApplicationContext.CoreService.StartDownload(
                    request,
                    name,
                    FileNameFetchMode.None,
                    saveFolder,
                    Config.Instance.StartDownloadAutomatically,
                    null,
                    Config.Instance.Proxy,
                    null,
                    false);
            }
        }

        /// <summary>
        /// Queues every file of a torrent without further prompting, each into the
        /// sub-folder its path within the torrent implies.
        /// </summary>
        public static void StartAll(IEnumerable<IRequestData> requests, string rootFolder,
            string? groupName, string sourceUrl)
        {
            // One group per torrent, so its files collapse into a single expandable
            // row and the completion popup fires once rather than per file.
            var group = DownloadGroupManager.Create(
                string.IsNullOrWhiteSpace(groupName) ? "Torrent" : groupName!,
                sourceUrl,
                rootFolder);

            var started = 0;
            foreach (var request in requests)
            {
                // File carries the torrent-relative path; split it back into the
                // directory to create and the name to save under.
                var relativePath = request.File ?? string.Empty;
                var directory = TorrentPaths.DirectoryOf(relativePath);
                var name = TorrentPaths.FileNameOf(relativePath);

                var targetFolder = string.IsNullOrEmpty(directory)
                    ? rootFolder
                    : Path.Combine(rootFolder, directory);

                request.File = name;

                ApplicationContext.CoreService.StartDownload(
                    request,
                    name,
                    FileNameFetchMode.None,
                    targetFolder,
                    Config.Instance.StartDownloadAutomatically,
                    null,
                    Config.Instance.Proxy,
                    null,
                    false,
                    group.Id);
                started++;
            }

            Log.Debug($"Queued {started} file(s) from torrent '{group.Name}' into {rootFolder}");
        }

        // -------------------------------------------------------------- helpers

        public static IRequestData ToRequest(string url, string? fileName, long size)
            => new SingleSourceHTTPDownloadInfo
            {
                Uri = url,
                File = string.IsNullOrEmpty(fileName) ? FileHelper.GetFileName(new Uri(url)) : fileName!,
                ContentLength = size
            };

        public static bool IsTorrentInput(string input)
            => IsMagnet(input) || IsTorrentFilePath(input);

        public static bool IsMagnet(string input)
            => input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);

        public static bool IsTorrentFilePath(string input)
            => input.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(input);
    }

    /// <summary>
    /// What a resolved input turned into. <see cref="Folder"/> is null when the
    /// result should go through the selection dialog, and set to the torrent's
    /// destination root when it should be queued directly.
    /// </summary>
    public class ResolveResult
    {
        public IList<IRequestData> Requests { get; set; } = new List<IRequestData>();

        public string? Folder { get; set; }

        /// <summary>Torrent name, used to label the group's parent row.</summary>
        public string? GroupName { get; set; }
    }
}
