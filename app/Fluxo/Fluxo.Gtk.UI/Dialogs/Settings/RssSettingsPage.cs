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

            // One row rather than three stacked label/field pairs, which spent most
            // of the page's height on labels before either list got any.
            var settingsRow = new Box(Orientation.Horizontal, 12);
            chkEnabled.Label = TextResource.GetText("CHK_RSS_ENABLED");

            // The debrid note is a tooltip rather than a permanent paragraph: it is
            // read once, and the two lists need the height more.
            chkEnabled.TooltipText = TextResource.GetText("MSG_RSS_NEEDS_DEBRID");
            settingsRow.PackStart(chkEnabled, false, true, 0);

            txtRefreshMinutes.WidthChars = 5;
            settingsRow.PackStart(Caption("LBL_RSS_REFRESH"), false, true, 0);
            settingsRow.PackStart(txtRefreshMinutes, false, true, 0);

            txtMaxArticles.WidthChars = 5;
            settingsRow.PackStart(Caption("LBL_RSS_MAX_ARTICLES"), false, true, 0);
            settingsRow.PackStart(txtMaxArticles, false, true, 0);
            box.PackStart(settingsRow, false, true, 0);

            // Feeds
            btnFeedAdd = new Button(TextResource.GetText("SETTINGS_CAT_ADD"));
            btnFeedEdit = new Button(TextResource.GetText("SETTINGS_CAT_EDIT"));
            btnFeedDelete = new Button(TextResource.GetText("DESC_DEL"));
            btnFeedRefresh = new Button(TextResource.GetText("MSG_RSS_REFRESH_NOW"));
            Section(box, "LBL_RSS_FEEDS", btnFeedAdd, btnFeedEdit, btnFeedDelete, btnFeedRefresh);

            lvFeeds = new TreeView { HeadersVisible = true };
            box.PackStart(Scrolled(lvFeeds), true, true, 0);

            btnFeedAdd.Clicked += (_, _) => AddFeed();
            btnFeedEdit.Clicked += (_, _) => EditFeed();
            btnFeedDelete.Clicked += (_, _) => DeleteFeed();
            btnFeedRefresh.Clicked += (_, _) => ApplicationContext.CoreService.RefreshAllFeeds();

            // Rules
            btnRuleAdd = new Button(TextResource.GetText("SETTINGS_CAT_ADD"));
            btnRuleEdit = new Button(TextResource.GetText("SETTINGS_CAT_EDIT"));
            btnRuleDelete = new Button(TextResource.GetText("DESC_DEL"));
            Section(box, "LBL_RSS_RULES", btnRuleAdd, btnRuleEdit, btnRuleDelete);

            lvRules = new TreeView { HeadersVisible = true };
            box.PackStart(Scrolled(lvRules), true, true, 0);

            btnRuleAdd.Clicked += (_, _) => AddRule();
            btnRuleEdit.Clicked += (_, _) => EditRule();
            btnRuleDelete.Clicked += (_, _) => DeleteRule();

            // The page itself no longer scrolls: each list scrolls inside its own
            // frame and the two share whatever height is left, so the page cannot
            // end up scrolling a scrolling list.
            box.ShowAll();
            return box;
        }

        // ------------------------------------------------------------ builders

        /// <summary>
        /// A list in its own scrolling frame, sized by the space available rather
        /// than by a fixed request.
        /// </summary>
        private static Widget Scrolled(Widget child)
        {
            var scroller = new ScrolledWindow
            {
                Hexpand = true,
                Vexpand = true,
                ShadowType = ShadowType.In,
                MinContentHeight = 90
            };
            scroller.Add(child);
            return scroller;
        }

        /// <summary>
        /// A section heading with its actions on the same line, which saves the row
        /// a separate button strip would have taken from the list.
        /// </summary>
        private static void Section(Box box, string key, params Widget[] actions)
        {
            var row = new Box(Orientation.Horizontal, 5) { MarginTop = 8 };

            var label = new Label(TextResource.GetText(key)) { Halign = Align.Start };
            label.StyleContext.AddClass("medium-font");
            row.PackStart(label, false, true, 0);

            foreach (var action in actions)
            {
                row.PackEnd(action, false, true, 0);
            }

            box.PackStart(row, false, true, 0);
        }

        private static Label Caption(string key)
            => new Label(TextResource.GetText(key)) { Halign = Align.Start };

        private static Label Wrapped(string text)
            => new Label(text) { Halign = Align.Start, Wrap = true, Xalign = 0 };

        private static void AddTextColumn(TreeView view, string labelKey, int column)
        {
            var renderer = new CellRendererText();
            // Autosize rather than a fixed 160px: three fixed columns overflowed the
            // settings pane and clipped the last one out of sight.
            view.AppendColumn(new TreeViewColumn(TextResource.GetText(labelKey), renderer, "text", column)
            {
                Resizable = true,
                Expand = true,
                Sizing = TreeViewColumnSizing.Autosize
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