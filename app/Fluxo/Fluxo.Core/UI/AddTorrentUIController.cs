using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TraceLog;
using Translations;
using Fluxo.Core.Clients.Debrid;
using Fluxo.Core.Downloader;
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
        private readonly IDebridService debrid;
        private readonly CancelFlag cancelFlag = new();
        private bool running;

        public AddTorrentUIController(IAddTorrentView view)
            : this(view, new AllDebridService())
        {
        }

        public AddTorrentUIController(IAddTorrentView view, IDebridService debrid)
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

            if (!this.debrid.IsConfigured)
            {
                ShowMessage(TextResource.GetText("MSG_ALLDEBRID_NO_KEY"));
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

        private void ResolveInBackground(string input)
        {
            try
            {
                var links = Resolve(input);
                this.view.RunOnUiThread(() =>
                {
                    this.running = false;
                    this.view.IsBusy = false;
                    this.view.DestroyWindow();
                    ApplicationContext.Application.ShowDownloadSelectionWindow(FileNameFetchMode.None, links);
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
                ReportFailure(TextResource.GetText("MSG_ALLDEBRID_FAILED"));
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
        /// Resolves user input into download requests. A plain hoster URL unlocks
        /// straight to a single link; magnets and .torrent files go through the
        /// torrent flow and can yield many files.
        /// </summary>
        private IEnumerable<IRequestData> Resolve(string input)
        {
            void Progress(string text) => this.view.RunOnUiThread(() => this.view.StatusText = text);

            if (IsTorrentFilePath(input))
            {
                var bytes = File.ReadAllBytes(input);
                return Unlock(this.debrid.ResolveTorrentFile(bytes, Path.GetFileName(input), Progress, this.cancelFlag), Progress);
            }

            if (input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                return Unlock(this.debrid.ResolveMagnet(input, Progress, this.cancelFlag), Progress);
            }

            // Anything else is treated as a premium hoster link. Nothing to pick
            // from, so it unlocks straight to a single download.
            var link = this.debrid.UnlockLink(input);
            return new List<IRequestData> { ToRequest(link.Url, link.FileName, link.Size) };
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
        private IEnumerable<IRequestData> Unlock(IList<DebridFile> files, Action<string> progress)
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
                    var link = this.debrid.UnlockLink(file.RestrictedLink);
                    results.Add(ToRequest(link.Url,
                        string.IsNullOrEmpty(link.FileName) ? file.FileName : link.FileName,
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
                throw new DebridException(TextResource.GetText("MSG_ALLDEBRID_FAILED"));
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
