namespace Fluxo.Core.Clients.Debrid
{
    /// <summary>
    /// One downloadable file inside a resolved torrent, flattened out of the
    /// service's folder tree.
    /// </summary>
    public class DebridFile
    {
        /// <summary>
        /// Path within the torrent, folders joined with '/'. For a single file
        /// torrent this is just the file name.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        public long Size { get; set; }

        /// <summary>
        /// The service's stable, restricted link. It is not directly downloadable;
        /// pass it through <see cref="IDebridService.UnlockLink"/> to obtain a
        /// direct URL. Direct URLs expire, this one does not, so this is the value
        /// worth holding on to.
        /// </summary>
        public string RestrictedLink { get; set; } = string.Empty;

        /// <summary>File name without the folder prefix.</summary>
        public string FileName
        {
            get
            {
                var i = Path.LastIndexOf('/');
                return i < 0 ? Path : Path.Substring(i + 1);
            }
        }
    }
}
