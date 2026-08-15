using System;

namespace Fluxo.Core.UI
{
    /// <summary>
    /// Peer interface for the Add Torrent dialog. Implemented directly by the WPF
    /// window and the GTK window; all shared behaviour lives in
    /// <see cref="AddTorrentUIController"/>.
    /// </summary>
    public interface IAddTorrentView
    {
        /// <summary>Magnet URI, hoster URL, or a local .torrent path.</summary>
        string Url { get; set; }

        /// <summary>Status line shown while AllDebrid fetches the torrent.</summary>
        string StatusText { get; set; }

        /// <summary>
        /// Disables input while a resolve is in flight so the dialog cannot be
        /// submitted twice.
        /// </summary>
        bool IsBusy { get; set; }

        void ShowWindow();
        void DestroyWindow();

        /// <summary>Marshals an action onto the UI thread.</summary>
        void RunOnUiThread(Action action);

        event EventHandler? OkClicked;
        event EventHandler? CancelClicked;

        /// <summary>Raised when the user asks to pick a .torrent file from disk.</summary>
        event EventHandler? BrowseTorrentClicked;
    }
}
