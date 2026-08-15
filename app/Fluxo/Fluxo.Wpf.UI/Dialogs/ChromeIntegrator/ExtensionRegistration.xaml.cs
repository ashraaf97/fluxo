using System;
using System.Windows;
using Translations;
using Fluxo.Core.BrowserMonitoring;
using Fluxo.Core.Util;
using Fluxo.Wpf.UI.Win32;
using System.Windows.Interop;

namespace Fluxo.Wpf.UI.Dialogs.ChromeIntegrator
{
    /// <summary>
    /// Interaction logic for ExtensionRegistration.xaml
    /// </summary>
    public partial class ExtensionRegistration : Window
    {
        public ExtensionRegistration()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.DisableMinMaxButton(this);

#if NET45_OR_GREATER
            if (Fluxo.Wpf.UI.App.Skin == Skin.Dark)
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                DarkModeHelper.UseImmersiveDarkMode(helper.Handle, true);
            }
#endif
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtExtId.Text))
            {
                MessageBox.Show(this, TextResource.GetText("MSG_FIELD_BLANK"));
                return;
            }
            //ExtensionRegistrationHelper.AddExtension("chrome-extension://" + TxtExtId.Text);
            //NativeMessagingHostConfigurer.InstallNativeMessagingHostForWindows(Browser.Chrome);
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
