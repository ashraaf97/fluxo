Building the Fluxo Windows installer
====================================

One-time setup
--------------
1. .NET 10 SDK        https://dotnet.microsoft.com/download/dotnet/10.0
2. WiX v5:
       dotnet tool install --global wix --version 5.*
       wix extension add --global WixToolset.UI.wixext/5.0.2

   WiX v5 is used rather than v6/v7 because those require accepting the Open
   Source Maintenance Fee EULA. WiX v3 (heat/candle/light) is no longer used;
   the file harvest is now done by the <Files> element in product.wxs.

Building
--------
    make-msi            ->  fluxosetup-<ver>.msi         (install wizard)
    make-msi silent     ->  fluxosetup-<ver>-silent.msi  (no wizard)

Set the version at the top of make-msi.bat (BUILD_VER). Keep it in step with
AppInfo.APP_VERSION.

What is in the package
----------------------
The app is published SELF-CONTAINED, so it carries its own .NET runtime and the
target machine needs nothing pre-installed. That is why the payload is ~165 MB
and the resulting MSI ~54 MB.

Also staged into the package:
  ..\chrome-extension   the browser integration extension
  ..\ext-loader         tiny helper that opens the browser's extension page

ffmpeg
------
ffmpeg is NOT redistributed in this repository. To bundle it, drop
ffmpeg-x86.exe next to make-msi.bat before building. Without it Fluxo falls back
to ffmpeg on PATH or the FFMPEG_HOME environment variable. See
..\FFmpegCustomBuild for how the custom build is produced.

Verifying without installing
----------------------------
    msiexec /a fluxosetup-<ver>.msi /qn TARGETDIR=C:\some\empty\dir

lays the payload out on disk without touching the registry or the system, which
is a quick way to confirm the MSI is intact.
