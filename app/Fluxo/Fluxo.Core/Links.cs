using System;
using System.Collections.Generic;
using System.Text;

namespace Fluxo.Core
{
    public static class Links
    {
        private const string RepoUrl = "https://github.com/ashraaf97/fluxo";
        private const string RepoApiUrl = "https://api.github.com/repos/ashraaf97/fluxo";

        public const string HomePageUrl = RepoUrl;
        public const string SupportUrl = RepoUrl + "/discussions";
        public const string IssueUrl = RepoUrl + "/issues";

        // Browser extensions are not published to any store yet. Point at the
        // install guide in the repo until store listings exist.
        public const string ChromeExtensionUrl = RepoUrl + "/blob/master/docs/browser-extensions.md";
        public const string FirefoxExtensionUrl = ChromeExtensionUrl;
        public const string OperaExtensionUrl = ChromeExtensionUrl;
        public const string EdgeExtensionUrl = ChromeExtensionUrl;
        public const string ManualExtensionInstallGuideUrl = ChromeExtensionUrl;

        public const string VideoDownloadTutorialUrl = RepoUrl + "/blob/master/docs/browser-extensions.md";
        public const string MediaGrabberHowToUrl = VideoDownloadTutorialUrl;
        public const string HelperToolsUrl = RepoUrl + "/releases/latest";

        // Queried as JSON by UpdateChecker.
        public const string AppLatestReleaseGH = RepoApiUrl + "/releases/latest";

        // Opened in the user's browser by AppUpdater.UpdatePage, so this has to be
        // a human readable page rather than the API endpoint.
        public const string AppUpdateCheckerUrl = RepoUrl + "/releases/latest";

        public const string YtDlpReleaseGH = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";

        // ffmpeg builds still come from the upstream XDM helper repo; this fork
        // does not publish its own.
        public const string FFmpegCustomReleaseGH = "https://api.github.com/repos/subhra74/xdm-ffmpeg-update/releases/latest";
    }
}
