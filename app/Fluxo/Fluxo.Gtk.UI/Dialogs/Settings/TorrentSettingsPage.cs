using System;
using System.Globalization;
using Gtk;
using Fluxo.Core;
using Fluxo.GtkUI.Utils;
using Translations;

namespace Fluxo.GtkUI.Dialogs.Settings
{
    /// <summary>
    /// The Torrent page of the settings dialog.
    ///
    /// Built in code rather than in settings-dialog.glade, unlike the other pages.
    /// The page is a long, flat run of about twenty labelled fields, and expressing
    /// that as hand-written glade XML would be several hundred lines in which a
    /// mistyped packing position is invisible until the dialog is opened on Linux -
    /// which is the one thing that cannot be checked from here.
    /// </summary>
    internal class TorrentSettingsPage
    {
        private readonly Window parent;

        private readonly Entry txtPort = new();
        private readonly Entry txtMaxDownload = new();
        private readonly Entry txtMaxUpload = new();
        private readonly Entry txtMaxConnections = new();
        private readonly Entry txtMaxConnectionsPerTorrent = new();
        private readonly Entry txtUploadSlots = new();
        private readonly ComboBox cmbEncryption = new();

        private readonly CheckButton chkDht = new();
        private readonly CheckButton chkPex = new();
        private readonly CheckButton chkLsd = new();
        private readonly CheckButton chkPortForwarding = new();

        private readonly Entry txtSaveFolder = new();
        private readonly CheckButton chkCreateSubfolder = new();
        private readonly CheckButton chkIncompleteExtension = new();

        private readonly CheckButton chkSeeding = new();
        private readonly CheckButton chkSuperSeeding = new();
        private readonly Entry txtSeedRatio = new();
        private readonly Entry txtSeedMinutes = new();

        private readonly Entry txtHalfOpen = new();
        private readonly Entry txtOpenFiles = new();
        private readonly Entry txtDiskCache = new();

        /// <summary>Combo entries in listed order; the index maps onto this.</summary>
        private static readonly TorrentEncryptionMode[] EncryptionModes =
        {
            TorrentEncryptionMode.Prefer,
            TorrentEncryptionMode.Require,
            TorrentEncryptionMode.Disable
        };

        public TorrentSettingsPage(Window parent)
        {
            this.parent = parent;
            Widget = Build();
        }

        /// <summary>The page body, ready to be appended to the settings notebook.</summary>
        public Widget Widget { get; }

        private Widget Build()
        {
            var box = new Box(Orientation.Vertical, 10)
            {
                MarginStart = 10,
                MarginEnd = 10,
                MarginTop = 10,
                MarginBottom = 10
            };

            var heading = new Label(TextResource.GetText("SETTINGS_TORRENT")) { Halign = Align.Start };
            heading.StyleContext.AddClass("medium-font");
            box.PackStart(heading, false, true, 0);
            box.PackStart(Wrapped(TextResource.GetText("MSG_TORRENT_INTRO")), false, true, 0);

            Section(box, "LBL_TORRENT_CONNECTION");
            Field(box, "LBL_TORRENT_PORT", this.txtPort);
            Field(box, "LBL_TORRENT_MAX_DOWN", this.txtMaxDownload);
            Field(box, "LBL_TORRENT_MAX_UP", this.txtMaxUpload);
            Field(box, "LBL_TORRENT_MAX_CONN", this.txtMaxConnections);
            Field(box, "LBL_TORRENT_MAX_CONN_PER", this.txtMaxConnectionsPerTorrent);
            Field(box, "LBL_TORRENT_UPLOAD_SLOTS", this.txtUploadSlots);

            GtkHelper.PopulateComboBox(this.cmbEncryption,
                TextResource.GetText("LBL_TORRENT_ENC_PREFER"),
                TextResource.GetText("LBL_TORRENT_ENC_REQUIRE"),
                TextResource.GetText("LBL_TORRENT_ENC_DISABLE"));
            box.PackStart(Caption("LBL_TORRENT_ENCRYPTION"), false, true, 0);
            this.cmbEncryption.Halign = Align.Start;
            box.PackStart(this.cmbEncryption, false, true, 0);

            Section(box, "LBL_TORRENT_DISCOVERY");
            Toggle(box, this.chkDht, "CHK_TORRENT_DHT");
            Toggle(box, this.chkPex, "CHK_TORRENT_PEX");
            Toggle(box, this.chkLsd, "CHK_TORRENT_LSD");
            Toggle(box, this.chkPortForwarding, "CHK_TORRENT_UPNP");

            Section(box, "LBL_TORRENT_FILES");
            box.PackStart(Caption("LBL_TORRENT_SAVE_FOLDER"), false, true, 0);

            var folderRow = new Box(Orientation.Horizontal, 10);
            this.txtSaveFolder.Hexpand = true;
            folderRow.PackStart(this.txtSaveFolder, true, true, 0);
            var browse = new Button("...");
            browse.Clicked += (_, _) =>
            {
                var folder = GtkHelper.SelectFolder(this.parent);
                if (!string.IsNullOrEmpty(folder))
                {
                    this.txtSaveFolder.Text = folder;
                }
            };
            folderRow.PackStart(browse, false, true, 0);
            box.PackStart(folderRow, false, true, 0);

            Toggle(box, this.chkCreateSubfolder, "CHK_TORRENT_SUBFOLDER");
            Toggle(box, this.chkIncompleteExtension, "CHK_TORRENT_INCOMPLETE_EXT");

            Section(box, "LBL_TORRENT_SEEDING");
            box.PackStart(Wrapped(TextResource.GetText("MSG_TORRENT_SEEDING_NOTE")), false, true, 0);
            Toggle(box, this.chkSeeding, "CHK_TORRENT_SEEDING");
            Toggle(box, this.chkSuperSeeding, "CHK_TORRENT_SUPERSEED");
            Field(box, "LBL_TORRENT_SEED_RATIO", this.txtSeedRatio);
            Field(box, "LBL_TORRENT_SEED_TIME", this.txtSeedMinutes);

            Section(box, "LBL_TORRENT_ADVANCED");
            Field(box, "LBL_TORRENT_HALF_OPEN", this.txtHalfOpen);
            Field(box, "LBL_TORRENT_OPEN_FILES", this.txtOpenFiles);
            Field(box, "LBL_TORRENT_DISK_CACHE", this.txtDiskCache);

            var scroller = new ScrolledWindow { Hexpand = true, Vexpand = true };
            scroller.Add(new Viewport { Child = box });
            scroller.ShowAll();
            return scroller;
        }

        // ------------------------------------------------------------ builders

        private static void Section(Box box, string key)
        {
            var label = new Label(TextResource.GetText(key)) { Halign = Align.Start, MarginTop = 8 };
            label.StyleContext.AddClass("medium-font");
            box.PackStart(label, false, true, 0);
        }

        private static Label Caption(string key)
            => new Label(TextResource.GetText(key)) { Halign = Align.Start };

        private static Label Wrapped(string text)
            => new Label(text) { Halign = Align.Start, Wrap = true, Xalign = 0 };

        private static void Field(Box box, string key, Entry entry)
        {
            box.PackStart(Caption(key), false, true, 0);
            entry.WidthChars = 12;
            entry.Halign = Align.Start;
            box.PackStart(entry, false, true, 0);
        }

        private static void Toggle(Box box, CheckButton check, string key)
        {
            check.Label = TextResource.GetText(key);
            box.PackStart(check, false, true, 0);
        }

        // ------------------------------------------------------------- config

        public void LoadConfig()
        {
            var config = Config.Instance;

            this.txtPort.Text = Str(config.TorrentListenPort);
            this.txtMaxDownload.Text = Str(config.TorrentMaxDownloadRate);
            this.txtMaxUpload.Text = Str(config.TorrentMaxUploadRate);
            this.txtMaxConnections.Text = Str(config.TorrentMaxConnections);
            this.txtMaxConnectionsPerTorrent.Text = Str(config.TorrentMaxConnectionsPerTorrent);
            this.txtUploadSlots.Text = Str(config.TorrentUploadSlotsPerTorrent);

            var encryption = Array.IndexOf(EncryptionModes, config.TorrentEncryption);
            this.cmbEncryption.Active = encryption < 0 ? 0 : encryption;

            this.chkDht.Active = config.TorrentEnableDht;
            this.chkPex.Active = config.TorrentEnablePeerExchange;
            this.chkLsd.Active = config.TorrentEnableLocalPeerDiscovery;
            this.chkPortForwarding.Active = config.TorrentEnablePortForwarding;

            this.txtSaveFolder.Text = config.TorrentSaveFolder;
            this.chkCreateSubfolder.Active = config.TorrentCreateSubfolder;
            this.chkIncompleteExtension.Active = config.TorrentAppendExtensionToIncompleteFiles;

            this.chkSeeding.Active = config.TorrentEnableSeeding;
            this.chkSuperSeeding.Active = config.TorrentEnableSuperSeeding;
            this.txtSeedRatio.Text = config.TorrentSeedRatioLimit.ToString("0.##", CultureInfo.InvariantCulture);
            this.txtSeedMinutes.Text = Str(config.TorrentSeedTimeLimitMinutes);

            this.txtHalfOpen.Text = Str(config.TorrentMaxHalfOpenConnections);
            this.txtOpenFiles.Text = Str(config.TorrentMaxOpenFiles);
            this.txtDiskCache.Text = Str(config.TorrentDiskCacheMiB);
        }

        public void UpdateConfig()
        {
            var config = Config.Instance;

            // Anything unparseable keeps the value that was already there rather than
            // silently becoming zero, which for a port or a connection cap would be a
            // functional change the user never asked for.
            config.TorrentListenPort = ReadInt(this.txtPort.Text, config.TorrentListenPort, 0, 65535);
            config.TorrentMaxDownloadRate = ReadInt(this.txtMaxDownload.Text, config.TorrentMaxDownloadRate, 0, int.MaxValue);
            config.TorrentMaxUploadRate = ReadInt(this.txtMaxUpload.Text, config.TorrentMaxUploadRate, 0, int.MaxValue);
            config.TorrentMaxConnections = ReadInt(this.txtMaxConnections.Text, config.TorrentMaxConnections, 1, int.MaxValue);
            config.TorrentMaxConnectionsPerTorrent = ReadInt(this.txtMaxConnectionsPerTorrent.Text,
                config.TorrentMaxConnectionsPerTorrent, 1, int.MaxValue);
            config.TorrentUploadSlotsPerTorrent = ReadInt(this.txtUploadSlots.Text,
                config.TorrentUploadSlotsPerTorrent, 1, int.MaxValue);

            var encryption = this.cmbEncryption.Active;
            config.TorrentEncryption = encryption >= 0 && encryption < EncryptionModes.Length
                ? EncryptionModes[encryption]
                : TorrentEncryptionMode.Prefer;

            config.TorrentEnableDht = this.chkDht.Active;
            config.TorrentEnablePeerExchange = this.chkPex.Active;
            config.TorrentEnableLocalPeerDiscovery = this.chkLsd.Active;
            config.TorrentEnablePortForwarding = this.chkPortForwarding.Active;

            config.TorrentSaveFolder = this.txtSaveFolder.Text.Trim();
            config.TorrentCreateSubfolder = this.chkCreateSubfolder.Active;
            config.TorrentAppendExtensionToIncompleteFiles = this.chkIncompleteExtension.Active;

            config.TorrentEnableSeeding = this.chkSeeding.Active;
            config.TorrentEnableSuperSeeding = this.chkSuperSeeding.Active;
            config.TorrentSeedRatioLimit = ReadDouble(this.txtSeedRatio.Text, config.TorrentSeedRatioLimit);
            config.TorrentSeedTimeLimitMinutes = ReadInt(this.txtSeedMinutes.Text,
                config.TorrentSeedTimeLimitMinutes, 0, int.MaxValue);

            config.TorrentMaxHalfOpenConnections = ReadInt(this.txtHalfOpen.Text,
                config.TorrentMaxHalfOpenConnections, 1, int.MaxValue);
            config.TorrentMaxOpenFiles = ReadInt(this.txtOpenFiles.Text, config.TorrentMaxOpenFiles, 1, int.MaxValue);
            config.TorrentDiskCacheMiB = ReadInt(this.txtDiskCache.Text, config.TorrentDiskCacheMiB, 0, int.MaxValue);
        }

        private static string Str(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static int ReadInt(string? text, int fallback, int min, int max)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;

        private static double ReadDouble(string? text, double fallback)
            => double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0
                ? value
                : fallback;
    }
}
