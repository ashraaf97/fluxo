using System;
using System.Collections;
using System.ComponentModel;

namespace Fluxo.Wpf.UI
{
    /// <summary>
    /// Sorts the in-progress list without breaking its hierarchy.
    ///
    /// A plain SortDescription would order every row independently, which scatters a
    /// torrent's files away from their header. This positions rows by their anchor -
    /// the header for a member, the row itself otherwise - and only then orders
    /// within a group, so a torrent always moves as one block.
    ///
    /// Used via ListCollectionView.CustomSort, which is the only sorting hook that
    /// can express this; SortDescriptions cannot.
    /// </summary>
    internal class GroupAwareRowComparer : IComparer
    {
        private readonly string field;
        private readonly ListSortDirection direction;
        private readonly DownloadGroupRows groups;

        public GroupAwareRowComparer(string field, ListSortDirection direction, DownloadGroupRows groups)
        {
            this.field = field;
            this.direction = direction;
            this.groups = groups;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not InProgressDownloadEntryWrapper a || y is not InProgressDownloadEntryWrapper b)
            {
                return 0;
            }

            var anchorA = groups.AnchorOf(a);
            var anchorB = groups.AnchorOf(b);

            if (!ReferenceEquals(anchorA, anchorB))
            {
                var result = CompareField(anchorA, anchorB);
                return direction == ListSortDirection.Descending ? -result : result;
            }

            // Same group: header first, then members in the order they were queued.
            // Deliberately not affected by direction - reversing a torrent's files
            // relative to their header would read as a glitch.
            return groups.OrderWithin(a).CompareTo(groups.OrderWithin(b));
        }

        private int CompareField(InProgressDownloadEntryWrapper a, InProgressDownloadEntryWrapper b)
        {
            switch (field)
            {
                case "Name":
                    return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                case "Size":
                    return a.Size.CompareTo(b.Size);
                case "Progress":
                    return a.Progress.CompareTo(b.Progress);
                case "Status":
                    return string.Compare(a.StatusText, b.StatusText, StringComparison.CurrentCultureIgnoreCase);
                case "DateAdded":
                default:
                    return a.DateAdded.CompareTo(b.DateAdded);
            }
        }
    }
}
