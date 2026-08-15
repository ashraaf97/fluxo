using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Fluxo.Core.UI;
using Fluxo.Wpf.UI.Win32;

namespace Fluxo.Wpf.UI.Dialogs.SpeedLimiter
{
    /// <summary>
    /// Interaction logic for SpeedLimiterWindow.xaml
    /// </summary>
    public partial class SpeedLimiterWindow : Window, ISpeedLimiterWindow
    {
        public event EventHandler? OkClicked;

        public SpeedLimiterWindow()
        {
            InitializeComponent();
        }

        public int SpeedLimit
        {
            get => this.SpeedLimiter.SpeedLimit;
            set => this.SpeedLimiter.SpeedLimit = value;
        }

        public bool EnableSpeedLimit
        {
            get => this.SpeedLimiter.IsSpeedLimitEnabled;
            set => this.SpeedLimiter.IsSpeedLimitEnabled = value;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            OkClicked?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

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

        public void ShowWindow()
        {
            this.Show();
        }
    }
}
