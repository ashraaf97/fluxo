using System;
using System.Collections.Generic;
using System.Linq;
using TraceLog;
using Fluxo.Core.DataAccess;

namespace Fluxo.Core
{
    /// <summary>
    /// Owns the grouping rules: which downloads belong together, what the parent row
    /// should read, and when a group counts as finished.
    ///
    /// Membership is held in the database (downloads.group_id) rather than in memory,
    /// so a group survives a restart mid-download. This type keeps a cache of the
    /// group records themselves purely to avoid a query per repaint.
    /// </summary>
    public static class DownloadGroupManager
    {
        private static readonly object sync = new();
        private static Dictionary<string, DownloadGroup>? cache;

        /// <summary>Raised when a group's last member finishes.</summary>
        public static event EventHandler<DownloadGroupEventArgs>? GroupCompleted;

        private static Dictionary<string, DownloadGroup> Cache
        {
            get
            {
                lock (sync)
                {
                    if (cache == null)
                    {
                        cache = new Dictionary<string, DownloadGroup>();
                        try
                        {
                            foreach (var g in AppDB.Instance.Groups.LoadGroups())
                            {
                                cache[g.Id] = g;
                            }
                        }
                        catch (Exception ex)
                        {
                            // A broken group table must not stop downloads working;
                            // the worst case is that rows show ungrouped.
                            Log.Debug(ex, "Failed to load download groups");
                        }
                    }
                    return cache;
                }
            }
        }

        public static DownloadGroup? Get(string? groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return null;
            }
            lock (sync)
            {
                return Cache.TryGetValue(groupId!, out var g) ? g : null;
            }
        }

        public static IEnumerable<DownloadGroup> All()
        {
            lock (sync)
            {
                return Cache.Values.ToList();
            }
        }

        public static DownloadGroup Create(string name, string? sourceUrl, string? targetDir)
        {
            var group = new DownloadGroup
            {
                Id = Guid.NewGuid().ToString(),
                Name = string.IsNullOrWhiteSpace(name) ? "Torrent" : name,
                DateAdded = DateTime.Now,
                SourceUrl = sourceUrl,
                TargetDir = targetDir,
                Completed = false
            };

            lock (sync)
            {
                Cache[group.Id] = group;
            }
            AppDB.Instance.Groups.AddGroup(group);
            Log.Debug($"Created download group '{group.Name}' ({group.Id})");
            return group;
        }

        public static void Remove(string groupId)
        {
            lock (sync)
            {
                Cache.Remove(groupId);
            }
            AppDB.Instance.Groups.DeleteGroup(groupId);
        }

        /// <summary>
        /// Called when any download finishes. If it belonged to a group and it was the
        /// last one outstanding, marks the group complete and raises
        /// <see cref="GroupCompleted"/>.
        ///
        /// Returns true when the finished download was part of a still-incomplete
        /// group, which is the signal to suppress the per-file completion popup.
        /// </summary>
        public static bool OnMemberFinished(string? groupId)
        {
            var group = Get(groupId);
            if (group == null)
            {
                return false;
            }

            var remaining = CountUnfinishedMembers(group.Id);
            if (remaining > 0)
            {
                Log.Debug($"Group '{group.Name}': {remaining} file(s) still downloading");
                return true;
            }

            if (!group.Completed)
            {
                group.Completed = true;
                AppDB.Instance.Groups.SetCompleted(group.Id, true);
                Log.Debug($"Group '{group.Name}' finished");
                GroupCompleted?.Invoke(null, new DownloadGroupEventArgs(group));
            }

            // Suppressed here too: the group-level popup stands in for the per-file one.
            return true;
        }

        /// <summary>
        /// How many members are still in the in-progress list. Read from the database
        /// rather than a counter so a restart mid-torrent cannot leave it wrong.
        /// </summary>
        public static int CountUnfinishedMembers(string groupId)
        {
            try
            {
                if (!AppDB.Instance.Downloads.LoadDownloads(out var inProgress, out _, QueryMode.InProgress))
                {
                    return 0;
                }
                return inProgress.Count(d => string.Equals(d.GroupId, groupId, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "CountUnfinishedMembers");
                return 0;
            }
        }

        /// <summary>Drops the cache so the next read reloads from the database.</summary>
        public static void Invalidate()
        {
            lock (sync)
            {
                cache = null;
            }
        }
    }

    public class DownloadGroupEventArgs : EventArgs
    {
        public DownloadGroupEventArgs(DownloadGroup group)
        {
            Group = group;
        }

        public DownloadGroup Group { get; }
    }
}
