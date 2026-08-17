using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Fluxo.Core.UI;
using Fluxo.Core;
using Fluxo.Core.Util;

namespace Fluxo.Wpf.UI
{
    internal class InProgressDownloadEntryWrapper : INotifyPropertyChanged, IInProgressDownloadRow
    {
        private readonly InProgressDownloadItem entry;

        public event PropertyChangedEventHandler PropertyChanged;

        public InProgressDownloadEntryWrapper(InProgressDownloadItem entry)
        {
            this.entry = entry;
        }

        public string Name
        {
            get { return entry.Name; }
            set
            {
                entry.Name = value;
                OnPropertyChanged("Name");
            }
        }

        public long Size
        {
            get { return entry.Size; }
            set
            {
                entry.Size = value;
                OnPropertyChanged("Size");
                Header?.RecomputeFromChildren();
            }
        }

        public DateTime DateAdded
        {
            get { return entry.DateAdded; }
            set
            {
                entry.DateAdded = value;
                OnPropertyChanged("DateAdded");
            }
        }

        public int Progress
        {
            get { return entry.Progress; }
            set
            {
                entry.Progress = value;
                OnPropertyChanged("Progress");
                OnPropertyChanged("Status");
                OnPropertyChanged("StatusText");
                Header?.RecomputeFromChildren();
            }
        }

        public string StatusText => IsGroupHeader ? GroupStatusText() : Helpers.GenerateStatusText(this.entry);

        public InProgressDownloadItem DownloadEntry => this.entry;

        public string FileIconText => IsGroupHeader
            ? "ri-folder-download-line"
            : IconMap.GetVectorNameForFileType(entry.Name);

        #region Grouping
        // A torrent is shown as one parent row with its files nested underneath.
        // The parent is an ordinary wrapper over a synthetic entry rather than a
        // separate type, so the collection stays homogeneous and every existing
        // code path over inProgressList keeps working unchanged.

        /// <summary>True when this row stands for a whole torrent.</summary>
        public bool IsGroupHeader { get; init; }

        /// <summary>Set on child rows; null on standalone downloads and on headers.</summary>
        public string? MemberOfGroupId { get; init; }

        /// <summary>
        /// The header this row rolls up into. Held directly so a member can refresh
        /// its parent's summary the moment its own progress moves, without the list
        /// having to poll or the group manager having to observe every row.
        /// </summary>
        public InProgressDownloadEntryWrapper? Header { get; init; }

        /// <summary>Children of a header row, in the order they were queued.</summary>
        public List<InProgressDownloadEntryWrapper> Children { get; } = new();

        private bool expanded;

        public bool IsExpanded
        {
            get => expanded;
            set
            {
                if (expanded == value) return;
                expanded = value;
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(ExpanderGlyph));
            }
        }

        /// <summary>Chevron direction; empty for rows that cannot expand.</summary>
        // Segoe MDL2: ChevronDown when open, ChevronRight when closed - the tree
        // convention, where the arrow points at what expanding would reveal.
        public string ExpanderGlyph => !IsGroupHeader
            ? string.Empty
            : (IsExpanded ? "" : "");

        public Visibility ExpanderVisibility => IsGroupHeader ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Indents member rows so the hierarchy reads at a glance.</summary>
        public Thickness RowIndent => MemberOfGroupId != null
            ? new Thickness(28, 0, 0, 0)
            : new Thickness(0);

        public FontWeight RowWeight => IsGroupHeader ? FontWeights.SemiBold : FontWeights.Normal;

        /// <summary>
        /// Recomputes the parent's size and progress from its children. Called as
        /// members advance, so the header is always a live summary rather than a
        /// stored value that can fall out of step.
        /// </summary>
        public void RecomputeFromChildren()
        {
            if (!IsGroupHeader || Children.Count == 0)
            {
                return;
            }

            long totalSize = 0;
            long done = 0;
            var anyUnknownSize = false;

            foreach (var child in Children)
            {
                var size = child.DownloadEntry.Size;
                if (size > 0)
                {
                    totalSize += size;
                    done += (long)(size * (child.DownloadEntry.Progress / 100.0));
                }
                else
                {
                    anyUnknownSize = true;
                }
            }

            entry.Size = totalSize;

            // With any member of unknown size a byte-weighted percentage would be a
            // lie, so fall back to averaging the per-file percentages.
            entry.Progress = anyUnknownSize || totalSize == 0
                ? (int)Children.Average(c => c.DownloadEntry.Progress)
                : (int)(done * 100 / totalSize);

            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
        }

        private string GroupStatusText()
        {
            if (Children.Count == 0)
            {
                return string.Empty;
            }

            var finished = Children.Count(c => c.DownloadEntry.Progress >= 100);
            if (finished >= Children.Count)
            {
                return $"{Children.Count} files";
            }

            var active = Children.Any(c => c.DownloadEntry.Status == DownloadStatus.Downloading);
            var label = active ? "downloading" : "paused";
            return $"{finished}/{Children.Count} files, {label}";
        }
        #endregion

        public DownloadStatus Status
        {
            get => entry.Status;
            set
            {
                entry.Status = value;
                OnPropertyChanged("Status");
                OnPropertyChanged("StatusText");
                Header?.RecomputeFromChildren();
            }
        }

        public string DownloadSpeed
        {
            get => entry.DownloadSpeed ?? string.Empty;
            set
            {
                entry.DownloadSpeed = value;
                OnPropertyChanged("Status");
                OnPropertyChanged("StatusText");
            }
        }

        public string ETA
        {
            get => entry.ETA ?? string.Empty;
            set
            {
                entry.ETA = value;
                OnPropertyChanged("Status");
                OnPropertyChanged("StatusText");
            }
        }

        /// <summary>
        /// Torrent only. Both stay empty for every other kind of download, which is
        /// what keeps the two extra columns unobtrusive when no torrent is running.
        /// </summary>
        public string UploadSpeed
        {
            get => entry.UploadSpeed ?? string.Empty;
            set
            {
                entry.UploadSpeed = value;
                OnPropertyChanged(nameof(UploadSpeed));
            }
        }

        public string Peers
        {
            get => entry.Peers ?? string.Empty;
            set
            {
                entry.Peers = value;
                OnPropertyChanged(nameof(Peers));
            }
        }

        /// <summary>
        /// This needs to be called after updating download speed or stopping the download
        /// </summary>
        public void UpdateStatusText()
        {
            OnPropertyChanged("Status");
            OnPropertyChanged("StatusText");
        }

        private void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
