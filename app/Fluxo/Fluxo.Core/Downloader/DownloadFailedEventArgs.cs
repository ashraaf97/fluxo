using System;
using Fluxo.Core;

namespace Fluxo.Core.Downloader
{
    public class DownloadFailedEventArgs : EventArgs
    {
        public DownloadFailedEventArgs(ErrorCode errorCode)
        {
            ErrorCode = errorCode;
        }
        public ErrorCode ErrorCode { get; }
    }
}
