using System;
using System.Collections.Generic;
using System.Linq;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Picks the debrid service to use, and reports whether torrent support is
    /// usable at all.
    ///
    /// Fluxo has no BitTorrent client of its own - torrents are handed to a debrid
    /// service - so with no credentials configured there is nothing the torrent
    /// entry points can do. The UIs consult <see cref="IsConfigured"/> to disable
    /// them rather than letting the user get as far as an error dialog.
    /// </summary>
    public static class DebridSupport
    {
        /// <summary>
        /// Every supported provider, in the order used when the user has expressed
        /// no preference of their own.
        /// </summary>
        public static readonly DebridProvider[] DefaultOrder =
        {
            DebridProvider.AllDebrid,
            DebridProvider.RealDebrid
        };

        /// <summary>
        /// The user's ordering, repaired as needed: unknown and duplicate entries
        /// are dropped, and any provider the stored order does not mention is
        /// appended in <see cref="DefaultOrder"/> order.
        ///
        /// That last part is what stops a provider added in a future version from
        /// being invisible to everyone whose settings predate it.
        /// </summary>
        public static IList<DebridProvider> PreferredOrder()
        {
            var order = new List<DebridProvider>(DefaultOrder.Length);

            foreach (var value in Config.Instance.DebridProviderOrder ?? Array.Empty<int>())
            {
                var provider = (DebridProvider)value;
                if (Array.IndexOf(DefaultOrder, provider) >= 0 && !order.Contains(provider))
                {
                    order.Add(provider);
                }
            }

            foreach (var provider in DefaultOrder)
            {
                if (!order.Contains(provider))
                {
                    order.Add(provider);
                }
            }

            return order;
        }

        /// <summary>
        /// Services in the user's order. Nothing is cached: a key added in Settings
        /// takes effect without a restart.
        /// </summary>
        public static IList<IDebridService> All()
            => PreferredOrder().Select(Create).ToList();

        /// <summary>True when at least one service has a key.</summary>
        public static bool IsConfigured => All().Any(s => s.IsConfigured);

        /// <summary>
        /// The service to use right now: the first one in the user's order that has
        /// an API key.
        ///
        /// Returns an unconfigured service rather than null when nothing is set up,
        /// so callers still get the usual "no API key" error instead of a crash.
        /// </summary>
        public static IDebridService Create()
        {
            var services = All();
            return services.FirstOrDefault(s => s.IsConfigured) ?? services[0];
        }

        /// <summary>The name to show for a provider, taken from the service itself.</summary>
        public static string DisplayName(DebridProvider provider) => Create(provider).Name;

        private static IDebridService Create(DebridProvider provider) => provider switch
        {
            DebridProvider.RealDebrid => new RealDebridService(),
            _ => new AllDebridService()
        };
    }
}
