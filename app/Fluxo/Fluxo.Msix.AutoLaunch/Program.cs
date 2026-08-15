using System;
using System.Diagnostics;
using System.IO;

namespace Fluxo.Msix.AutoLaunch
{
    static class Program
    {
        static void Main()
        {
            var psi = new ProcessStartInfo();
            psi.FileName = "fluxo-app.exe";
            psi.UseShellExecute = true;
            psi.Arguments = "--background";
            Process.Start(psi);
        }
    }
}
