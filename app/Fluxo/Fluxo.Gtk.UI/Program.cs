using System;
using Gtk;
using TraceLog;
using Translations;
using Fluxo.Core;
using Fluxo.Core.DataAccess;
using FluxoApp = Fluxo.Core.Application;
using System.Linq;
using Fluxo.Core.BrowserMonitoring;
using Fluxo.Core.Util;

namespace Fluxo.GtkUI
{
    class Program
    {
        private const string DisableCachingName = @"TestSwitch.LocalAppContext.DisableCaching";
        private const string DontEnableSchUseStrongCryptoName = @"Switch.System.Net.DontEnableSchUseStrongCrypto";

        static void Main(string[] args)
        {
            Config.LoadConfig();
            var debugMode = Environment.GetEnvironmentVariable("Fluxo_DEBUG_MODE");
            if (!string.IsNullOrEmpty(debugMode) && debugMode == "1")
            {
                var logFile = System.IO.Path.Combine(Config.AppDir, "log.txt");
                Log.InitFileBasedTrace(System.IO.Path.Combine(Config.AppDir, "log.txt"));
            }
            Log.Debug("Application_Startup");
            Environment.SetEnvironmentVariable("GTK_USE_PORTAL", "1");
            Gtk.Application.Init("fluxo-app", ref args);
            GLib.ExceptionManager.UnhandledException += ExceptionManager_UnhandledException;
            LoadStyleSheet();

            TlsHelper.ApplyDefaults();

            AppContext.SetSwitch(DisableCachingName, true);
            // Must stay false: setting this to true opts out of the strong crypto defaults.
            AppContext.SetSwitch(DontEnableSchUseStrongCryptoName, false);

            Log.Debug("Loading languages...");

            LoadLanguageTexts();

            // Only ask for the dark variant of whatever theme is in use. This used
            // to also force ThemeName = "Adwaita", which overrode the user's chosen
            // desktop theme outright.
            if (Config.Instance.AllowSystemDarkTheme)
            {
                Gtk.Settings.Default.ApplicationPreferDarkTheme = true;
            }

            var core = new ApplicationCore();
            var app = new FluxoApp();
            var win = new MainWindow();

            Log.Debug("Configuring app context...");

            ApplicationContext.FirstRunCallback += ApplicationContext_FirstRunCallback;
            ApplicationContext.Configurer()
                .RegisterApplicationWindow(win)
                .RegisterApplication(app)
                .RegisterApplicationCore(core)
                .RegisterCapturedVideoTracker(new VideoTracker())
                .RegisterClipboardMonitor(new ClipboardMonitor())
                .RegisterLinkRefresher(new LinkRefresher())
                .RegisterPlatformUIService(new GtkPlatformUIService())
                .Configure();

            Log.Debug("Processing arguments...");

            ArgsProcessor.Process(args);

            Log.Debug("Gtk Run...");

            Gtk.Application.Run();
        }

        private static void ApplicationContext_FirstRunCallback(object? sender, EventArgs e)
        {
            PlatformHelper.EnableAutoStart(true);
        }

        /// <summary>
        /// Applies styles/fluxo.css on top of the user's GTK theme.
        /// A missing or malformed stylesheet must never stop the app starting, so
        /// failures are logged and swallowed - the app just renders unstyled.
        /// </summary>
        private static void LoadStyleSheet()
        {
            try
            {
                var cssFile = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "styles", "fluxo.css");

                if (!System.IO.File.Exists(cssFile))
                {
                    Log.Debug($"Stylesheet not found at {cssFile}, using theme defaults");
                    return;
                }

                var provider = new CssProvider();
                // ParsingErrorArgs.Error is a raw GError pointer in GtkSharp 3.24, so
                // there is nothing safe to read off it here. GTK also writes the
                // details to stderr, which is enough to diagnose a bad rule.
                provider.ParsingError += (o, args) =>
                    Log.Debug("CSS parse error in fluxo.css (see stderr for details)");

                provider.LoadFromPath(cssFile);

                // Priority 600 (APPLICATION) rather than 800 (USER): this is the
                // app's own sheet, so a user override should still be able to win.
                Gtk.StyleContext.AddProviderForScreen(Gdk.Screen.Default, provider, 600);
                Log.Debug("Stylesheet loaded");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load stylesheet");
            }
        }

        private static void ExceptionManager_UnhandledException(GLib.UnhandledExceptionArgs args)
        {
            Log.Debug("GLib ExceptionManager_UnhandledException: " + args.ExceptionObject);
            args.ExitApplication = false;
        }

        private static void LoadLanguageTexts()
        {
            Log.Debug("Language loading ...");
            try
            {
                // Path.Combine with a literal "Lang\index.txt" produced "Lang\index.txt"
                // as a single segment on Linux, so the file was never found and the
                // user's language selection silently fell back to English.
                var indexFile = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Lang", "index.txt");
                if (System.IO.File.Exists(indexFile))
                {
                    var lines = System.IO.File.ReadAllLines(indexFile);
                    foreach (var line in lines)
                    {
                        var index = line.IndexOf("=");
                        if (index > 0)
                        {
                            var name = line.Substring(0, index);
                            var value = line.Substring(index + 1);
                            if (name == Config.Instance.Language)
                            {
                                TextResource.Load(value);
                                break;
                            }
                        }
                    }
                }
                Log.Debug("Language loaded.");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
            }
        }
    }
}
