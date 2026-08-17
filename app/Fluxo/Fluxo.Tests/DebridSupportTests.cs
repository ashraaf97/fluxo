using System.Linq;
using Fluxo.Core;
using Fluxo.Core.Clients.Debrid;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Which service a torrent is handed to, now that there is more than one and
    /// the user can order them.
    /// </summary>
    [TestFixture]
    public class DebridSupportTests
    {
        private string allDebrid = string.Empty;
        private string realDebrid = string.Empty;
        private int[] order = System.Array.Empty<int>();

        [SetUp]
        public void SaveConfig()
        {
            this.allDebrid = Config.Instance.AllDebridApiKey;
            this.realDebrid = Config.Instance.RealDebridApiKey;
            this.order = Config.Instance.DebridProviderOrder;
        }

        [TearDown]
        public void RestoreConfig()
        {
            Config.Instance.AllDebridApiKey = this.allDebrid;
            Config.Instance.RealDebridApiKey = this.realDebrid;
            Config.Instance.DebridProviderOrder = this.order;
        }

        private static void Configure(string? allDebrid, string? realDebrid, params DebridProvider[] order)
        {
            Config.Instance.AllDebridApiKey = allDebrid ?? string.Empty;
            Config.Instance.RealDebridApiKey = realDebrid ?? string.Empty;
            Config.Instance.DebridProviderOrder = order.Select(p => (int)p).ToArray();
        }

        // -------------------------------------------------------------- ordering

        [Test]
        public void PreferredOrder_KeepsWhatWasSaved()
        {
            Configure(null, null, DebridProvider.RealDebrid, DebridProvider.AllDebrid);

            Assert.That(DebridSupport.PreferredOrder(), Is.EqualTo(new[]
            {
                DebridProvider.RealDebrid,
                DebridProvider.AllDebrid
            }));
        }

        [Test]
        public void PreferredOrder_AppendsProvidersTheSavedOrderDoesNotMention()
        {
            // Settings written before a provider existed must not hide it.
            Configure(null, null, DebridProvider.RealDebrid);

            Assert.That(DebridSupport.PreferredOrder(), Is.EqualTo(new[]
            {
                DebridProvider.RealDebrid,
                DebridProvider.AllDebrid
            }));
        }

        [Test]
        public void PreferredOrder_DropsUnknownAndDuplicateEntries()
        {
            Config.Instance.DebridProviderOrder = new[]
            {
                (int)DebridProvider.RealDebrid,
                99,
                (int)DebridProvider.RealDebrid
            };

            Assert.That(DebridSupport.PreferredOrder(), Is.EqualTo(new[]
            {
                DebridProvider.RealDebrid,
                DebridProvider.AllDebrid
            }));
        }

        [Test]
        public void PreferredOrder_FallsBackToTheDefaultWhenNothingIsSaved()
        {
            Config.Instance.DebridProviderOrder = System.Array.Empty<int>();
            Assert.That(DebridSupport.PreferredOrder(), Is.EqualTo(DebridSupport.DefaultOrder));
        }

        // ------------------------------------------------------------- selection

        [Test]
        public void IsConfigured_IsFalseUntilSomeKeyIsSet()
        {
            Configure(null, null, DebridSupport.DefaultOrder);
            Assert.That(DebridSupport.IsConfigured, Is.False);

            Configure(null, "rd-key", DebridSupport.DefaultOrder);
            Assert.That(DebridSupport.IsConfigured, Is.True);

            Configure("ad-key", null, DebridSupport.DefaultOrder);
            Assert.That(DebridSupport.IsConfigured, Is.True);
        }

        [Test]
        public void Create_UsesTheOnlyConfiguredService()
        {
            Configure(null, "rd-key", DebridSupport.DefaultOrder);
            Assert.That(DebridSupport.Create(), Is.TypeOf<RealDebridService>());

            Configure("ad-key", null, DebridSupport.DefaultOrder);
            Assert.That(DebridSupport.Create(), Is.TypeOf<AllDebridService>());
        }

        [Test]
        public void Create_FollowsTheUsersOrderWhenBothAreConfigured()
        {
            Configure("ad-key", "rd-key", DebridProvider.RealDebrid, DebridProvider.AllDebrid);
            Assert.That(DebridSupport.Create(), Is.TypeOf<RealDebridService>());

            Configure("ad-key", "rd-key", DebridProvider.AllDebrid, DebridProvider.RealDebrid);
            Assert.That(DebridSupport.Create(), Is.TypeOf<AllDebridService>());
        }

        [Test]
        public void Create_SkipsAHigherRankedServiceWithNoKey()
        {
            // Ranking AllDebrid first should not break anything once its key is
            // cleared - the next service in the order takes over.
            Configure(null, "rd-key", DebridProvider.AllDebrid, DebridProvider.RealDebrid);
            Assert.That(DebridSupport.Create(), Is.TypeOf<RealDebridService>());
        }

        [Test]
        public void Create_ReturnsAServiceEvenWhenNothingIsConfigured()
        {
            // Callers get the usual "no API key" DebridException rather than a crash.
            Configure(null, null, DebridSupport.DefaultOrder);

            var service = DebridSupport.Create();

            Assert.That(service, Is.Not.Null);
            Assert.That(service.IsConfigured, Is.False);
        }

        [Test]
        public void DisplayName_NamesEveryProvider()
        {
            Assert.That(DebridSupport.DisplayName(DebridProvider.AllDebrid), Is.EqualTo("AllDebrid"));
            Assert.That(DebridSupport.DisplayName(DebridProvider.RealDebrid), Is.EqualTo("Real-Debrid"));
        }
    }
}
