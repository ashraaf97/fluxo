using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using TraceLog;

namespace Fluxo.Core.DataAccess
{
    public static class SchemaInitializer
    {
        private static void CreateTablesIfNotExists(SQLiteConnection c)
        {
            var query = @"CREATE TABLE IF NOT EXISTS downloads(
                                            id TEXT PRIMARY KEY,
                                            completed INT,
                                            name TEXT,
                                            date_added INT,
                                            size INT,
                                            status INT,
                                            progress INT,
                                            download_type TEXT,
                                            filenamefetchmode INT,
                                            maxspeedlimitinkib INT,
                                            targetdir TEXT,
                                            primary_url TEXT,
                                            referer_url TEXT,
                                            auth INT,
                                            user TEXT,
                                            pass TEXT,
                                            proxy INT,
                                            proxy_host TEXT,
                                            proxy_port INT,
                                            proxy_user TEXT,
                                            proxy_pass TEXT,
                                            proxy_type INT
                                        ) WITHOUT ROWID";
            using var cmd = new SQLiteCommand(c);
            cmd.CommandText = query;
            cmd.ExecuteNonQuery();

            // Groups of downloads shown as one expandable row, e.g. the files of a
            // torrent. Members point back via downloads.group_id.
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS download_groups(
                                            id TEXT PRIMARY KEY,
                                            name TEXT,
                                            date_added INT,
                                            source_url TEXT,
                                            targetdir TEXT,
                                            completed INT
                                        ) WITHOUT ROWID";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Brings an existing database up to date.
        ///
        /// There was no migration step before this, only CREATE TABLE IF NOT EXISTS,
        /// so a database created by an earlier build has a downloads table without
        /// group_id. SQLite has no ADD COLUMN IF NOT EXISTS, hence the pragma check.
        ///
        /// New columns must be APPENDED. DownloadList reads rows by ordinal against
        /// SELECT *, so inserting a column mid-table would silently shift every
        /// field after it.
        /// </summary>
        private static void Migrate(SQLiteConnection c)
        {
            AddColumnIfMissing(c, "downloads", "group_id", "TEXT");
        }

        private static void AddColumnIfMissing(SQLiteConnection c, string table, string column, string type)
        {
            if (HasColumn(c, table, column))
            {
                return;
            }

            Log.Debug($"Schema migration: adding {table}.{column}");
            using var alter = new SQLiteCommand($"ALTER TABLE {table} ADD COLUMN {column} {type}", c);
            alter.ExecuteNonQuery();
        }

        private static bool HasColumn(SQLiteConnection c, string table, string column)
        {
            // PRAGMA table_info yields one row per column, with the name at index 1.
            using var pragma = new SQLiteCommand($"PRAGMA table_info({table})", c);
            using var r = pragma.ExecuteReader();
            while (r.Read())
            {
                if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Init(SQLiteConnection c)
        {
            CreateTablesIfNotExists(c);
            Migrate(c);
        }
    }
}
