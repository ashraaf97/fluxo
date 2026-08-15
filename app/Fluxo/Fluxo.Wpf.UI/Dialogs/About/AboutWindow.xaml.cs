using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Fluxo.Core;
using Fluxo.Core.Util;
using Fluxo.Wpf.UI.Common;
using Fluxo.Wpf.UI.Win32;

namespace Fluxo.Wpf.UI.Dialogs.About
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window, IDialog
    {
        public AboutWindow()
        {
            InitializeComponent();
            this.TxtAppVersion.Text = AppInfo.APP_VERSION_TEXT;
            this.TxtCopyright.Text = AppInfo.APP_COPYRIGHT_TEXT;
            this.TxtWebsite.Text = AppInfo.APP_HOMEPAGE_TEXT;
            this.TxtOSInfo.Text = Environment.OSVersion.ToString();
            this.TxtNetFxInfo.Text = GetNetImageVersion();
            this.TxtMSIXInfo.Text = "App container: " + MsixHelper.IsAppContainer;
        }

        public bool Result { get; set; }

        private string GetNetImageVersion()
        {
            try
            {
#if NET35
                return Environment.Version.ToString();
#else
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes(true).OfType<TargetFrameworkAttribute>().First().FrameworkDisplayName;
#endif
            }
            catch
            {
                return Environment.Version.ToString();
            }

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

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlatformHelper.OpenBrowser(Links.HomePageUrl);
        }
    }
}
