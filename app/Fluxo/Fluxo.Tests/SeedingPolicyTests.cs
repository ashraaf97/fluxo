using System;
using Fluxo.Core;
using Fluxo.Core.Downloader.Torrent;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// MonoTorrent has no ratio or seed-time limits of its own, so this rule is
    /// entirely Fluxo's. Getting it wrong means either seeding forever or not at all,
    /// and neither announces itself.
    /// </summary>
    [TestFixture]
    public class SeedingPolicyTests
    {
        private static readonly TimeSpan NoTimeLimit = TimeSpan.Zero;

        // ----------------------------------------------------------------- ratio

        [Test]
        public void Ratio_IsUploadedOverDownloaded()
        {
            Assert.That(SeedingPolicy.Ratio(uploaded: 200, downloaded: 100), Is.EqualTo(2.0));
            Assert.That(SeedingPolicy.Ratio(uploaded: 50, downloaded: 100), Is.EqualTo(0.5));
        }

        [Test]
        public void Ratio_IsZeroWhenNothingWasDownloaded()
        {
            // A torrent that was already complete on disk has downloaded nothing.
            // Dividing by it would be infinity, which would stop seeding instantly.
            Assert.That(SeedingPolicy.Ratio(uploaded: 500, downloaded: 0), Is.EqualTo(0));
        }

        [Test]
        public void Ratio_IsZeroBeforeAnythingIsUploaded()
        {
            Assert.That(SeedingPolicy.Ratio(uploaded: 0, downloaded: 100), Is.EqualTo(0));
        }

        // ----------------------------------------------------------------- limits

        [Test]
        public void ShouldStop_WhenTheRatioTargetIsReached()
        {
            Assert.That(SeedingPolicy.ShouldStop(2.0, TimeSpan.FromMinutes(1), 2.0, NoTimeLimit), Is.True);
            Assert.That(SeedingPolicy.ShouldStop(2.5, TimeSpan.FromMinutes(1), 2.0, NoTimeLimit), Is.True);
        }

        [Test]
        public void ShouldStop_KeepsSeedingBelowTheRatioTarget()
        {
            Assert.That(SeedingPolicy.ShouldStop(1.99, TimeSpan.FromMinutes(1), 2.0, NoTimeLimit), Is.False);
        }

        [Test]
        public void ShouldStop_WhenTheTimeLimitIsReached()
        {
            Assert.That(
                SeedingPolicy.ShouldStop(0.1, TimeSpan.FromHours(2), 0, TimeSpan.FromHours(1)),
                Is.True);
        }

        [Test]
        public void ShouldStop_EitherLimitAloneIsEnough()
        {
            // Ratio met, time not.
            Assert.That(
                SeedingPolicy.ShouldStop(5.0, TimeSpan.FromMinutes(1), 2.0, TimeSpan.FromHours(10)),
                Is.True);

            // Time met, ratio not.
            Assert.That(
                SeedingPolicy.ShouldStop(0.1, TimeSpan.FromHours(11), 2.0, TimeSpan.FromHours(10)),
                Is.True);
        }

        [Test]
        public void ShouldStop_NeverWhenBothLimitsAreDisabled()
        {
            Assert.That(
                SeedingPolicy.ShouldStop(999, TimeSpan.FromDays(30), 0, NoTimeLimit),
                Is.False);
        }

        [Test]
        public void ShouldStop_TreatsANegativeLimitAsDisabled()
        {
            Assert.That(
                SeedingPolicy.ShouldStop(999, TimeSpan.FromDays(30), -1, TimeSpan.FromSeconds(-1)),
                Is.False);
        }

        // ------------------------------------------------------ config-driven form

        [Test]
        public void ShouldStop_ReadsTheLimitsFromConfig()
        {
            var ratio = Config.Instance.TorrentSeedRatioLimit;
            var minutes = Config.Instance.TorrentSeedTimeLimitMinutes;
            try
            {
                Config.Instance.TorrentSeedRatioLimit = 1.5;
                Config.Instance.TorrentSeedTimeLimitMinutes = 0;

                // 150 uploaded against 100 downloaded is exactly 1.5.
                Assert.That(SeedingPolicy.ShouldStop(150, 100, TimeSpan.FromMinutes(5)), Is.True);
                Assert.That(SeedingPolicy.ShouldStop(140, 100, TimeSpan.FromMinutes(5)), Is.False);

                // Limits are read fresh, so raising the target resumes seeding.
                Config.Instance.TorrentSeedRatioLimit = 3.0;
                Assert.That(SeedingPolicy.ShouldStop(150, 100, TimeSpan.FromMinutes(5)), Is.False);
            }
            finally
            {
                Config.Instance.TorrentSeedRatioLimit = ratio;
                Config.Instance.TorrentSeedTimeLimitMinutes = minutes;
            }
        }
    }
}
