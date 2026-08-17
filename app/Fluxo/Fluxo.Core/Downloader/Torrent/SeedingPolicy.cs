using System;

namespace Fluxo.Core.Downloader.Torrent
{
    /// <summary>
    /// Decides when a completed torrent has seeded enough.
    ///
    /// MonoTorrent has no ratio or seed-time limits of its own - <c>TorrentSettings</c>
    /// exposes neither - so the stopping rule lives here. Kept free of engine types
    /// so it can be tested directly.
    /// </summary>
    internal static class SeedingPolicy
    {
        /// <summary>
        /// Share ratio: bytes sent over bytes received.
        ///
        /// A torrent that was already complete on disk has downloaded nothing, and
        /// dividing by that would be infinity rather than "seeded forever". Such a
        /// torrent reports 0, leaving the time limit as the only thing that can stop
        /// it - which is the honest answer, since it has no ratio to speak of.
        /// </summary>
        public static double Ratio(long uploaded, long downloaded)
        {
            if (downloaded <= 0 || uploaded <= 0)
            {
                return 0;
            }
            return (double)uploaded / downloaded;
        }

        /// <summary>
        /// Whether seeding should stop now. Either limit alone is enough; a limit of
        /// zero or less means "no limit", which is how both are disabled.
        /// </summary>
        public static bool ShouldStop(double ratio, TimeSpan seededFor, double ratioLimit, TimeSpan timeLimit)
        {
            if (ratioLimit > 0 && ratio >= ratioLimit)
            {
                return true;
            }

            if (timeLimit > TimeSpan.Zero && seededFor >= timeLimit)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// The configured limits, read fresh so a change in Settings applies to
        /// torrents that are already seeding.
        /// </summary>
        public static bool ShouldStop(long uploaded, long downloaded, TimeSpan seededFor)
            => ShouldStop(
                Ratio(uploaded, downloaded),
                seededFor,
                Config.Instance.TorrentSeedRatioLimit,
                TimeSpan.FromMinutes(Math.Max(0, Config.Instance.TorrentSeedTimeLimitMinutes)));
    }
}
