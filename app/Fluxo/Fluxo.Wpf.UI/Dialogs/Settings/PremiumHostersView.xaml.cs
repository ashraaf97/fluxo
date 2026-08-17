using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Fluxo.Core;
using Fluxo.Core.Clients.Debrid;
using Fluxo.Core.UI;

namespace Fluxo.Wpf.UI.Dialogs.Settings
{
    /// <summary>
    /// Interaction logic for PremiumHostersView.xaml
    /// </summary>
    public partial class PremiumHostersView : UserControl, ISettingsPage
    {
        /// <summary>
        /// One row of the ordering list. Displayed through ToString rather than a
        /// bound path, which keeps the type private without the binding engine
        /// having to reflect over it.
        /// </summary>
        private sealed class ProviderRow
        {
            public DebridProvider Provider { get; set; }
            public string Name { get; set; } = string.Empty;

            public override string ToString() => Name;
        }

        private readonly ObservableCollection<ProviderRow> order = new();

        public PremiumHostersView()
        {
            InitializeComponent();
            LstProviderOrder.ItemsSource = this.order;
        }

        public void PopulateUI()
        {
            TxtAllDebridApiKey.Text = Config.Instance.AllDebridApiKey;
            TxtRealDebridApiKey.Text = Config.Instance.RealDebridApiKey;

            this.order.Clear();
            foreach (var provider in DebridSupport.PreferredOrder())
            {
                this.order.Add(new ProviderRow
                {
                    Provider = provider,
                    Name = DebridSupport.DisplayName(provider)
                });
            }

            LstProviderOrder.SelectedIndex = 0;
            UpdateButtons();
        }

        public void UpdateConfig()
        {
            Config.Instance.AllDebridApiKey = TxtAllDebridApiKey.Text.Trim();
            Config.Instance.RealDebridApiKey = TxtRealDebridApiKey.Text.Trim();
            Config.Instance.DebridProviderOrder = this.order.Select(r => (int)r.Provider).ToArray();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e) => Move(1);

        private void Move(int offset)
        {
            var from = LstProviderOrder.SelectedIndex;
            var to = from + offset;
            if (from < 0 || to < 0 || to >= this.order.Count)
            {
                return;
            }

            this.order.Move(from, to);

            // Keep the moved row selected so the button can be clicked again.
            LstProviderOrder.SelectedIndex = to;
        }

        private void LstProviderOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateButtons();

        private void UpdateButtons()
        {
            var index = LstProviderOrder.SelectedIndex;
            BtnMoveUp.IsEnabled = index > 0;
            BtnMoveDown.IsEnabled = index >= 0 && index < this.order.Count - 1;
        }
    }
}
