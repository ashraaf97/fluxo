using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.Client;
using TraceLog;
using Fluxo.Core.IO;
using Fluxo.Core.Util;

namespace Fluxo.Core.Downloader.Torrent
{
    /// <summary>
    /// Presents a torrent as an ordinary Fluxo download.
    ///
    /// Implementing <see cref="IBaseDownloader"/> is what lets the existing queue,
    /// progress window, notification and list machinery treat a swarm like any other
    /// download without knowing what BitTorrent is.
    ///
    /// The lifecycle differs from the HTTP downloaders in one way that matters:
    /// reaching 100% is not the end. <see cref="Finished"/> fires at completion so
    /// the app frees the parallel-download slot and moves the row to the finished
    /// list, while the torrent itself carries on seeding under
    /// <see cref="TorrentEngine.Supervise"/>.
    /// </summary>
    public class TorrentDownloader : IBaseDownloader
    {
        private readonly ReaderWriterLockSlim rwLock = new();
        private readonly CancelFlag cancelFlag = new();

        /// <summary>
        /// <see cref="CancelFlag"/> satisfies the interface but carries no
        /// <see cref="CancellationToken"/>, and the engine's async calls want a real
        /// one. Both are cancelled together in <see cref="Stop"/>.
        /// </summary>
        private readonly CancellationTokenSource cts = new();

        private TorrentDownloadInfo? info;
        private TorrentManager? manager;
        private Timer? progressTimer;

        private long lastReportedDownloaded;
        private bool finishedReported;
        private volatile bool stopping;

        public TorrentDownloader(TorrentDownloadInfo info)
        {
            Id = Guid.NewGuid().ToString();
            this.info = info;
            TargetFileName = info.File;
            TargetDir = info.SaveDirectory;
        }

        /// <summary>Restores a download across a restart; the info is loaded on <see cref="Start"/>.</summary>
        public TorrentDownloader(string id)
        {
            Id = id;
        }

        public string? Id { get; private set; }

        public string Type => DownloadTypes.Torrent;

        public bool IsCancelled => this.cancelFlag.IsCancellationRequested;

        public ReaderWriterLockSlim Lock => this.rwLock;

        public string? TargetDir { get; private set; }

        public string? TargetFileName { get; private set; }

        public string? TargetFile
            => TargetDir == null || TargetFileName == null ? null : Path.Combine(TargetDir, TargetFileName);

        /// <summary>
        /// Total size of the torrent's files. A magnet has no metadata until the
        /// swarm supplies it, so this reads 0 until then.
        /// </summary>
        public long FileSize => this.manager?.Torrent?.Size ?? 0;

        public FileNameFetchMode FileNameFetchMode { get; private set; } = FileNameFetchMode.None;

        public Uri? PrimaryUrl
            => Uri.TryCreate(this.info?.MagnetUri ?? string.Empty, UriKind.Absolute, out var uri) ? uri : null;

        public event EventHandler? Probed;
        public event EventHandler? Finished;
        public event EventHandler? Started;
        public event EventHandler<ProgressResultEventArgs>? ProgressChanged;
        public event EventHandler<ProgressResultEventArgs>? AssembingProgressChanged;
        public event EventHandler? Cancelled;
        public event EventHandler<DownloadFailedEventArgs>? Failed;

        public void SetFileName(string name, FileNameFetchMode fileNameFetchMode)
        {
            TargetFileName = name;
            FileNameFetchMode = fileNameFetchMode;
        }

        public void SetTargetDirectory(string? folder) => TargetDir = folder;

        public long GetDownloaded() => this.manager?.Monitor.DataBytesReceived ?? 0;

        public long GetTotalDownloaded() => GetDownloaded();

        public void Start()
        {
            this.stopping = false;
            RunInBackground(StartAsync);
        }

        /// <summary>Resuming a torrent is starting it again; the engine has the resume data.</summary>
        public void Resume() => Start();

        public void Stop()
        {
            this.stopping = true;
            this.cancelFlag.Cancel();
            this.cts.Cancel();
            RunInBackground(async () =>
            {
                await StopManagerAsync();
                Cancelled?.Invoke(this, EventArgs.Empty);
            });
        }

        /// <summary>Stops without cancelling, so the download can be picked up later.</summary>
        public void SaveForLater()
        {
            this.stopping = true;
            RunInBackground(StopManagerAsync);
        }

        // ---------------------------------------------------------------- internals

        private async Task StartAsync()
        {
            var request = this.info ?? RequestDataIO.LoadTorrentDownloadInfo(Id!);
            if (request == null || !request.IsValid)
            {
                Log.Debug($"No torrent request data for {Id}");
                Fail(ErrorCode.Generic);
                return;
            }
            this.info = request;

            var saveDirectory = ResolveSaveDirectory(request);
            TargetDir = saveDirectory;

            this.manager = await TorrentEngine.AddAsync(request, saveDirectory);
            this.manager.TorrentStateChanged += OnStateChanged;

            await this.manager.StartAsync();
            Started?.Invoke(this, EventArgs.Empty);

            // A magnet has no file list yet. Waiting here rather than in the caller
            // keeps the "size unknown" window inside the downloader.
            if (request.IsMagnet && !this.manager.HasMetadata)
            {
                await this.manager.WaitForMetadataAsync(this.cts.Token);
            }

            AdoptTorrentName();
            Probed?.Invoke(this, EventArgs.Empty);

            // MonoTorrent pushes no periodic progress, so it is polled. One second
            // matches what the speed and ETA columns can usefully show.
            this.progressTimer = new Timer(_ => ReportProgress(), null,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// A magnet is added before its real name is known, so the placeholder taken
        /// from the link is replaced once metadata arrives.
        /// </summary>
        private void AdoptTorrentName()
        {
            var name = this.manager?.Torrent?.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                TargetFileName = FileHelper.SanitizeFileName(name);
            }
        }

        private string ResolveSaveDirectory(TorrentDownloadInfo request)
        {
            var folder = TargetDir
                ?? request.SaveDirectory
                ?? Config.Instance.DefaultDownloadFolder;

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = FileHelper.GetDownloadFolderByFileName(request.File);
            }

            return folder!;
        }

        private void ReportProgress()
        {
            var current = this.manager;
            if (current == null || this.stopping)
            {
                return;
            }

            try
            {
                var downloaded = current.Monitor.DataBytesReceived;
                var speed = current.Monitor.DownloadRate;
                var remaining = FileSize - downloaded;

                ProgressChanged?.Invoke(this, new ProgressResultEventArgs
                {
                    Progress = (int)Math.Round(Math.Clamp(current.Progress, 0, 100)),
                    DownloadSpeed = speed,
                    Downloaded = downloaded,
                    Eta = speed > 0 && remaining > 0 ? remaining / speed : 0
                });

                this.lastReportedDownloaded = downloaded;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to report torrent progress");
            }
        }

        /// <summary>
        /// Completion is a state change to Seeding, not an event of its own. Error is
        /// handled here too, since MonoTorrent reports failures the same way.
        /// </summary>
        private void OnStateChanged(object? sender, TorrentStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case TorrentState.Seeding when !this.finishedReported:
                    this.finishedReported = true;
                    ReportProgress();
                    StopProgressTimer();

                    // Hand the swarm over before announcing completion: the app will
                    // drop this downloader as soon as Finished fires.
                    if (this.manager != null)
                    {
                        TorrentEngine.Supervise(this.manager);
                    }
                    Log.Debug($"Torrent complete: {TargetFileName}");
                    Finished?.Invoke(this, EventArgs.Empty);
                    break;

                case TorrentState.Error:
                    Log.Debug($"Torrent error: {this.manager?.Error?.Reason}");
                    Fail(ErrorCode.Generic);
                    break;
            }
        }

        private async Task StopManagerAsync()
        {
            StopProgressTimer();

            var current = this.manager;
            if (current == null)
            {
                return;
            }

            try
            {
                current.TorrentStateChanged -= OnStateChanged;
                if (current.State != TorrentState.Stopped)
                {
                    await current.StopAsync(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to stop torrent");
            }
        }

        private void StopProgressTimer()
        {
            this.progressTimer?.Dispose();
            this.progressTimer = null;
        }

        private void Fail(ErrorCode code)
        {
            StopProgressTimer();
            Failed?.Invoke(this, new DownloadFailedEventArgs(code));
        }

        /// <summary>
        /// The interface is synchronous and the engine is not, so the work runs on a
        /// background thread. Faults are turned into Failed rather than being left to
        /// take down the process as an unobserved task exception.
        /// </summary>
        private void RunInBackground(Func<Task> work)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await work();
                }
                catch (OperationCanceledException)
                {
                    // Stop() was called; Cancelled has already been raised.
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Torrent download failed");
                    Fail(ErrorCode.Generic);
                }
            });
        }
    }
}
