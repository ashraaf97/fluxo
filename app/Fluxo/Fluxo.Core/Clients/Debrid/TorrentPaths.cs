using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fluxo.Core.Util;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// Maps a torrent's internal layout onto local folders.
    ///
    /// Paths here come from a remote torrent and are therefore untrusted: every
    /// segment is sanitized and "." / ".." are dropped outright, so a crafted
    /// torrent cannot write outside the download folder.
    /// </summary>
    internal static class TorrentPaths
    {
        /// <summary>
        /// Where a multi-file torrent should be rooted.
        ///
        /// If every file already shares one top-level folder then the torrent's own
        /// name is redundant - the structure carries it, and prepending the name
        /// again would nest the download one level deeper than the torrent intends.
        /// Only when the files sit at the top level does the name become the folder.
        /// </summary>
        public static string RootFolderFor(DebridTorrent torrent)
        {
            var baseFolder = Config.Instance.DefaultDownloadFolder;
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                var sample = torrent.Files.Count > 0 ? torrent.Files[0].FileName : string.Empty;
                baseFolder = FileHelper.GetDownloadFolderByFileName(sample);
            }

            if (HasCommonRootFolder(torrent.Files))
            {
                return baseFolder;
            }

            var name = SanitizeSegment(torrent.Name);
            return string.IsNullOrEmpty(name) ? baseFolder : Path.Combine(baseFolder, name);
        }

        /// <summary>
        /// True when every file lives under the same single top-level folder.
        /// </summary>
        internal static bool HasCommonRootFolder(IList<DebridFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return false;
            }

            string? root = null;
            foreach (var file in files)
            {
                var path = file.Path ?? string.Empty;
                var slash = path.IndexOf('/');
                if (slash <= 0)
                {
                    // A file at the top level means there is no single shared root.
                    return false;
                }

                var segment = path.Substring(0, slash);
                if (root == null)
                {
                    root = segment;
                }
                else if (!string.Equals(root, segment, System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return !string.IsNullOrEmpty(root);
        }

        /// <summary>
        /// The directory part of a torrent-relative path, as a local relative path.
        /// Returns an empty string for a file at the top level.
        /// </summary>
        internal static string DirectoryOf(string relativePath)
        {
            var segments = SafeSegments(relativePath);
            if (segments.Count <= 1)
            {
                return string.Empty;
            }

            segments.RemoveAt(segments.Count - 1);
            return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
        }

        /// <summary>The file name part of a torrent-relative path.</summary>
        internal static string FileNameOf(string relativePath)
        {
            var segments = SafeSegments(relativePath);
            return segments.Count == 0 ? string.Empty : segments[segments.Count - 1];
        }

        /// <summary>
        /// Splits on '/', sanitizes each segment and discards anything that could
        /// escape the target folder.
        /// </summary>
        private static List<string> SafeSegments(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return new List<string>();
            }

            return relativePath
                .Split('/')
                .Select(SanitizeSegment)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        /// <summary>
        /// Makes one path segment safe to use as a folder or file name.
        /// FileHelper.SanitizeFileName replaces characters the filesystem rejects,
        /// but "." and ".." contain none of them, so they are dropped here.
        /// </summary>
        internal static string SanitizeSegment(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            var trimmed = segment.Trim();
            if (trimmed == "." || trimmed == "..")
            {
                return string.Empty;
            }

            var cleaned = FileHelper.SanitizeFileName(trimmed) ?? string.Empty;
            cleaned = cleaned.Replace('\\', '_').Trim().TrimEnd('.');

            return cleaned == "." || cleaned == ".." ? string.Empty : cleaned;
        }
    }
}
