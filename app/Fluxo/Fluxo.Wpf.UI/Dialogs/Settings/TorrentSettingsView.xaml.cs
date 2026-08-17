using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Translations;
using Fluxo.Core;
using Fluxo.Core.UI;
using WinForms = System.Windows.Forms;

namespace Fluxo.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// Interaction logic for TorrentSettingsView.xaml
    /// </summary>
    public partial class TorrentSettingsView : UserControl, ISettingsPage
    {
        /// <summary>
        /// Combo entries in listed order, so the selected index maps onto this rather
        /// than onto the enum's numeric values.
        /// </summary>
        private static readonly TorrentEncryptionMode[] EncryptionModes =
        {
            TorrentEncryptionMode.Prefer,
            TorrentEncryptionMode.Require,
            TorrentEncryptionMode.Disable
        };

        public TorrentSettingsView()
        {
            InitializeComponent();
            CmbEncryption.ItemsSource = new[]
            {
                TextResource.GetText("LBL_TORRENT_ENC_PREFER"),
                TextResource.GetText("LBL_TORRENT_ENC_REQUIRE"),
                TextResource.GetText("LBL_TORRENT_ENC_DISABLE")
            };
        }

        public void PopulateUI()
        {
            var config = Config.Instance;

            TxtPort.Text = config.TorrentListenPort.ToString(CultureInfo.InvariantCulture);
            TxtMaxDownload.Text = config.TorrentMaxDownloadRate.ToString(CultureInfo.InvariantCulture);
            TxtMaxUpload.Text = config.TorrentMaxUploadRate.ToString(CultureInfo.InvariantCulture);
            TxtMaxConnections.Text = config.TorrentMaxConnections.ToString(CultureInfo.InvariantCulture);
            TxtMaxConnectionsPerTorrent.Text = config.TorrentMaxConnectionsPerTorrent.ToString(CultureInfo.InvariantCulture);
            TxtUploadSlots.Text = config.TorrentUploadSlotsPerTorrent.ToString(CultureInfo.InvariantCulture);

            var encryption = Array.IndexOf(EncryptionModes, config.TorrentEncryption);
            CmbEncryption.SelectedIndex = encryption < 0 ? 0 : encryption;

            ChkDht.IsChecked = config.TorrentEnableDht;
            ChkPex.IsChecked = config.TorrentEnablePeerExchange;
            ChkLsd.IsChecked = config.TorrentEnableLocalPeerDiscovery;
            ChkPortForwarding.IsChecked = config.TorrentEnablePortForwarding;

            TxtSaveFolder.Text = config.TorrentSaveFolder;
            ChkCreateSubfolder.IsChecked = config.TorrentCreateSubfolder;
            ChkIncompleteExtension.IsChecked = config.TorrentAppendExtensionToIncompleteFiles;

            ChkSeeding.IsChecked = config.TorrentEnableSeeding;
            ChkSuperSeeding.IsChecked = config.TorrentEnableSuperSeeding;
            TxtSeedRatio.Text = config.TorrentSeedRatioLimit.ToString("0.##", CultureInfo.InvariantCulture);
            TxtSeedMinutes.Text = config.TorrentSeedTimeLimitMinutes.ToString(CultureInfo.InvariantCulture);

            TxtHalfOpen.Text = config.TorrentMaxHalfOpenConnections.ToString(CultureInfo.InvariantCulture);
            TxtOpenFiles.Text = config.TorrentMaxOpenFiles.ToString(CultureInfo.InvariantCulture);
            TxtDiskCache.Text = config.TorrentDiskCacheMiB.ToString(CultureInfo.InvariantCulture);
        }

        public void UpdateConfig()
        {
            var config = Config.Instance;

            // Anything unparseable keeps the value that was already there rather than
            // silently becoming zero, which for a port or a connection cap would be
            // a functional change the user never asked for.
            config.TorrentListenPort = ReadInt(TxtPort.Text, config.TorrentListenPort, 0, 65535);
            config.TorrentMaxDownloadRate = ReadInt(TxtMaxDownload.Text, config.TorrentMaxDownloadRate, 0, int.MaxValue);
            config.TorrentMaxUploadRate = ReadInt(TxtMaxUpload.Text, config.TorrentMaxUploadRate, 0, int.MaxValue);
            config.TorrentMaxConnections = ReadInt(TxtMaxConnections.Text, config.TorrentMaxConnections, 1, int.MaxValue);
            config.TorrentMaxConnectionsPerTorrent =
                ReadInt(TxtMaxConnectionsPerTorrent.Text, config.TorrentMaxConnectionsPerTorrent, 1, int.MaxValue);
            config.TorrentUploadSlotsPerTorrent =
                ReadInt(TxtUploadSlots.Text, config.TorrentUploadSlotsPerTorrent, 1, int.MaxValue);

            var encryption = CmbEncryption.SelectedIndex;
            config.TorrentEncryption = encryption >= 0 && encryption < EncryptionModes.Length
                ? EncryptionModes[encryption]
                : TorrentEncryptionMode.Prefer;

            config.TorrentEnableDht = ChkDht.IsChecked ?? true;
            config.TorrentEnablePeerExchange = ChkPex.IsChecked ?? true;
            config.TorrentEnableLocalPeerDiscovery = ChkLsd.IsChecked ?? true;
            config.TorrentEnablePortForwarding = ChkPortForwarding.IsChecked ?? true;

            config.TorrentSaveFolder = TxtSaveFolder.Text.Trim();
            config.TorrentCreateSubfolder = ChkCreateSubfolder.IsChecked ?? true;
            config.TorrentAppendExtensionToIncompleteFiles = ChkIncompleteExtension.IsChecked ?? false;

            config.TorrentEnableSeeding = ChkSeeding.IsChecked ?? true;
            config.TorrentEnableSuperSeeding = ChkSuperSeeding.IsChecked ?? false;
            config.TorrentSeedRatioLimit = ReadDouble(TxtSeedRatio.Text, config.TorrentSeedRatioLimit);
            config.TorrentSeedTimeLimitMinutes =
                ReadInt(TxtSeedMinutes.Text, config.TorrentSeedTimeLimitMinutes, 0, int.MaxValue);

            config.TorrentMaxHalfOpenConnections =
                ReadInt(TxtHalfOpen.Text, config.TorrentMaxHalfOpenConnections, 1, int.MaxValue);
            config.TorrentMaxOpenFiles = ReadInt(TxtOpenFiles.Text, config.TorrentMaxOpenFiles, 1, int.MaxValue);
            config.TorrentDiskCacheMiB = ReadInt(TxtDiskCache.Text, config.TorrentDiskCacheMiB, 0, int.MaxValue);
        }

        private static int ReadInt(string text, int fallback, int min, int max)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;

        private static double ReadDouble(string text, double fallback)
            => double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0
                ? value
                : fallback;

        private void BtnBrowseSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            using var folderBrowser = new WinForms.FolderBrowserDialog();
            if (folderBrowser.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtSaveFolder.Text = folderBrowser.SelectedPath;
            }
        }
    }
}
