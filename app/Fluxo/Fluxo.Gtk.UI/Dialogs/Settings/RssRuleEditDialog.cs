using System;
using System.Globalization;
using Gtk;
using IoPath = System.IO.Path;
using Fluxo.Core.Rss;
using Translations;
using UI = Gtk.Builder.ObjectAttribute;
using Fluxo.GtkUI.Utils;

namespace Fluxo.GtkUI.Dialogs.Settings
{
    /// <summary>
    /// Add or edit one auto-download rule. The GTK twin of
    /// <c>RssRuleEditWindow</c> on the WPF side.
    /// </summary>
    public class RssRuleEditDialog : Dialog
    {
        [UI] private Label LabelName, LabelMustContain, LabelMustNotContain, LabelExprHint,
            LabelIgnoreDays, LabelPriority, LabelSaveFolder;
        [UI] private Entry TxtName, TxtMustContain, TxtMustNotContain,
            TxtIgnoreDays, TxtPriority, TxtSaveFolder;
        [UI] private CheckButton ChkUseRegex, ChkSmartFilter;
        [UI] private Button BtnOk, BtnCancel, BtnBrowse;

        public bool Result { get; private set; }

        private WindowGroup group;
        private Window parent;

        private RssRuleEditDialog(Builder builder, Window parent, WindowGroup group)
            : base(builder.GetRawOwnedObject("dialog"))
        {
            builder.Autoconnect(this);

            Modal = true;
            SetPosition(WindowPosition.CenterAlways);
            TransientFor = parent;
            this.parent = parent;
            this.group = group;
            this.group.AddWindow(this);

            GtkHelper.AttachSafeDispose(this);

            LabelName.Text = TextResource.GetText("LBL_RSS_RULE_NAME");
            LabelMustContain.Text = TextResource.GetText("LBL_RSS_MUST_CONTAIN");
            LabelMustNotContain.Text = TextResource.GetText("LBL_RSS_MUST_NOT_CONTAIN");
            LabelExprHint.Text = TextResource.GetText("MSG_RSS_EXPRESSION_HINT");
            LabelIgnoreDays.Text = TextResource.GetText("LBL_RSS_IGNORE_DAYS");
            LabelPriority.Text = TextResource.GetText("LBL_RSS_PRIORITY");
            LabelSaveFolder.Text = TextResource.GetText("LBL_RSS_SAVE_FOLDER");

            ChkUseRegex.Label = TextResource.GetText("CHK_RSS_USE_REGEX");
            ChkSmartFilter.Label = TextResource.GetText("CHK_RSS_SMART_FILTER");

            BtnOk.Label = TextResource.GetText("MSG_OK");
            BtnCancel.Label = TextResource.GetText("ND_CANCEL");

            BtnOk.Clicked += BtnOk_Clicked;
            BtnCancel.Clicked += BtnCancel_Clicked;
            BtnBrowse.Clicked += BtnBrowse_Clicked;

            Title = TextResource.GetText("RSS_RULE_TITLE");
            SetDefaultSize(460, 460);
        }

        public void SetRule(RssRule? rule)
        {
            if (rule == null)
            {
                TxtName.Text = string.Empty;
                TxtMustContain.Text = string.Empty;
                TxtMustNotContain.Text = string.Empty;
                ChkUseRegex.Active = false;
                ChkSmartFilter.Active = false;
                TxtIgnoreDays.Text = "0";
                TxtPriority.Text = "0";
                TxtSaveFolder.Text = string.Empty;
                return;
            }

            TxtName.Text = rule.Name;
            TxtMustContain.Text = rule.MustContain;
            TxtMustNotContain.Text = rule.MustNotContain;
            ChkUseRegex.Active = rule.UseRegex;
            ChkSmartFilter.Active = rule.UseSmartFilter;
            TxtIgnoreDays.Text = rule.IgnoreDays.ToString(CultureInfo.InvariantCulture);
            TxtPriority.Text = rule.Priority.ToString(CultureInfo.InvariantCulture);
            TxtSaveFolder.Text = rule.SaveFolder;
        }

        public RssRule GetRule(RssRule? original)
        {
            var rule = original ?? new RssRule();
            rule.Name = TxtName.Text.Trim();
            rule.MustContain = TxtMustContain.Text;
            rule.MustNotContain = TxtMustNotContain.Text;
            rule.UseRegex = ChkUseRegex.Active;
            rule.UseSmartFilter = ChkSmartFilter.Active;
            rule.IgnoreDays = ReadInt(TxtIgnoreDays.Text, rule.IgnoreDays, 0, int.MaxValue);
            rule.Priority = ReadInt(TxtPriority.Text, rule.Priority, 0, int.MaxValue);
            rule.SaveFolder = TxtSaveFolder.Text.Trim();
            return rule;
        }

        private void BtnOk_Clicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                GtkHelper.ShowMessageBox(this, TextResource.GetText("MSG_RSS_RULE_NAME_MISSING"));
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

        private void BtnBrowse_Clicked(object? sender, EventArgs e)
        {
            var folder = GtkHelper.SelectFolder(this.parent);
            if (!string.IsNullOrEmpty(folder))
            {
                TxtSaveFolder.Text = folder;
            }
        }

        private static int ReadInt(string? text, int fallback, int min, int max)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;

        public static RssRuleEditDialog CreateFromGladeFile(Window parent, WindowGroup group)
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "rss-rule-edit-dialog.glade"));
            return new RssRuleEditDialog(builder, parent, group);
        }
    }
}