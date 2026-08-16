using System;
using System.Collections.Generic;
using Gtk;
using Fluxo.Core;
using Fluxo.Core.Util;

namespace Fluxo.GtkUI.Utils
{
    /// <summary>
    /// Hierarchy helpers for the in-progress download tree.
    ///
    /// GTK's TreeStore nests natively, so unlike the WPF side there is no need to
    /// flatten anything - a torrent is a real parent node with its files as real
    /// children. What is needed is the bookkeeping around it: finding a group's
    /// parent node, walking the whole tree rather than just its top level, and
    /// keeping the parent row's summary in step with its children.
    ///
    /// The column layout matches the store built in MainWindow:
    ///   0 name, 1 date, 2 size, 3 progress, 4 status, 5 the download item
    /// A parent row holds no download item, which is how a group is told apart
    /// from an ordinary download.
    /// </summary>
    internal static class DownloadTreeHelper
    {
        public const int ColName = 0;
        public const int ColDate = 1;
        public const int ColSize = 2;
        public const int ColProgress = 3;
        public const int ColStatus = 4;
        public const int ColData = 5;

        /// <summary>A parent row is the one with no download behind it.</summary>
        public static bool IsGroupRow(ITreeModel model, TreeIter iter)
            => model.GetValue(iter, ColData) is not InProgressDownloadItem;

        /// <summary>
        /// Walks every node, descending into children. The stock GetIterFirst plus
        /// IterNext pattern only visits the top level, which would miss every file
        /// inside a torrent.
        /// </summary>
        public static IEnumerable<TreeIter> WalkAll(ITreeModel model)
        {
            if (!model.GetIterFirst(out var iter))
            {
                yield break;
            }

            do
            {
                yield return iter;

                if (model.IterHasChild(iter) && model.IterChildren(out var child, iter))
                {
                    do
                    {
                        yield return child;
                    }
                    while (model.IterNext(ref child));
                }
            }
            while (model.IterNext(ref iter));
        }

        /// <summary>
        /// Finds the parent node for a group, creating it if this is the first file
        /// of that torrent to arrive.
        /// </summary>
        public static TreeIter GetOrCreateGroupRow(
            TreeStore store,
            Dictionary<string, TreeIter> groupRows,
            string groupId)
        {
            if (groupRows.TryGetValue(groupId, out var existing) && store.IterIsValid(existing))
            {
                return existing;
            }

            var group = DownloadGroupManager.Get(groupId);
            var iter = store.AppendValues(
                group?.Name ?? "Torrent",
                (group?.DateAdded ?? DateTime.Now).ToShortDateString(),
                string.Empty,
                0,
                string.Empty,
                null);

            groupRows[groupId] = iter;
            return iter;
        }

        /// <summary>
        /// Recomputes a parent's size, progress and status from its children, so the
        /// summary is always derived rather than stored.
        /// </summary>
        public static void RefreshGroupRow(TreeStore store, TreeIter groupIter)
        {
            if (!store.IterIsValid(groupIter) || !store.IterHasChild(groupIter))
            {
                return;
            }

            long totalSize = 0;
            long done = 0;
            var count = 0;
            var finished = 0;
            var progressSum = 0;
            var anyUnknownSize = false;
            var anyDownloading = false;

            if (store.IterChildren(out var child, groupIter))
            {
                do
                {
                    if (store.GetValue(child, ColData) is not InProgressDownloadItem item)
                    {
                        continue;
                    }

                    count++;
                    progressSum += item.Progress;
                    if (item.Progress >= 100) finished++;
                    if (item.Status == DownloadStatus.Downloading) anyDownloading = true;

                    if (item.Size > 0)
                    {
                        totalSize += item.Size;
                        done += (long)(item.Size * (item.Progress / 100.0));
                    }
                    else
                    {
                        anyUnknownSize = true;
                    }
                }
                while (store.IterNext(ref child));
            }

            if (count == 0)
            {
                return;
            }

            // Byte-weighting is only honest when every member's size is known;
            // otherwise fall back to averaging the percentages.
            var progress = anyUnknownSize || totalSize == 0
                ? progressSum / count
                : (int)(done * 100 / totalSize);

            var status = finished >= count
                ? $"{count} files"
                : $"{finished}/{count} files, {(anyDownloading ? "downloading" : "paused")}";

            store.SetValue(groupIter, ColSize, FormattingHelper.FormatSize(totalSize));
            store.SetValue(groupIter, ColProgress, progress);
            store.SetValue(groupIter, ColStatus, status);
        }

        /// <summary>
        /// Refreshes the parent of a row, if it has one. Called after a child's
        /// progress or status changes.
        /// </summary>
        public static void RefreshParentOf(TreeStore store, TreeIter childIter)
        {
            if (store.IterParent(out var parent, childIter))
            {
                RefreshGroupRow(store, parent);
            }
        }
    }
}
