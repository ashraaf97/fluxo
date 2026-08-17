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
                        DebridTorrentResolver.StartAll(resolved.Requests, resolved.Folder, resolved.GroupName, input);
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
        /// Resolves user input into download requests. The journey itself lives in
        /// <see cref="DebridTorrentResolver"/>, shared with RSS auto-download; what
        /// stays here is reporting progress back into the dialog.
        /// </summary>
        private ResolveResult Resolve(string input)
        {
            void Progress(string text) => this.view.RunOnUiThread(() => this.view.StatusText = text);

            return new DebridTorrentResolver(ServiceFor(input), this.cancelFlag)
                .ResolveInput(input, Progress);
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

        // Shared with RSS auto-download, which has to classify the same inputs.
        private static bool IsTorrentInput(string input) => DebridTorrentResolver.IsTorrentInput(input);

        private static bool IsTorrentFilePath(string input) => DebridTorrentResolver.IsTorrentFilePath(input);

    }
}
