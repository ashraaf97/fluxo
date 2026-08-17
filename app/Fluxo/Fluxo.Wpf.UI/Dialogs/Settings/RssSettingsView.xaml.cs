using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Core;
using Fluxo.Core.Rss;
using Fluxo.Core.UI;
using Fluxo.Wpf.UI.Win32;

namespace Fluxo.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// The RSS page of the settings dialog.
    ///
    /// Three sections: the on/off toggle and refresh interval, the feed list,
    /// and the rule list. Feeds and rules live in the <see cref="RssStore"/>,
    /// not in <see cref="Config"/>; the page reads them on populate and writes
    /// them back on save, so the rest of the time nothing holds them in memory.
    /// </summary>
    public partial class RssSettingsView : UserControl, ISettingsPage
    {
        private readonly ObservableCollection<RssFeed> feeds = new();
        private readonly ObservableCollection<RssRule> rules = new();
        private readonly RssStore store = new();

        public RssSettingsView()
        {
            InitializeComponent();
            LvFeeds.ItemsSource = this.feeds;
            LvRules.ItemsSource = this.rules;
        }

        public void PopulateUI()
        {
            var config = Config.Instance;
            ChkEnabled.IsChecked = config.RssEnabled;
            TxtRefreshMinutes.Text = config.RssRefreshMinutes.ToString(CultureInfo.InvariantCulture);
            TxtMaxArticles.Text = config.RssMaxArticlesPerFeed.ToString(CultureInfo.InvariantCulture);

            this.feeds.Clear();
            foreach (var feed in this.store.LoadFeeds())
            {
                this.feeds.Add(feed);
            }

            this.rules.Clear();
            foreach (var rule in this.store.LoadRules())
            {
                this.rules.Add(rule);
            }

            UpdateFeedButtons();
            UpdateRuleButtons();
            UpdateEmptyStates();
        }

        /// <summary>
        /// An empty list otherwise reads as a broken page rather than a starting
        /// point, and "no rules" in particular has a consequence worth stating:
        /// feeds still refresh, but nothing downloads.
        /// </summary>
        private void UpdateEmptyStates()
        {
            TxtNoFeeds.Visibility = this.feeds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtNoRules.Visibility = this.rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateConfig()
        {
            var config = Config.Instance;
            config.RssEnabled = ChkEnabled.IsChecked ?? false;
            config.RssRefreshMinutes = ReadInt(TxtRefreshMinutes.Text, config.RssRefreshMinutes, 1, 24 * 60);
            config.RssMaxArticlesPerFeed = ReadInt(TxtMaxArticles.Text, config.RssMaxArticlesPerFeed, 10, int.MaxValue);

            // A name typed in the dialog sticks, even if the feed has been
            // auto-named from its own title on a previous refresh.
            this.store.SaveFeeds(this.feeds);
            this.store.SaveRules(this.rules);
        }

        // --------------------------------------------------------------- feeds

        private void BtnFeedAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RssFeedEditWindow { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                this.feeds.Add(dlg.GetFeed(null));
                UpdateEmptyStates();
            }
        }

        /// <summary>Double-click opens the row, as it does elsewhere in the app.</summary>
        private void LvFeeds_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvFeeds.SelectedIndex >= 0)
            {
                BtnFeedEdit_Click(sender, e);
            }
        }

        private void LvRules_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvRules.SelectedIndex >= 0)
            {
                BtnRuleEdit_Click(sender, e);
            }
        }

        private void BtnFeedEdit_Click(object sender, RoutedEventArgs e)
        {
            var index = LvFeeds.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            var feed = this.feeds[index];
            var dlg = new RssFeedEditWindow { Owner = Window.GetWindow(this) };
            dlg.SetFeed(feed);
            if (dlg.ShowDialog() == true)
            {
                this.feeds[index] = dlg.GetFeed(feed);
            }
        }

        private void BtnFeedDelete_Click(object sender, RoutedEventArgs e)
        {
            var index = LvFeeds.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            var feed = this.feeds[index];
            this.feeds.RemoveAt(index);
            this.store.DeleteArticles(feed.Id);
            UpdateEmptyStates();
        }

        private void BtnFeedRefresh_Click(object sender, RoutedEventArgs e)
        {
            // Triggers a refresh on the live service, which is the same one
            // ApplicationCore owns. Off by default, so this is also how a user
            // who has not enabled RSS can still pull a feed on demand.
            ApplicationContext.CoreService.RefreshAllFeeds();
        }

        private void LvFeeds_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateFeedButtons();

        private void UpdateFeedButtons()
        {
            var has = LvFeeds.SelectedIndex >= 0;
            BtnFeedEdit.IsEnabled = has;
            BtnFeedDelete.IsEnabled = has;
        }

        // --------------------------------------------------------------- rules

        private void BtnRuleAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RssRuleEditWindow { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                this.rules.Add(dlg.GetRule(null));
                UpdateEmptyStates();
            }
        }

        private void BtnRuleEdit_Click(object sender, RoutedEventArgs e)
        {
            var index = LvRules.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            var rule = this.rules[index];
            var dlg = new RssRuleEditWindow { Owner = Window.GetWindow(this) };
            dlg.SetRule(rule);
            if (dlg.ShowDialog() == true)
            {
                this.rules[index] = dlg.GetRule(rule);
            }
        }

        private void BtnRuleDelete_Click(object sender, RoutedEventArgs e)
        {
            var index = LvRules.SelectedIndex;
            if (index >= 0)
            {
                this.rules.RemoveAt(index);
                UpdateEmptyStates();
            }
        }

        private void LvRules_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateRuleButtons();

        private void UpdateRuleButtons()
        {
            var has = LvRules.SelectedIndex >= 0;
            BtnRuleEdit.IsEnabled = has;
            BtnRuleDelete.IsEnabled = has;
        }

        private static int ReadInt(string text, int fallback, int min, int max)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;
    }
}