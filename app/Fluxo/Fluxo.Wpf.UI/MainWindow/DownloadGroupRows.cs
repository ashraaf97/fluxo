using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Fluxo.Core;

namespace Fluxo.Wpf.UI
{
    /// <summary>
    /// Keeps the flat ObservableCollection behind the in-progress ListView looking
    /// like a tree.
    ///
    /// GridView has no hierarchy of its own, so instead of nesting containers the
    /// rows are kept flat and ordered: a header row immediately followed by its
    /// members. Collapsing removes the member rows from the collection and
    /// expanding puts them back, which is what gives the ListView something to
    /// virtualize and keeps sorting operating on rows it already understands.
    /// </summary>
    internal class DownloadGroupRows
    {
        private readonly ObservableCollection<InProgressDownloadEntryWrapper> rows;

        /// <summary>Header row per group id.</summary>
        private readonly Dictionary<string, InProgressDownloadEntryWrapper> headers = new();

        public DownloadGroupRows(ObservableCollection<InProgressDownloadEntryWrapper> rows)
        {
            this.rows = rows;
        }

        public void Clear() => headers.Clear();

        /// <summary>
        /// Adds a download, creating or reusing its group header when it belongs to
        /// one. Returns the row that was added for the download itself.
        /// </summary>
        public InProgressDownloadEntryWrapper Add(InProgressDownloadItem entry)
        {
            if (string.IsNullOrEmpty(entry.GroupId))
            {
                var plain = new InProgressDownloadEntryWrapper(entry);
                rows.Add(plain);
                return plain;
            }

            var header = GetOrCreateHeader(entry.GroupId!);
            var child = new InProgressDownloadEntryWrapper(entry)
            {
                MemberOfGroupId = entry.GroupId,
                Header = header
            };
            header.Children.Add(child);

            if (header.IsExpanded)
            {
                rows.Insert(LastRowIndexOf(header) + 1, child);
            }

            header.RecomputeFromChildren();
            return child;
        }

        /// <summary>
        /// Removes a row, and the header too if that was its last member - otherwise
        /// a finished torrent would leave an empty parent behind.
        /// </summary>
        public void Remove(InProgressDownloadEntryWrapper row)
        {
            rows.Remove(row);

            if (row.MemberOfGroupId == null ||
                !headers.TryGetValue(row.MemberOfGroupId, out var header))
            {
                return;
            }

            header.Children.Remove(row);
            if (header.Children.Count == 0)
            {
                rows.Remove(header);
                headers.Remove(row.MemberOfGroupId);
            }
            else
            {
                header.RecomputeFromChildren();
            }
        }

        public void Toggle(InProgressDownloadEntryWrapper header)
        {
            if (!header.IsGroupHeader)
            {
                return;
            }

            if (header.IsExpanded)
            {
                foreach (var child in header.Children)
                {
                    rows.Remove(child);
                }
                header.IsExpanded = false;
            }
            else
            {
                var at = rows.IndexOf(header);
                if (at < 0)
                {
                    return;
                }
                for (var i = 0; i < header.Children.Count; i++)
                {
                    rows.Insert(at + 1 + i, header.Children[i]);
                }
                header.IsExpanded = true;
            }
        }

        /// <summary>Refreshes the header summary for whichever group a row belongs to.</summary>
        public void RefreshHeaderFor(InProgressDownloadEntryWrapper row)
        {
            if (row.MemberOfGroupId != null && headers.TryGetValue(row.MemberOfGroupId, out var header))
            {
                header.RecomputeFromChildren();
            }
        }

        /// <summary>
        /// Every download row a user action should apply to. Acting on a header means
        /// acting on all of its members.
        /// </summary>
        public IEnumerable<InProgressDownloadEntryWrapper> Expand(
            IEnumerable<InProgressDownloadEntryWrapper> selection)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in selection)
            {
                if (row.IsGroupHeader)
                {
                    foreach (var child in row.Children)
                    {
                        if (seen.Add(child.DownloadEntry.Id))
                        {
                            yield return child;
                        }
                    }
                }
                else if (seen.Add(row.DownloadEntry.Id))
                {
                    yield return row;
                }
            }
        }

        /// <summary>
        /// The row a sort should position this one by: its header if it is a member,
        /// otherwise itself. Sorting on the anchor is what keeps a torrent's files
        /// travelling with their header instead of scattering across the list.
        /// </summary>
        public InProgressDownloadEntryWrapper AnchorOf(InProgressDownloadEntryWrapper row)
        {
            if (row.MemberOfGroupId != null && headers.TryGetValue(row.MemberOfGroupId, out var header))
            {
                return header;
            }
            return row;
        }

        /// <summary>
        /// Position within a group: the header first, then its members in the order
        /// they were queued. Only meaningful between rows sharing an anchor.
        /// </summary>
        public int OrderWithin(InProgressDownloadEntryWrapper row)
        {
            if (row.IsGroupHeader || row.MemberOfGroupId == null)
            {
                return 0;
            }
            return headers.TryGetValue(row.MemberOfGroupId, out var header)
                ? header.Children.IndexOf(row) + 1
                : 0;
        }

        private InProgressDownloadEntryWrapper GetOrCreateHeader(string groupId)
        {
            if (headers.TryGetValue(groupId, out var existing))
            {
                return existing;
            }

            var group = DownloadGroupManager.Get(groupId);
            var header = new InProgressDownloadEntryWrapper(new InProgressDownloadItem
            {
                Id = groupId,
                Name = group?.Name ?? "Torrent",
                DateAdded = group?.DateAdded ?? DateTime.Now,
                TargetDir = group?.TargetDir,
                Status = DownloadStatus.Downloading,
                DownloadType = "Group"
            })
            {
                IsGroupHeader = true,
                IsExpanded = true
            };

            headers[groupId] = header;
            rows.Add(header);
            return header;
        }

        /// <summary>
        /// Index of the last row belonging to a header, so an inserted member lands
        /// after its siblings rather than jumping to the front of the group.
        /// </summary>
        private int LastRowIndexOf(InProgressDownloadEntryWrapper header)
        {
            var index = rows.IndexOf(header);
            for (var i = index + 1; i < rows.Count; i++)
            {
                if (rows[i].MemberOfGroupId != header.DownloadEntry.Id)
                {
                    break;
                }
                index = i;
            }
            return index;
        }
    }
}
