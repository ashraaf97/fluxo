using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Fluxo.Core.UI;
using Fluxo.Wpf.UI.Win32;

namespace Fluxo.Wpf.UI.Dialogs.AddTorrent
{
    /// <summary>
    /// Interaction logic for AddTorrentWindow.xaml
    /// </summary>
    public partial class AddTorrentWindow : Window, IAddTorrentView
    {
        public AddTorrentWindow()
        {
            InitializeComponent();
        }

        public string Url { get => TxtUrl.Text; set => TxtUrl.Text = value; }

        public string StatusText { get => LblStatus.Text; set => LblStatus.Text = value; }

        public bool IsBusy
        {
            get => PrgBusy.Visibility == Visibility.Visible;
            set
            {
                PrgBusy.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                TxtUrl.IsEnabled = !value;
                BtnBrowse.IsEnabled = !value;
                BtnOK.IsEnabled = !value;
            }
        }

        public event EventHandler? OkClicked;
        public event EventHandler? CancelClicked;
        public event EventHandler? BrowseTorrentClicked;

        public void ShowWindow() => Show();

        public void DestroyWindow() => Close();

        public void RunOnUiThread(Action action) => Dispatcher.Invoke(action);

        private void BtnOK_Click(object sender, RoutedEventArgs e) => OkClicked?.Invoke(this, EventArgs.Empty);

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => CancelClicked?.Invoke(this, EventArgs.Empty);

        private void BtnBrowse_Click(object sender, RoutedEventArgs e) => BrowseTorrentClicked?.Invoke(this, EventArgs.Empty);

        private void Window_Closing(object sender, CancelEventArgs e) => CancelClicked?.Invoke(this, EventArgs.Empty);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.DisableMinMaxButton(this);
#if NET45_OR_GREATER || NET5_0_OR_GREATER
            if (Fluxo.Wpf.UI.App.Skin == Skin.Dark)
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                DarkModeHelper.UseImmersiveDarkMode(helper.Handle, true);
            }
#endif
        }
    }
}
