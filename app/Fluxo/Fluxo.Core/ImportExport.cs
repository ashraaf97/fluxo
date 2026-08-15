using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Fluxo.Core;
using Fluxo.Core.DataAccess;

namespace Fluxo.Core
{
    internal class ImportExport
    {
        internal static void Import(string path)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            using FileStream zipToOpen = new(path, FileMode.Open);
            using ZipArchive archive = new(zipToOpen, ZipArchiveMode.Read);
            archive.ExtractToDirectory(tempDir);

            AppDB.Instance.Import(Path.Combine(tempDir, "downloads-export.db"));

            foreach (var file in Directory.GetFiles(tempDir, "*.state"))
            {
                File.Copy(file, Path.Combine(Config.DataDir, Path.GetFileName(file)));
            }

            foreach (var file in Directory.GetFiles(tempDir, "*.info"))
            {
                File.Copy(file, Path.Combine(Config.DataDir, Path.GetFileName(file)));
            }
        }

        internal static void Export(string path)
        {
            if (!path.EndsWith(".zip"))
            {
                path = $"{path}.zip";
            }
            var dir = new DirectoryInfo(Config.DataDir);
            var filesToAdd = new List<string>();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var dbFile = Path.Combine(tempDir, "downloads-export.db");
            AppDB.Instance.Export(dbFile);
            filesToAdd.Add(dbFile);
            filesToAdd.AddRange(dir.GetFiles("*.state").Select(x => x.FullName));
            filesToAdd.AddRange(dir.GetFiles("*.info").Select(x => x.FullName));

            using FileStream zipToCreate = new(path, FileMode.Create);
            using ZipArchive archive = new(zipToCreate, ZipArchiveMode.Create);
            foreach (var file in filesToAdd)
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }
    }
}
