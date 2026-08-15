using System;
using System.Collections.Generic;
using Fluxo.Core.Downloader;
using Fluxo.Core.Downloader.Adaptive.Dash;
using Fluxo.Core.Downloader.Adaptive.Hls;
using Fluxo.Core.Downloader.Progressive;
using Fluxo.Core.Downloader.Progressive.DualHttp;
using Fluxo.Core.Downloader.Progressive.SingleHttp;
using Fluxo.Core.UI;

namespace Fluxo.Core
{
    public interface IApplicationCore
    {
        public Version AppVerion { get; }
        public string AppPlatform { get; }

        public void AddDownload(Message message);

        public string? StartDownload(
            IRequestData info,
            string fileName,
            FileNameFetchMode fileNameFetchMode,
            string? targetFolder,
            bool startImmediately,
            AuthenticationInfo? authentication,
            ProxyInfo? proxyInfo,
            string? queueId,
            bool convertToMp3);

        public void StopDownloads(IEnumerable<string> list, bool closeProgressWindow = false);

        public void ResumeDownload(Dictionary<string, DownloadItemBase> list, bool nonInteractive = false);

        public void ResumeNonInteractiveDownloads(IEnumerable<string> idList);

        public bool IsDownloadActive(string id);

        public int ActiveDownloadCount { get; }

        public void RenameDownload(string id, string folder, string file);

        public AuthenticationInfo? PromptForCredential(string id, string message);

        public void RestartDownload(DownloadItemBase entry);

        public string? GetPrimaryUrl(DownloadItemBase entry);

        public void RemoveDownload(DownloadItemBase entry, bool deleteDownloadedFile, bool removeInfo = true);

        public void ShowProgressWindow(string downloadId);

        public void HideProgressWindow(string id);

        public void Export(string path);

        public void Import(string path);

        void AddBatchLinks(List<Message> messages);
    }
}
