using System;

namespace Fluxo.Core.Util
{
    /// <summary>
    /// Small reads over a magnet URI.
    ///
    /// Shared because both the native torrent engine and the debrid clients need the
    /// same thing from a magnet before anything has been fetched, and two copies of
    /// this parsing would drift.
    /// </summary>
    public static class MagnetHelper
    {
        /// <summary>
        /// The "dn" (display name) parameter, or null when the magnet carries none.
        ///
        /// Only ever a hint: the authoritative name arrives with the torrent's
        /// metadata, and callers replace this once they have it.
        /// </summary>
        public static string? DisplayName(string? magnet)
        {
            if (string.IsNullOrEmpty(magnet))
            {
                return null;
            }

            const string marker = "dn=";
            var start = magnet!.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            var end = magnet.IndexOf('&', start);
            var value = end < 0 ? magnet.Substring(start) : magnet.Substring(start, end - start);

            try
            {
                // '+' is a legacy space encoding that UnescapeDataString leaves alone.
                var name = Uri.UnescapeDataString(value.Replace('+', ' ')).Trim();
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
