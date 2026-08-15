using System.Collections.Generic;

namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// A resolved torrent: the service's name for it plus every downloadable file
    /// inside, flattened out of the folder tree.
    ///
    /// The name matters because a multi-file torrent is saved into a folder of its
    /// own. Some torrents already carry a root folder in each file's
    /// <see cref="DebridFile.Path"/> and some list their files at the top level;
    /// the name is what gives the second kind somewhere to live.
    /// </summary>
    public class DebridTorrent
    {
        public string Name { get; set; } = string.Empty;

        public IList<DebridFile> Files { get; set; } = new List<DebridFile>();

        public int Count => Files.Count;
    }
}
