using System.Collections.Generic;
using System.IO;
using Fluxo.Core.Clients.Debrid;
using NUnit.Framework;

namespace Fluxo.Tests
{
    /// <summary>
    /// Covers how a torrent's internal layout is mapped onto local folders.
    ///
    /// These paths arrive from a remote service and are untrusted, so the
    /// traversal cases matter as much as the happy path.
    /// </summary>
    [TestFixture]
    public class TorrentPathsTests
    {
        private static IList<DebridFile> Files(params string[] paths)
        {
            var list = new List<DebridFile>();
            foreach (var p in paths)
            {
                list.Add(new DebridFile { Path = p, RestrictedLink = "https://alldebrid.com/f/x" });
            }
            return list;
        }

        private static string Sep(string path) => path.Replace('/', Path.DirectorySeparatorChar);

        // ------------------------------------------------------------ structure

        [Test]
        public void DirectoryOf_ReturnsEmptyForTopLevelFile()
        {
            Assert.That(TorrentPaths.DirectoryOf("movie.mkv"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void DirectoryOf_KeepsNestedFolders()
        {
            Assert.That(TorrentPaths.DirectoryOf("Show/Season 1/ep1.mkv"),
                Is.EqualTo(Sep("Show/Season 1")));
        }

        [Test]
        public void FileNameOf_ReturnsLastSegment()
        {
            Assert.That(TorrentPaths.FileNameOf("Show/Season 1/ep1.mkv"), Is.EqualTo("ep1.mkv"));
            Assert.That(TorrentPaths.FileNameOf("movie.mkv"), Is.EqualTo("movie.mkv"));
        }

        // --------------------------------------------------------- common root

        [Test]
        public void HasCommonRootFolder_TrueWhenEveryFileSharesOneTopFolder()
        {
            Assert.That(TorrentPaths.HasCommonRootFolder(
                Files("Show/a.mkv", "Show/Season 1/b.mkv")), Is.True);
        }

        [Test]
        public void HasCommonRootFolder_FalseWhenAFileSitsAtTopLevel()
        {
            Assert.That(TorrentPaths.HasCommonRootFolder(
                Files("Show/a.mkv", "readme.txt")), Is.False);
        }

        [Test]
        public void HasCommonRootFolder_FalseWhenRootsDiffer()
        {
            Assert.That(TorrentPaths.HasCommonRootFolder(
                Files("Show A/a.mkv", "Show B/b.mkv")), Is.False);
        }

        // ------------------------------------------------------------ security

        [Test]
        public void SanitizeSegment_DropsTraversal()
        {
            Assert.That(TorrentPaths.SanitizeSegment(".."), Is.Empty);
            Assert.That(TorrentPaths.SanitizeSegment("."), Is.Empty);
            Assert.That(TorrentPaths.SanitizeSegment("  ..  "), Is.Empty);
        }

        [Test]
        public void DirectoryOf_StripsTraversalSegments()
        {
            // A crafted torrent must not be able to climb out of the download folder.
            var dir = TorrentPaths.DirectoryOf("../../etc/passwd");
            Assert.That(dir, Is.EqualTo("etc"));
        }

        [Test]
        public void DirectoryOf_NeutralisesBackslashSegments()
        {
            // Backslash is a separator on Windows but a legal file name character
            // on Linux, so it must not survive as one.
            var name = TorrentPaths.FileNameOf(@"a\..\b.mkv");
            Assert.That(name, Does.Not.Contain(".."));
            Assert.That(name, Does.Not.Contain("\\"));
        }

        [Test]
        public void FileNameOf_HandlesEmptyInput()
        {
            Assert.That(TorrentPaths.FileNameOf(string.Empty), Is.EqualTo(string.Empty));
        }
    }
}
