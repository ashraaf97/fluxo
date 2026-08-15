using System;
using UI = Gtk.Builder.ObjectAttribute;
using Gtk;
using GLib;
using IoPath = System.IO.Path;
using Fluxo.Core.UI;
using Translations;
using Fluxo.GtkUI.Utils;

namespace Fluxo.GtkUI.Dialogs.AddTorrent
{
    internal class AddTorrentWindow : Window, IAddTorrentView
    {
        private WindowGroup windowGroup;
        private uint pulseTimer;

        [UI] private Label lblMagnet;
        [UI] private Label LblStatus;
        [UI] private Entry TxtUrl;
        [UI] private Button BtnBrowse;
        [UI] private Button BtnOK;
        [UI] private Button BtnCancel;
        [UI] private ProgressBar PrgBusy;

        public string Url { get => TxtUrl.Text; set => TxtUrl.Text = value; }

        public string StatusText { get => LblStatus.Text; set => LblStatus.Text = value; }

        private bool isBusy;
        public bool IsBusy
        {
            get => this.isBusy;
            set
            {
                this.isBusy = value;
                PrgBusy.Visible = value;
                TxtUrl.Sensitive = !value;
                BtnBrowse.Sensitive = !value;
                BtnOK.Sensitive = !value;
                SetPulsing(value);
            }
        }

        public event EventHandler? OkClicked;
        public event EventHandler? CancelClicked;
        public event EventHandler? BrowseTorrentClicked;

        public void ShowWindow() => this.Show();

        public void DestroyWindow()
        {
            SetPulsing(false);
            Close();
            Destroy();
            Dispose();
        }

        /// <summary>
        /// GTK is not thread safe; the controller resolves on a worker thread, so
        /// every UI touch has to be marshalled back onto the main loop.
        /// </summary>
        // System.Action, not Gtk.Action, which is also in scope here.
        public void RunOnUiThread(System.Action action) => Idle.Add(() => { action(); return false; });

        private void SetPulsing(bool on)
        {
            if (on && this.pulseTimer == 0)
            {
                this.pulseTimer = Timeout.Add(100, () => { PrgBusy.Pulse(); return true; });
            }
            else if (!on && this.pulseTimer != 0)
            {
                Source.Remove(this.pulseTimer);
                this.pulseTimer = 0;
            }
        }

        private AddTorrentWindow(Builder builder, Window parent) : base(builder.GetRawOwnedObject("window"))
        {
            builder.Autoconnect(this);
            Title = TextResource.GetText("TOR_TITLE");
            SetDefaultSize(620, 200);
            SetPosition(WindowPosition.CenterAlways);
            TransientFor = parent;

            this.windowGroup = new WindowGroup();
            this.windowGroup.AddWindow(this);

            GtkHelper.AttachSafeDispose(this);
            LoadTexts();

            PrgBusy.Visible = false;

            BtnOK.Clicked += (s, e) => OkClicked?.Invoke(this, EventArgs.Empty);
            BtnCancel.Clicked += (s, e) => CancelClicked?.Invoke(this, EventArgs.Empty);
            BtnBrowse.Clicked += (s, e) => BrowseTorrentClicked?.Invoke(this, EventArgs.Empty);
            DeleteEvent += (s, e) => CancelClicked?.Invoke(this, EventArgs.Empty);
        }

        private void LoadTexts()
        {
            lblMagnet.Text = TextResource.GetText("TOR_MAGNET_LABEL");
            BtnBrowse.Label = TextResource.GetText("TOR_BROWSE");
            BtnOK.Label = TextResource.GetText("MSG_OK");
            BtnCancel.Label = TextResource.GetText("ND_CANCEL");
            LblStatus.Text = string.Empty;
        }

        public static AddTorrentWindow CreateFromGladeFile(Window parent)
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "add-torrent-dialog.glade"));
            return new AddTorrentWindow(builder, parent);
        }
    }
}
