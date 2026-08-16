using System;
using System.Data.SQLite;
using System.IO;
using Fluxo.Core.DataAccess;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// The download list is the user's history, so the migration that adds
    /// downloads.group_id has to be provably non-destructive and repeatable.
    ///
    /// These build a database with the pre-migration schema, run the initializer
    /// over it, and check both that the column arrives and that existing rows are
    /// still readable at their original ordinals - DownloadList reads by position
    /// against SELECT *, so a column added anywhere but the end would corrupt reads.
    /// </summary>
    [TestFixture]
    public class SchemaMigrationTests
    {
        private string dbFile = string.Empty;

        /// <summary>The downloads table exactly as it was before group_id existed.</summary>
        private const string LegacySchema = @"CREATE TABLE IF NOT EXISTS downloads(
                id TEXT PRIMARY KEY, completed INT, name TEXT, date_added INT, size INT,
                status INT, progress INT, download_type TEXT, filenamefetchmode INT,
                maxspeedlimitinkib INT, targetdir TEXT, primary_url TEXT, referer_url TEXT,
                auth INT, user TEXT, pass TEXT, proxy INT, proxy_host TEXT, proxy_port INT,
                proxy_user TEXT, proxy_pass TEXT, proxy_type INT) WITHOUT ROWID";

        [SetUp]
        public void SetUp()
        {
            dbFile = Path.Combine(Path.GetTempPath(), $"fluxo-migration-{Guid.NewGuid():N}.db");
            SQLiteConnection.CreateFile(dbFile);
        }

        [TearDown]
        public void TearDown()
        {
            SQLiteConnection.ClearAllPools();
            try { if (File.Exists(dbFile)) File.Delete(dbFile); } catch { }
        }

        private SQLiteConnection OpenLegacyDbWithOneRow()
        {
            var db = new SQLiteConnection($"URI=file:{dbFile}");
            db.Open();

            using (var cmd = new SQLiteCommand(LegacySchema, db))
            {
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SQLiteCommand(
                "INSERT INTO downloads(id, completed, name, date_added, size, status, progress, " +
                "download_type, filenamefetchmode, maxspeedlimitinkib, targetdir, primary_url, " +
                "referer_url, auth, user, pass, proxy, proxy_host, proxy_port, proxy_user, " +
                "proxy_pass, proxy_type) VALUES('old-1', 0, 'existing.bin', 0, 123, 0, 42, " +
                "'Http', 0, 0, 'C:/dl', 'https://example.test/a', '', 0, '', '', 0, '', 0, '', '', 1)", db))
            {
                cmd.ExecuteNonQuery();
            }
            return db;
        }

        private static bool HasColumn(SQLiteConnection db, string table, string column)
        {
            using var pragma = new SQLiteCommand($"PRAGMA table_info({table})", db);
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

        [Test]
        public void Init_AddsGroupIdToALegacyDownloadsTable()
        {
            using var db = OpenLegacyDbWithOneRow();
            Assert.That(HasColumn(db, "downloads", "group_id"), Is.False, "precondition");

            SchemaInitializer.Init(db);

            Assert.That(HasColumn(db, "downloads", "group_id"), Is.True);
        }

        [Test]
        public void Init_PreservesExistingRows()
        {
            using var db = OpenLegacyDbWithOneRow();
            SchemaInitializer.Init(db);

            using var cmd = new SQLiteCommand("SELECT * FROM downloads WHERE id='old-1'", db);
            using var r = cmd.ExecuteReader();

            Assert.That(r.Read(), Is.True, "existing row should survive the migration");
            Assert.That(r.GetString(0), Is.EqualTo("old-1"));
            Assert.That(r.GetString(2), Is.EqualTo("existing.bin"));
            Assert.That(r.GetInt32(6), Is.EqualTo(42), "progress at its original ordinal");
            Assert.That(r.GetString(11), Is.EqualTo("https://example.test/a"), "url at its original ordinal");
        }

        [Test]
        public void Init_AppendsGroupIdLastSoOrdinalReadsStillLineUp()
        {
            using var db = OpenLegacyDbWithOneRow();
            SchemaInitializer.Init(db);

            using var cmd = new SQLiteCommand("SELECT * FROM downloads WHERE id='old-1'", db);
            using var r = cmd.ExecuteReader();
            Assert.That(r.Read(), Is.True);

            // 22 original columns at 0..21 (ending at proxy_type), so group_id is
            // index 22. Reading it at 21 would return proxy_type instead, handing
            // every download the same bogus group.
            Assert.That(r.FieldCount, Is.EqualTo(23));
            Assert.That(r.GetName(22), Is.EqualTo("group_id"));
            Assert.That(r.GetName(21), Is.EqualTo("proxy_type"));
            Assert.That(r.GetName(r.FieldCount - 1), Is.EqualTo("group_id"));
            Assert.That(r.IsDBNull(r.FieldCount - 1), Is.True, "legacy rows have no group");
        }

        [Test]
        public void Init_IsIdempotent()
        {
            using var db = OpenLegacyDbWithOneRow();

            SchemaInitializer.Init(db);
            Assert.DoesNotThrow(() => SchemaInitializer.Init(db),
                "running twice must not fail - it runs on every app start");

            using var cmd = new SQLiteCommand("SELECT COUNT(*) FROM downloads", db);
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));
        }

        [Test]
        public void Init_CreatesGroupsTableOnAFreshDatabase()
        {
            using var db = new SQLiteConnection($"URI=file:{dbFile}");
            db.Open();

            SchemaInitializer.Init(db);

            using var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='download_groups'", db);
            Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));
        }
    }
}
