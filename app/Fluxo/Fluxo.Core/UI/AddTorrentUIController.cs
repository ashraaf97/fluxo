using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TraceLog;
using Translations;
using Fluxo.Core.Clients.Debrid;
using Fluxo.Core.Downloader;
using Fluxo.Core.Downloader.Torrent;
using Fluxo.Core.Downloader.Progressive.SingleHttp;
using Fluxo.Core.Util;

namespace Fluxo.Core.UI
{
    /// <summary>
    /// Turns a magnet link, .torrent file or premium hoster URL into a set of HTTP
    /// downloads by way of a debrid service, then hands them to the existing
    /// download selection dialog.
    ///
    /// Modelled on <see cref="BatchDownloadUIController"/>, which does the same
    /// one-input-to-many-links expansion.
    /// </summary>
    public class AddTorrentUIController
    {
        private readonly IAddTorrentView view;

        /// <summary>
        /// Set only when a caller supplied a service outright, which the tests do.
        /// Otherwise the service is chosen per input, because a torrent and a hoster
        /// link do not necessarily go to the same place - see <see cref="ServiceFor"/>.
        /// </summary>
        private readonly IDebridService? debrid;

        private readonly CancelFlag cancelFlag = new();
        private bool running;

        public AddTorrentUIController(IAddTorrentView view)
            : this(view, null)
        {
        }

        public AddTorrentUIController(IAddTorrentView view, IDebridService? debrid)
        {
            this.view = view;
            this.debrid = debrid;
            this.view.OkClicked += (s, e) => OnOkClicked();
            this.view.CancelClicked += (s, e) => OnCancelClicked();
            this.view.BrowseTorrentClicked += (s, e) => OnBrowseTorrentClicked();
        }

        public void Run() => this.view.ShowWindow();

        private void OnCancelClicked()
        {
            this.cancelFlag.Cancel();
            this.view.DestroyWindow();
        }

        private void OnBrowseTorrentClicked()
        {
            var file = ApplicationContext.PlatformUIService.OpenFileDialog(null, "torrent", "Torrent files|*.torrent");
            if (!string.IsNullOrEmpty(file))
            {
                this.view.Url = file!;
            }
        }

        private void OnOkClicked()
        {
            if (this.running)
            {
                return;
            }

            var input = (this.view.Url ?? string.Empty).Trim();
            if (input.Length == 0)
            {
                ShowMessage(TextResource.GetText("MSG_INVALID_MAGNET"));
                return;
            }

            // A torrent with no debrid service able to take it falls back to Fluxo's
            // own BitTorrent engine, which needs no subscription. A hoster link has
            // no such fallback: unlocking it is exactly what a debrid service is for.
            if (IsTorrentInput(input) && !ServiceFor(input).IsConfigured)
            {
                StartNativeTorrent(input);
                return;
            }

            var service = ServiceFor(input);
            if (!service.IsConfigured)
            {
                ShowMessage(TextResource.GetText("MSG_DEBRID_NO_KEY"));
                return;
            }

            this.running = true;
            this.view.IsBusy = true;
            this.view.StatusText = TextResource.GetText("MSG_TORRENT_RESOLVING");

            // Resolving polls the service for as long as the torrent takes to
            // cache, so it must not run on the UI thread.
            var thread = new Thread(() => ResolveInBackground(input)) { IsBackground = true };
            thread.Start();
        }

        /// <summary>
        /// Hands the input to Fluxo's own BitTorrent engine. Unlike the debrid path
        /// there is nothing to resolve first - the engine takes the magnet or the
        /// file directly - so this returns immediately and the download appears in
        /// the list straight away.
        /// </summary>
        private void StartNativeTorrent(string input)
        {
            try
            {
                var info = BuildNativeRequest(input);

                ApplicationContext.CoreService.StartDownload(
                    info,
                    info.File,
                    FileNameFetchMode.None,
                    Config.Instance.DefaultDownloadFolder,
                    Config.Instance.StartDownloadAutomatically,
                    null,
                    Config.Instance.Proxy,
                    null,
                    false);

                this.view.DestroyWindow();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to start native torrent");
                ShowMessage(TextResource.GetText("MSG_DEBRID_FAILED"));
            }
        }

        private static TorrentDownloadInfo BuildNativeRequest(string input)
        {
            if (IsTorrentFilePath(input))
            {
                return new TorrentDownloadInfo
                {
                    TorrentFile = File.ReadAllBytes(input),

                    // A placeholder until the metadata supplies the real name, which
                    // the downloader adopts once the torrent is loaded.
                    File = Path.GetFileNameWithoutExtension(input)
                };
            }

            return new TorrentDownloadInfo
            {
                MagnetUri = input,
                File = MagnetHelper.DisplayName(input) ?? "Torrent"
            };
        }

        private void ResolveInBackground(string input)
        {
            try
            {
                var resolved = Resolve(input);
                this.view.RunOnUiThread(() =>
                {
                    this.running = false;
                    this.view.IsBusy = false;
                    this.view.DestroyWindow();

                    // A multi-file torrent is taken as a whole: every file is queued
                    // straight away into a folder mirroring the torrent's own layout.
                    // Only a single file still goes through the picker, where the
                    // dialog is earning its keep by letting you set the destination.
                    if (resolved.Folder != null)
                    {
                        StartAll(resolved.Requests, resolved.Folder, resolved.GroupName, input);
                    }
                    else
                    {
                        ApplicationContext.Application.ShowDownloadSelectionWindow(
                            FileNameFetchMode.None, resolved.Requests);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // The user closed the dialog; nothing to report.
                this.running = false;
            }
            catch (DebridException ex)
            {
                Log.Debug("Debrid resolve failed, code: " + (ex.Code ?? "none"));
                ReportFailure(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Debrid resolve failed");
                ReportFailure(TextResource.GetText("MSG_DEBRID_FAILED"));
            }
        }

        private void ReportFailure(string message)
        {
            this.view.RunOnUiThread(() =>
            {
                this.running = false;
                this.view.IsBusy = false;
                this.view.StatusText = string.Empty;
                ShowMessage(message);
            });
        }

        private void ShowMessage(string message)
            => ApplicationContext.Application.ShowMessageBox(this.view, message);

        /// <summary>
        /// What a resolved input turned into. <see cref="Folder"/> is null when the
        /// result should go through the selection dialog, and set to the torrent's
        /// destination root when it should be queued directly.
        /// </summary>
        private sealed class ResolveResult
        {
            public IList<IRequestData> Requests { get; set; } = new List<IRequestData>();
            public string? Folder { get; set; }

            /// <summary>Torrent name, used to label the group's parent row.</summary>
            public string? GroupName { get; set; }
        }

        /// <summary>
        /// Resolves user input into download requests. A plain hoster URL unlocks
        /// straight to a single link; magnets and .torrent files go through the
        /// torrent flow and can yield many files.
        /// </summary>
        private ResolveResult Resolve(string input)
        {
            void Progress(string text) => this.view.RunOnUiThread(() => this.view.StatusText = text);

            var service = ServiceFor(input);

            if (IsTorrentFilePath(input))
            {
                var bytes = File.ReadAllBytes(input);
                return FromTorrent(service, service.ResolveTorrentFile(bytes, Path.GetFileName(input), Progress, this.cancelFlag), Progress);
            }

            if (IsMagnet(input))
            {
                return FromTorrent(service, service.ResolveMagnet(input, Progress, this.cancelFlag), Progress);
            }

            // Anything else is treated as a premium hoster link. Nothing to pick
            // from, so it unlocks straight to a single download.
            var link = service.UnlockLink(input);
            return new ResolveResult
            {
                Requests = new List<IRequestData> { ToRequest(link.Url, link.FileName, link.Size) }
            };
        }

        /// <summary>
        /// The service this particular input should go to. Torrents need one that
        /// can actually take a torrent; a hoster link can go to any of them.
        /// </summary>
        private IDebridService ServiceFor(string input)
        {
            if (this.debrid != null)
            {
                return this.debrid;
            }

            return IsTorrentInput(input) ? DebridSupport.CreateForTorrents() : DebridSupport.Create();
        }

        private static bool IsTorrentInput(string input)
            => IsMagnet(input) || IsTorrentFilePath(input);

        private static bool IsMagnet(string input)
            => input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);

        private ResolveResult FromTorrent(IDebridService service, DebridTorrent torrent, Action<string> progress)
        {
            var requests = Unlock(service, torrent.Files, progress);

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
        /// Queues every file of a torrent without further prompting, each into the
        /// sub-folder its path within the torrent implies.
        /// </summary>
        private void StartAll(IEnumerable<IRequestData> requests, string rootFolder,
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

        private static bool IsTorrentFilePath(string input)
            => input.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(input);

        /// <summary>
        /// Converts the service's restricted links into direct URLs.
        ///
        /// This happens before the file picker rather than after, because the
        /// picker starts downloads itself and offers no hook in between. The cost
        /// is unlocking files the user may then deselect; the alternative would be
        /// modifying the shared selection controller. Direct URLs are time limited,
        /// but comfortably outlive the few seconds between here and the picker.
        /// </summary>
        private IList<IRequestData> Unlock(IDebridService service, IList<DebridFile> files, Action<string> progress)
        {
            var results = new List<IRequestData>(files.Count);
            var index = 0;
            foreach (var file in files)
            {
                this.cancelFlag.ThrowIfCancellationRequested();
                index++;
                progress($"Preparing links {index}/{files.Count}...");

                try
                {
                    var link = service.UnlockLink(file.RestrictedLink);

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

        private static IRequestData ToRequest(string url, string? fileName, long size)
            => new SingleSourceHTTPDownloadInfo
            {
                Uri = url,
                File = string.IsNullOrEmpty(fileName) ? FileHelper.GetFileName(new Uri(url)) : fileName!,
                ContentLength = size
            };
    }
}
