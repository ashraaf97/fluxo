using System;
using Gtk;
using IoPath = System.IO.Path;
using Fluxo.Core.Rss;
using Translations;
using UI = Gtk.Builder.ObjectAttribute;
using Fluxo.GtkUI.Utils;

namespace Fluxo.GtkUI.Dialogs.Settings
{
    /// <summary>
    /// Add or edit one RSS feed subscription. The GTK twin of
    /// <c>RssFeedEditWindow</c> on the WPF side.
    /// </summary>
    public class RssFeedEditDialog : Dialog
    {
        [UI] private Label LabelUrl, LabelName, LabelNameHint;
        [UI] private Entry TxtUrl, TxtName;
        [UI] private CheckButton ChkEnabled;
        [UI] private Button BtnOk, BtnCancel;

        public bool Result { get; private set; }

        private WindowGroup group;

        private RssFeedEditDialog(Builder builder, Window parent, WindowGroup group)
            : base(builder.GetRawOwnedObject("dialog"))
        {
            builder.Autoconnect(this);

            Modal = true;
            SetPosition(WindowPosition.CenterAlways);
            TransientFor = parent;
            this.group = group;
            this.group.AddWindow(this);

            GtkHelper.AttachSafeDispose(this);

            LabelUrl.Text = TextResource.GetText("LBL_RSS_FEED_URL");
            LabelName.Text = TextResource.GetText("LBL_RSS_FEED_NAME");
            LabelNameHint.Text = TextResource.GetText("MSG_RSS_FEED_NAME_HINT");
            ChkEnabled.Label = TextResource.GetText("CHK_RSS_FEED_ENABLED");

            BtnOk.Label = TextResource.GetText("MSG_OK");
            BtnCancel.Label = TextResource.GetText("ND_CANCEL");

            BtnOk.Clicked += BtnOk_Clicked;
            BtnCancel.Clicked += BtnCancel_Clicked;

            Title = TextResource.GetText("RSS_FEED_TITLE");
            SetDefaultSize(420, 220);
        }

        public void SetFeed(RssFeed? feed)
        {
            if (feed == null)
            {
                TxtUrl.Text = string.Empty;
                TxtName.Text = string.Empty;
                ChkEnabled.Active = true;
                return;
            }

            TxtUrl.Text = feed.Url;
            TxtName.Text = feed.Name;
            ChkEnabled.Active = feed.Enabled;
        }

        public RssFeed GetFeed(RssFeed? original)
        {
            var feed = original ?? new RssFeed();
            feed.Url = TxtUrl.Text.Trim();
            feed.Name = TxtName.Text.Trim();
            feed.Enabled = ChkEnabled.Active;
            return feed;
        }

        private void BtnOk_Clicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUrl.Text))
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_RSS_FEED_URL_MISSING"));
                return;
            }

            if (!Uri.TryCreate(TxtUrl.Text.Trim(), UriKind.Absolute, out _))
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_RSS_FEED_URL_INVALID"));
                return;
            }

            Result = true;
            this.group.RemoveWindow(this);
            Dispose();
        }

        private void BtnCancel_Clicked(object? sender, EventArgs e)
        {
            Result = false;
            this.group.RemoveWindow(this);
            Dispose();
        }

        public static RssFeedEditDialog CreateFromGladeFile(Window parent, WindowGroup group)
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "rss-feed-edit-dialog.glade"));
            return new RssFeedEditDialog(builder, parent, group);
        }
    }
}