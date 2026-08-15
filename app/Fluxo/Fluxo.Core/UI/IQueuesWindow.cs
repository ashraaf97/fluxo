using System;
using System.Collections.Generic;
using Fluxo.Core;
using Fluxo.Core.UI;

namespace Fluxo.Core.UI
{
    public interface IQueuesWindow
    {
        event EventHandler<QueueListEventArgs>? QueuesModified;
        event EventHandler<DownloadListEventArgs>? QueueStartRequested;
        event EventHandler<DownloadListEventArgs>? QueueStopRequested;
        event EventHandler? WindowClosing;

        void RefreshView();
        void SetData(IEnumerable<DownloadQueue> queues);
        void ShowWindow(object window);

    }
}