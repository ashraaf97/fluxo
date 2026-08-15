using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace Fluxo.Wpf.UI
{
    /// <summary>
    /// Applies a <see cref="Skin"/> to the running application.
    ///
    /// Changing theme used to require an app restart: the palette was pulled in with
    /// StaticResource, which binds once. Control templates now reference colours with
    /// DynamicResource, so swapping the token dictionary underneath them is enough to
    /// repaint the whole UI in place.
    /// </summary>
    internal static class ThemeManager
    {
        /// <summary>Raised after the skin changes, so windows can refresh anything
        /// they draw imperatively rather than through resources.</summary>
        public static event EventHandler SkinChanged;

        public static void Apply(Skin skin)
        {
            if (App.Skin == skin)
            {
                // Still re-assert the title bars: a window opened before the last
                // change may never have had the attribute applied.
                ApplyWindowChrome(skin);
                return;
            }

            App.Skin = skin;

            var tokens = Application.Current?.Resources?.MergedDictionaries
                .OfType<SkinResourceDictionary>()
                .FirstOrDefault();

            // No dictionary means App.xaml was restructured without updating this.
            // Nothing sensible to fall back to, so leave the UI as it is.
            if (tokens == null)
            {
                return;
            }

            tokens.Refresh();
            ApplyWindowChrome(skin);
            SkinChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// The non-client area is drawn by the OS, not by WPF, so it does not follow
        /// the resource swap and has to be told separately.
        /// </summary>
        public static void ApplyWindowChrome(Skin skin)
        {
            if (Application.Current == null)
            {
                return;
            }

            foreach (Window window in Application.Current.Windows)
            {
                ApplyWindowChrome(window, skin);
            }
        }

        public static void ApplyWindowChrome(Window window, Skin skin)
        {
            if (window == null)
            {
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                // Not yet sourced; the window applies this itself on SourceInitialized.
                return;
            }

            DarkModeHelper.UseImmersiveDarkMode(handle, skin == Skin.Dark);
        }
    }
}
