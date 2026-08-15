using System;
using Fluxo.Core.Downloader.Progressive;
using Fluxo.Core.Downloader.Progressive.DualHttp;
using Fluxo.Core.Downloader.Progressive.SingleHttp;

namespace Fluxo.Core
{
    public interface ILinkRefresher
    {
        event EventHandler? RefreshedLinkReceived;

        void AddToWatchList(HTTPDownloaderBase downloader);
        void ClearWatchList();
        bool LinkAccepted(Message message);
        bool LinkAccepted(SingleSourceHTTPDownloadInfo info);
        bool LinkAccepted(DualSourceHTTPDownloadInfo info);
    }
}