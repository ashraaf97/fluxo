using Fluxo.Core;
using Fluxo.Core.Util;

namespace Fluxo.Core.UI
{
    public static class DownloadCompleteUIController
    {
        public static void ShowDialog(IDownloadCompleteDialog dwnCmpldDlg, string file, string folder)
        {
            dwnCmpldDlg.FileNameText = file;
            dwnCmpldDlg.FolderText = folder;
            dwnCmpldDlg.FileOpenClicked += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Path))
                {
                    PlatformHelper.OpenFile(args.Path!);
                }
            };
            dwnCmpldDlg.FolderOpenClicked += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Path))
                {
                    PlatformHelper.OpenFolder(args.Path!, args.FileName);
                }
            };
            dwnCmpldDlg.DontShowAgainClickd += (sender, args) =>
            {
                Config.Instance.ShowDownloadCompleteWindow = false;
            };
            dwnCmpldDlg.ShowDownloadCompleteDialog();
        }
    }
}
