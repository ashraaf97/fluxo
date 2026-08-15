namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Whether torrent support is usable right now.
    ///
    /// Fluxo has no BitTorrent client of its own - torrents are handed to a debrid
    /// service - so with no credentials configured there is nothing the torrent
    /// entry points can do. The UIs consult this to disable them rather than
    /// letting the user get as far as an error dialog.
    /// </summary>
    public static class DebridSupport
    {
        /// <summary>
        /// Re-evaluated on each call rather than cached, so adding a key in
        /// Settings takes effect without restarting.
        /// </summary>
        public static bool IsConfigured => new AllDebridService().IsConfigured;
    }
}
