using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TraceLog;

namespace Fluxo.Core.DataAccess
{
    /// <summary>
    /// Persistence for <see cref="DownloadGroup"/>.
    ///
    /// Deliberately mirrors <see cref="DownloadList"/>'s style: one prepared command
    /// per statement, every call taking the connection lock, and failures logged and
    /// swallowed so a database problem degrades the feature rather than killing the
    /// app mid-download.
    /// </summary>
    public class DownloadGroupList
    {
        private readonly SQLiteConnection db;

        private SQLiteCommand? cmdFetchAll, cmdInsert, cmdSetCompleted, cmdDelete;

        public DownloadGroupList(SQLiteConnection db)
        {
            this.db = db;
        }

        public List<DownloadGroup> LoadGroups()
        {
            lock (db)
            {
                var groups = new List<DownloadGroup>();
                try
                {
                    cmdFetchAll ??= new SQLiteCommand(
                        "SELECT id, name, date_added, source_url, targetdir, completed FROM download_groups", db);

                    using var r = cmdFetchAll.ExecuteReader();
                    while (r.Read())
                    {
                        groups.Add(new DownloadGroup
                        {
                            Id = r.IsDBNull(0) ? string.Empty : r.GetString(0),
                            Name = r.IsDBNull(1) ? string.Empty : r.GetString(1),
                            DateAdded = r.IsDBNull(2) ? DateTime.Now : DateTime.FromBinary(r.GetInt64(2)),
                            SourceUrl = r.IsDBNull(3) ? null : r.GetString(3),
                            TargetDir = r.IsDBNull(4) ? null : r.GetString(4),
                            Completed = !r.IsDBNull(5) && r.GetInt32(5) == 1
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "LoadGroups");
                }
                return groups;
            }
        }

        public bool AddGroup(DownloadGroup group)
        {
            lock (db)
            {
                try
                {
                    cmdInsert ??= new SQLiteCommand(
                        "INSERT INTO download_groups(id, name, date_added, source_url, targetdir, completed) " +
                        "VALUES(@id, @name, @date_added, @source_url, @targetdir, @completed)", db);

                    var p = cmdInsert.Parameters;
                    p.Clear();
                    p.AddWithValue("@id", group.Id);
                    p.AddWithValue("@name", group.Name);
                    p.AddWithValue("@date_added", group.DateAdded.ToBinary());
                    p.AddWithValue("@source_url", (object?)group.SourceUrl ?? DBNull.Value);
                    p.AddWithValue("@targetdir", (object?)group.TargetDir ?? DBNull.Value);
                    p.AddWithValue("@completed", group.Completed ? 1 : 0);

                    return cmdInsert.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "AddGroup");
                    return false;
                }
            }
        }

        public bool SetCompleted(string groupId, bool completed)
        {
            lock (db)
            {
                try
                {
                    cmdSetCompleted ??= new SQLiteCommand(
                        "UPDATE download_groups SET completed=@completed WHERE id=@id", db);

                    var p = cmdSetCompleted.Parameters;
                    p.Clear();
                    p.AddWithValue("@completed", completed ? 1 : 0);
                    p.AddWithValue("@id", groupId);

                    return cmdSetCompleted.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "SetCompleted");
                    return false;
                }
            }
        }

        public bool DeleteGroup(string groupId)
        {
            lock (db)
            {
                try
                {
                    cmdDelete ??= new SQLiteCommand("DELETE FROM download_groups WHERE id=@id", db);
                    var p = cmdDelete.Parameters;
                    p.Clear();
                    p.AddWithValue("@id", groupId);
                    return cmdDelete.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DeleteGroup");
                    return false;
                }
            }
        }
    }
}
