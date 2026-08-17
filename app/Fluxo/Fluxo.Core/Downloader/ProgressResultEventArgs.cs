using System;

namespace Fluxo.Core.Downloader
{
    public class ProgressResultEventArgs : EventArgs
    {
        public int Progress { get; set; }
        public double DownloadSpeed { get; set; }
        public long Eta { get; set; }
        public long Downloaded { get; set; }

        /// <summary>
        /// True when the fields below carry meaning. Only a torrent sets it, so the
        /// HTTP downloaders are unaffected and the UI can tell "zero peers" apart
        /// from "not a torrent".
        /// </summary>
        public bool HasSwarmStats { get; set; }

        public double UploadSpeed { get; set; }

        public long Uploaded { get; set; }

        public int Seeds { get; set; }

        public int Peers { get; set; }
    }
}
