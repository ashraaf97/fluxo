using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gtk;
using Fluxo.Core;
using Fluxo.Core.Rss;
using Fluxo.GtkUI.Utils;
using Translations;

namespace Fluxo.GtkUI.Dialogs.Settings
{
    /// <summary>
    /// The RSS page of the settings dialog. The GTK twin of
    /// <c>RssSettingsView</c> on the WPF side.
    ///
    /// Built in code, like <see cref="TorrentSettingsPage"/>, rather than in
    /// settings-dialog.glade: the page is a flat run of labelled fields and two
    /// list views, and expressing that as glade XML is several hundred lines
    /// where a mistyped packing position is invisible until the dialog opens on
    /// Linux - the one thing that cannot be checked from here.
    /// </summary>
    internal class RssSettingsPage
    {
        private readonly Window parent;
        private readonly WindowGroup group;

        private readonly CheckButton chkEnabled = new();
        private readonly Entry txtRefreshMinutes = new();
        private readonly Entry txtMaxArticles = new();

        private ListStore feedStore;
        private TreeView lvFeeds;
        private Button btnFeedAdd, btnFeedEdit, btnFeedDelete, btnFeedRefresh;

        private ListStore ruleStore;
        private TreeView lvRules;
        private Button btnRuleAdd, btnRuleEdit, btnRuleDelete;

        private readonly RssStore store = new();

        // The RssFeed/RssRule instances, held beside the list stores so an edit
        // can mutate the original in place rather than rebuilding the list.
        private readonly List<RssFeed> feeds = new();
        private readonly List<RssRule> rules = new();

        public RssSettingsPage(Window parent, WindowGroup group)
        {
            this.parent = parent;
            this.group = group;
            Widget = Build();

            feedStore = new ListStore(typeof(string), typeof(string), typeof(RssFeed));
            lvFeeds.Model = feedStore;
            AddTextColumn(lvFeeds, "LBL_RSS_FEED_NAME", 0);
            AddTextColumn(lvFeeds, "LBL_RSS_FEED_URL", 1);
            lvFeeds.Selection.Changed += (_, _) => UpdateFeedButtons();

            ruleStore = new ListStore(typeof(string), typeof(string), typeof(int), typeof(RssRule));
            lvRules.Model = ruleStore;
            AddTextColumn(lvRules, "LBL_RSS_RULE_NAME", 0);
            AddTextColumn(lvRules, "LBL_RSS_MUST_CONTAIN", 1);
            AddTextColumn(lvRules, "LBL_RSS_PRIORITY", 2);
            lvRules.Selection.Changed += (_, _) => UpdateRuleButtons();
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

            var heading = new Label(TextResource.GetText("SETTINGS_RSS")) { Halign = Align.Start };
            heading.StyleContext.AddClass("medium-font");
            box.PackStart(heading, false, true, 0);
            box.PackStart(Wrapped(TextResource.GetText("MSG_RSS_INTRO")), false, true, 0);

            chkEnabled.Label = TextResource.GetText("CHK_RSS_ENABLED");
            box.PackStart(chkEnabled, false, true, 0);

            box.PackStart(Caption("LBL_RSS_REFRESH"), false, true, 0);
            txtRefreshMinutes.WidthChars = 8;
            txtRefreshMinutes.Halign = Align.Start;
            box.PackStart(txtRefreshMinutes, false, true, 0);

            box.PackStart(Caption("LBL_RSS_MAX_ARTICLES"), false, true, 0);
            txtMaxArticles.WidthChars = 8;
            txtMaxArticles.Halign = Align.Start;
            box.PackStart(txtMaxArticles, false, true, 0);

            box.PackStart(Wrapped(TextResource.GetText("MSG_RSS_NEEDS_DEBRID")), false, true, 0);

            // Feeds
            Section(box, "LBL_RSS_FEEDS");
            lvFeeds = new TreeView { HeadersVisible = true, HeightRequest = 140 };
            box.PackStart(lvFeeds, true, true, 0);

            var feedButtons = new Box(Orientation.Horizontal, 5);
            btnFeedAdd = new Button(TextResource.GetText("SETTINGS_CAT_ADD"));
            btnFeedEdit = new Button(TextResource.GetText("SETTINGS_CAT_EDIT"));
            btnFeedDelete = new Button(TextResource.GetText("DESC_DEL"));
            btnFeedRefresh = new Button(TextResource.GetText("MSG_RSS_REFRESH_NOW"));
            feedButtons.PackStart(btnFeedAdd, false, true, 0);
            feedButtons.PackStart(btnFeedEdit, false, true, 0);
            feedButtons.PackStart(btnFeedDelete, false, true, 0);
            feedButtons.PackStart(btnFeedRefresh, false, true, 0);
            box.PackStart(feedButtons, false, true, 0);

            btnFeedAdd.Clicked += (_, _) => AddFeed();
            btnFeedEdit.Clicked += (_, _) => EditFeed();
            btnFeedDelete.Clicked += (_, _) => DeleteFeed();
            btnFeedRefresh.Clicked += (_, _) => ApplicationContext.CoreService.RefreshAllFeeds();

            // Rules
            Section(box, "LBL_RSS_RULES");
            lvRules = new TreeView { HeadersVisible = true, HeightRequest = 140 };
            box.PackStart(lvRules, true, true, 0);

            var ruleButtons = new Box(Orientation.Horizontal, 5);
            btnRuleAdd = new Button(TextResource.GetText("SETTINGS_CAT_ADD"));
            btnRuleEdit = new Button(TextResource.GetText("SETTINGS_CAT_EDIT"));
            btnRuleDelete = new Button(TextResource.GetText("DESC_DEL"));
            ruleButtons.PackStart(btnRuleAdd, false, true, 0);
            ruleButtons.PackStart(btnRuleEdit, false, true, 0);
            ruleButtons.PackStart(btnRuleDelete, false, true, 0);
            box.PackStart(ruleButtons, false, true, 0);

            btnRuleAdd.Clicked += (_, _) => AddRule();
            btnRuleEdit.Clicked += (_, _) => EditRule();
            btnRuleDelete.Clicked += (_, _) => DeleteRule();

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

        private static void AddTextColumn(TreeView view, string labelKey, int column)
        {
            var renderer = new CellRendererText();
            view.AppendColumn(new TreeViewColumn(TextResource.GetText(labelKey), renderer, "text", column)
            {
                Resizable = true,
                Sizing = TreeViewColumnSizing.Fixed,
                FixedWidth = 160
            });
        }

        // ------------------------------------------------------------- config

        public void LoadConfig()
        {
            var config = Config.Instance;
            chkEnabled.Active = config.RssEnabled;
            txtRefreshMinutes.Text = config.RssRefreshMinutes.ToString(CultureInfo.InvariantCulture);
            txtMaxArticles.Text = config.RssMaxArticlesPerFeed.ToString(CultureInfo.InvariantCulture);

            this.feeds.Clear();
            feedStore.Clear();
            foreach (var feed in this.store.LoadFeeds())
            {
                this.feeds.Add(feed);
                feedStore.AppendValues(feed.DisplayName, feed.Url, feed);
            }

            this.rules.Clear();
            ruleStore.Clear();
            foreach (var rule in this.store.LoadRules())
            {
                this.rules.Add(rule);
                ruleStore.AppendValues(rule.Name, rule.MustContain, rule.Priority, rule);
            }

            UpdateFeedButtons();
            UpdateRuleButtons();
        }

        public void UpdateConfig()
        {
            var config = Config.Instance;
            config.RssEnabled = chkEnabled.Active;
            config.RssRefreshMinutes = ReadInt(txtRefreshMinutes.Text, config.RssRefreshMinutes, 1, 24 * 60);
            config.RssMaxArticlesPerFeed = ReadInt(txtMaxArticles.Text, config.RssMaxArticlesPerFeed, 10, int.MaxValue);

            this.store.SaveFeeds(this.feeds);
            this.store.SaveRules(this.rules);
        }

        // --------------------------------------------------------------- feeds

        private void AddFeed()
        {
            using var dlg = RssFeedEditDialog.CreateFromGladeFile(this.parent, this.group);
            dlg.Run();
            dlg.Destroy();
            if (dlg.Result)
            {
                var feed = dlg.GetFeed(null);
                this.feeds.Add(feed);
                feedStore.AppendValues(feed.DisplayName, feed.Url, feed);
            }
        }

        private void EditFeed()
        {
            if (!lvFeeds.Selection.GetSelected(out TreeIter iter))
            {
                return;
            }

            var feed = (RssFeed)feedStore.GetValue(iter, 2);
            using var dlg = RssFeedEditDialog.CreateFromGladeFile(this.parent, this.group);
            dlg.SetFeed(feed);
            dlg.Run();
            dlg.Destroy();
            if (dlg.Result)
            {
                dlg.GetFeed(feed);
                feedStore.SetValues(iter, feed.DisplayName, feed.Url, feed);
            }
        }

        private void DeleteFeed()
        {
            if (!lvFeeds.Selection.GetSelected(out TreeIter iter))
            {
                return;
            }

            var feed = (RssFeed)feedStore.GetValue(iter, 2);
            this.feeds.Remove(feed);
            feedStore.Remove(ref iter);
            this.store.DeleteArticles(feed.Id);
        }

        private void UpdateFeedButtons()
        {
            var has = lvFeeds.Selection.GetSelected(out _);
            btnFeedEdit.Sensitive = has;
            btnFeedDelete.Sensitive = has;
        }

        // --------------------------------------------------------------- rules

        private void AddRule()
        {
            using var dlg = RssRuleEditDialog.CreateFromGladeFile(this.parent, this.group);
            dlg.Run();
            dlg.Destroy();
            if (dlg.Result)
            {
                var rule = dlg.GetRule(null);
                this.rules.Add(rule);
                ruleStore.AppendValues(rule.Name, rule.MustContain, rule.Priority, rule);
            }
        }

        private void EditRule()
        {
            if (!lvRules.Selection.GetSelected(out TreeIter iter))
            {
                return;
            }

            var rule = (RssRule)ruleStore.GetValue(iter, 3);
            using var dlg = RssRuleEditDialog.CreateFromGladeFile(this.parent, this.group);
            dlg.SetRule(rule);
            dlg.Run();
            dlg.Destroy();
            if (dlg.Result)
            {
                dlg.GetRule(rule);
                ruleStore.SetValues(iter, rule.Name, rule.MustContain, rule.Priority, rule);
            }
        }

        private void DeleteRule()
        {
            if (!lvRules.Selection.GetSelected(out TreeIter iter))
            {
                return;
            }

            var rule = (RssRule)ruleStore.GetValue(iter, 3);
            this.rules.Remove(rule);
            ruleStore.Remove(ref iter);
        }

        private void UpdateRuleButtons()
        {
            var has = lvRules.Selection.GetSelected(out _);
            btnRuleEdit.Sensitive = has;
            btnRuleDelete.Sensitive = has;
        }

        private static int ReadInt(string? text, int fallback, int min, int max)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;
    }
}