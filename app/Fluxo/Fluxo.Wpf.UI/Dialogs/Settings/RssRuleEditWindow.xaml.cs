using System;
using System.Globalization;
using System.Windows;
using Fluxo.Core.Rss;
using Fluxo.Wpf.UI.Win32;
using Translations;
using WinForms = System.Windows.Forms;

namespace Fluxo.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// Add or edit one auto-download rule. Rule fields are documented on
    /// <see cref="RssRule"/>; the dialog stays close to that shape and leaves
    /// validation to the rule's own defaults - an unreadable number keeps the
    /// value that was already there.
    /// </summary>
    public partial class RssRuleEditWindow : Window
    {
        public bool Result { get; private set; }

        public RssRuleEditWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.DisableMinMaxButton(this);
        }

        public void SetRule(RssRule? rule)
        {
            if (rule == null)
            {
                TxtName.Text = string.Empty;
                TxtMustContain.Text = string.Empty;
                TxtMustNotContain.Text = string.Empty;
                ChkUseRegex.IsChecked = false;
                ChkSmartFilter.IsChecked = false;
                TxtIgnoreDays.Text = "0";
                TxtPriority.Text = "0";
                TxtSaveFolder.Text = string.Empty;
                return;
            }

            TxtName.Text = rule.Name;
            TxtMustContain.Text = rule.MustContain;
            TxtMustNotContain.Text = rule.MustNotContain;
            ChkUseRegex.IsChecked = rule.UseRegex;
            ChkSmartFilter.IsChecked = rule.UseSmartFilter;
            TxtIgnoreDays.Text = rule.IgnoreDays.ToString(CultureInfo.InvariantCulture);
            TxtPriority.Text = rule.Priority.ToString(CultureInfo.InvariantCulture);
            TxtSaveFolder.Text = rule.SaveFolder;
        }

        /// <summary>Applies the dialog's values to an existing rule, or a new one.</summary>
        public RssRule GetRule(RssRule? original)
        {
            var rule = original ?? new RssRule();
            rule.Name = TxtName.Text.Trim();
            rule.MustContain = TxtMustContain.Text;
            rule.MustNotContain = TxtMustNotContain.Text;
            rule.UseRegex = ChkUseRegex.IsChecked ?? false;
            rule.UseSmartFilter = ChkSmartFilter.IsChecked ?? false;
            rule.IgnoreDays = ReadInt(TxtIgnoreDays.Text, rule.IgnoreDays, 0, int.MaxValue);
            rule.Priority = ReadInt(TxtPriority.Text, rule.Priority, 0, int.MaxValue);
            rule.SaveFolder = TxtSaveFolder.Text.Trim();
            return rule;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show(TextResource.GetText("MSG_RSS_RULE_NAME_MISSING"),
                    TextResource.GetText("RSS_RULE_TITLE"), MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
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

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var folderBrowser = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtSaveFolder.Text))
            {
                folderBrowser.SelectedPath = TxtSaveFolder.Text;
            }
            if (folderBrowser.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtSaveFolder.Text = folderBrowser.SelectedPath;
            }
        }

        private static int ReadInt(string text, int fallback, int min, int max)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;
    }
}