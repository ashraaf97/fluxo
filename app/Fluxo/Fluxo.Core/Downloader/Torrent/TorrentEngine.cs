using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using TraceLog;

namespace Fluxo.Core.Downloader.Torrent
{
    /// <summary>
    /// Owns the one BitTorrent engine the process has.
    ///
    /// A single <see cref="ClientEngine"/> is shared by every torrent, because it
    /// holds the listening socket, the DHT node table and the disk cache - all of
    /// which are per-process, not per-download. It is created on first use so an
    /// install that never touches torrents never opens a port.
    ///
    /// Seeding outlives the download it came from: once a torrent completes, Fluxo
    /// reports it finished and hands the manager to <see cref="Supervise"/>, which
    /// keeps it uploading until <see cref="SeedingPolicy"/> says to stop. That is
    /// what stops a seeding torrent from occupying a parallel-download slot forever.
    /// </summary>
    internal static class TorrentEngine
    {
        private static readonly object gate = new();
        private static ClientEngine? engine;

        /// <summary>Torrents that have finished downloading and are now seeding.</summary>
        private static readonly Dictionary<InfoHashes, DateTime> seedingSince = new();

        private static Timer? seedingTimer;

        /// <summary>
        /// Where the engine keeps fast-resume data, DHT nodes and magnet metadata.
        /// MonoTorrent defaults this to a path relative to the working directory,
        /// which for an installed app is not writable, so it is always set.
        /// </summary>
        public static string CacheDirectory => Path.Combine(Config.DataDir, "torrent");

        public static bool IsRunning
        {
            get
            {
                lock (gate)
                {
                    return engine != null && !engine.Disposed;
                }
            }
        }

        public static ClientEngine Instance
        {
            get
            {
                lock (gate)
                {
                    if (engine == null || engine.Disposed)
                    {
                        Directory.CreateDirectory(CacheDirectory);
                        engine = new ClientEngine(BuildSettings());
                        Log.Debug($"Torrent engine started, cache: {CacheDirectory}");

                        // One timer for every seeding torrent rather than one each:
                        // the check is cheap and the interval is coarse.
                        seedingTimer = new Timer(_ => EnforceSeedingLimits(), null,
                            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                    }
                    return engine;
                }
            }
        }

        /// <summary>
        /// Maps Fluxo's settings onto the engine's. Rates are held in KiB/s in the
        /// UI and bytes/s by MonoTorrent; 0 means unlimited on both sides.
        /// </summary>
        private static EngineSettings BuildSettings()
        {
            var config = Config.Instance;
            var port = config.TorrentListenPort < 0 || config.TorrentListenPort > 65535
                ? 0
                : config.TorrentListenPort;

            var builder = new EngineSettingsBuilder
            {
                CacheDirectory = CacheDirectory,

                // Let MonoTorrent persist and reload resume data, DHT nodes and
                // magnet metadata itself. Doing it by hand would duplicate work the
                // engine already does correctly.
                AutoSaveLoadFastResume = true,
                AutoSaveLoadDhtCache = config.TorrentEnableDht,
                AutoSaveLoadMagnetLinkMetadata = true,

                MaximumConnections = Math.Max(1, config.TorrentMaxConnections),
                MaximumHalfOpenConnections = Math.Max(1, config.TorrentMaxHalfOpenConnections),
                MaximumOpenFiles = Math.Max(1, config.TorrentMaxOpenFiles),
                MaximumDownloadRate = ToBytesPerSecond(config.TorrentMaxDownloadRate),
                MaximumUploadRate = ToBytesPerSecond(config.TorrentMaxUploadRate),

                AllowLocalPeerDiscovery = config.TorrentEnableLocalPeerDiscovery,
                AllowPortForwarding = config.TorrentEnablePortForwarding,

                // Marks incomplete files while they are being written, the same idea
                // as qBittorrent's ".!qB" suffix.
                UsePartialFiles = config.TorrentAppendExtensionToIncompleteFiles,

                AllowedEncryption = EncryptionFor(config.TorrentEncryption)
            };

            // 0 means "leave MonoTorrent's own default alone" rather than "no cache",
            // which would be a pathological setting rather than a useful one.
            if (config.TorrentDiskCacheMiB > 0)
            {
                builder.DiskCacheBytes = config.TorrentDiskCacheMiB * 1024 * 1024;
            }

            builder.ListenEndPoints["ipv4"] = new IPEndPoint(IPAddress.Any, port);
            builder.ListenEndPoints["ipv6"] = new IPEndPoint(IPAddress.IPv6Any, port);

            // A DHT endpoint of port 0 still runs DHT, just on a random port, so DHT
            // is disabled by removing the endpoint rather than by zeroing the port.
            builder.DhtEndPoint = config.TorrentEnableDht
                ? new IPEndPoint(IPAddress.Any, port)
                : null;

            // The Windows build is 32-bit, so the disk cache is somewhere the address
            // space can actually be exhausted. The default is modest; keep it.
            return builder.ToSettings();
        }

        private static int ToBytesPerSecond(int kibPerSecond)
            => kibPerSecond <= 0 ? 0 : kibPerSecond * 1024;

        /// <summary>
        /// The three-way choice most clients offer. Order matters: MonoTorrent tries
        /// the listed methods in turn, so putting the encrypted ones first is what
        /// makes "prefer" prefer rather than merely permit.
        /// </summary>
        private static List<EncryptionType> EncryptionFor(TorrentEncryptionMode mode) => mode switch
        {
            TorrentEncryptionMode.Require => new List<EncryptionType>
            {
                EncryptionType.RC4Header, EncryptionType.RC4Full
            },
            TorrentEncryptionMode.Disable => new List<EncryptionType>
            {
                EncryptionType.PlainText
            },
            _ => new List<EncryptionType>
            {
                EncryptionType.RC4Header, EncryptionType.RC4Full, EncryptionType.PlainText
            }
        };

        /// <summary>
        /// Re-applies settings to the running engine. Does nothing when no torrent
        /// has ever been started, since the settings are read at creation anyway.
        /// </summary>
        public static async Task ApplySettingsAsync()
        {
            ClientEngine? current;
            lock (gate)
            {
                current = engine != null && !engine.Disposed ? engine : null;
            }

            if (current == null)
            {
                return;
            }

            try
            {
                await current.UpdateSettingsAsync(BuildSettings());
                Log.Debug("Torrent engine settings updated");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to apply torrent engine settings");
            }
        }

        /// <summary>
        /// Adds a torrent to the engine without starting it. A magnet arrives with no
        /// metadata, so the returned manager may have no file list until it has been
        /// started and the metadata fetched.
        /// </summary>
        public static async Task<TorrentManager> AddAsync(TorrentDownloadInfo info, string saveDirectory)
        {
            if (!info.IsValid)
            {
                throw new InvalidOperationException("Neither a magnet link nor a torrent file was supplied");
            }

            Directory.CreateDirectory(saveDirectory);
            var settings = BuildTorrentSettings();

            if (info.IsMagnet)
            {
                if (!MagnetLink.TryParse(info.MagnetUri, out var magnet))
                {
                    throw new InvalidOperationException("The magnet link could not be parsed");
                }
                return await Instance.AddAsync(magnet, saveDirectory, settings);
            }

            if (!MonoTorrent.Torrent.TryLoad(info.TorrentFile.AsSpan(), out var torrent))
            {
                throw new InvalidOperationException("The .torrent file could not be read");
            }
            return await Instance.AddAsync(torrent, saveDirectory, settings);
        }

        private static TorrentSettings BuildTorrentSettings()
        {
            var config = Config.Instance;
            return new TorrentSettingsBuilder
            {
                AllowDht = config.TorrentEnableDht,
                AllowPeerExchange = config.TorrentEnablePeerExchange,
                AllowInitialSeeding = config.TorrentEnableSuperSeeding,

                MaximumConnections = Math.Max(1, config.TorrentMaxConnectionsPerTorrent),
                UploadSlots = Math.Max(1, config.TorrentUploadSlotsPerTorrent),

                // Keeps the torrent's own top-level folder, matching qBittorrent's
                // "Original" content layout. Without it a multi-file torrent writes
                // its files straight into the save folder and scatters them.
                CreateContainingDirectory = config.TorrentCreateSubfolder
            }.ToSettings();
        }

        /// <summary>
        /// Takes over a completed torrent so it keeps seeding after Fluxo has already
        /// reported the download finished. With seeding switched off it is stopped
        /// and removed instead.
        /// </summary>
        public static void Supervise(TorrentManager manager)
        {
            if (!Config.Instance.TorrentEnableSeeding)
            {
                _ = StopAndRemoveAsync(manager);
                return;
            }

            lock (gate)
            {
                seedingSince[manager.InfoHashes] = DateTime.UtcNow;
            }
            Log.Debug($"Seeding '{manager.Name}'");
        }

        /// <summary>
        /// Stops any seeding torrent that has met its ratio or time limit. Limits are
        /// read fresh each pass, so changing them in Settings affects torrents that
        /// are already seeding.
        /// </summary>
        private static void EnforceSeedingLimits()
        {
            List<KeyValuePair<InfoHashes, DateTime>> tracked;
            lock (gate)
            {
                tracked = seedingSince.ToList();
            }

            foreach (var entry in tracked)
            {
                try
                {
                    var manager = Instance.Torrents.FirstOrDefault(t => Equals(t.InfoHashes, entry.Key));
                    if (manager == null)
                    {
                        Forget(entry.Key);
                        continue;
                    }

                    if (manager.State != TorrentState.Seeding)
                    {
                        continue;
                    }

                    var seededFor = DateTime.UtcNow - entry.Value;
                    if (SeedingPolicy.ShouldStop(manager.Monitor.DataBytesSent,
                            manager.Monitor.DataBytesReceived, seededFor))
                    {
                        Log.Debug($"Seeding limit reached for '{manager.Name}', stopping");
                        Forget(entry.Key);
                        _ = StopAndRemoveAsync(manager);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed while checking seeding limits");
                }
            }
        }

        private static void Forget(InfoHashes infoHashes)
        {
            lock (gate)
            {
                seedingSince.Remove(infoHashes);
            }
        }

        private static async Task StopAndRemoveAsync(TorrentManager manager)
        {
            try
            {
                if (manager.State != TorrentState.Stopped)
                {
                    await manager.StopAsync(TimeSpan.FromSeconds(10));
                }

                // RemoveMode defaults to keeping the downloaded data, which is the
                // only sane choice here - the files are the point.
                await Instance.RemoveAsync(manager);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to stop torrent");
            }
        }

        /// <summary>
        /// Stops everything and releases the port. Called when the app exits; safe to
        /// call when no torrent was ever started.
        /// </summary>
        public static async Task ShutdownAsync()
        {
            ClientEngine? current;
            lock (gate)
            {
                seedingTimer?.Dispose();
                seedingTimer = null;
                seedingSince.Clear();

                current = engine;
                engine = null;
            }

            if (current == null || current.Disposed)
            {
                return;
            }

            try
            {
                await current.StopAllAsync(TimeSpan.FromSeconds(10));
                await current.SaveStateAsync();
                current.Dispose();
                Log.Debug("Torrent engine stopped");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to stop the torrent engine cleanly");
            }
        }
    }
}
