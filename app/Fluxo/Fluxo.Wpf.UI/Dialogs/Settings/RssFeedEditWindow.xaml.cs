using System;
using System.Windows;
using Fluxo.Core.Rss;
using Fluxo.Wpf.UI.Win32;
using Translations;

namespace Fluxo.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// Add or edit a single RSS feed subscription. A thin wrapper around two text
    /// boxes and an enable checkbox; the real work lives in the settings page that
    /// owns the feed list.
    /// </summary>
    public partial class RssFeedEditWindow : Window
    {
        public bool Result { get; private set; }

        public RssFeedEditWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.DisableMinMaxButton(this);
        }

        /// <summary>Populates the dialog from an existing feed, or leaves it empty.</summary>
        public void SetFeed(RssFeed? feed)
        {
            if (feed == null)
            {
                TxtUrl.Text = string.Empty;
                TxtName.Text = string.Empty;
                ChkEnabled.IsChecked = true;
                return;
            }

            TxtUrl.Text = feed.Url;
            TxtName.Text = feed.Name;
            ChkEnabled.IsChecked = feed.Enabled;
        }

        /// <summary>The feed as the dialog left it. Id is preserved when editing.</summary>
        public RssFeed GetFeed(RssFeed? original)
        {
            var feed = original ?? new RssFeed();
            feed.Url = TxtUrl.Text.Trim();
            feed.Name = TxtName.Text.Trim();
            feed.Enabled = ChkEnabled.IsChecked ?? true;
            return feed;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUrl.Text))
            {
                MessageBox.Show(TextResource.GetText("MSG_RSS_FEED_URL_MISSING"),
                    TextResource.GetText("RSS_FEED_TITLE"), MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUrl.Focus();
                return;
            }

            if (!Uri.TryCreate(TxtUrl.Text.Trim(), UriKind.Absolute, out _))
            {
                MessageBox.Show(TextResource.GetText("MSG_RSS_FEED_URL_INVALID"),
                    TextResource.GetText("RSS_FEED_TITLE"), MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUrl.Focus();
                return;
            }

            Result = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}