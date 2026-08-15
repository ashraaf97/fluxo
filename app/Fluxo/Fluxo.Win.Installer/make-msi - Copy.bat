set BUILD_VER=8.0.18

DEL /s /q *.wixobj
DEL /s /q net4.6.0.wxs
RMDIR /S /Q BIN
RMDIR /S /Q fluxo-helper-chrome

MKDIR BIN
MKDIR BIN\chrome-extension
MKDIR BIN\ext-loader
MKDIR BIN\Fluxo.App.Host
MKDIR BIN\demo

dotnet build -c Release -f net4.6.0 ..\Fluxo.Wpf.UI\Fluxo.Wpf.UI.csproj -o BIN
dotnet build -c Release -f net4.6.0 ..\Fluxo.App.Host\Fluxo.App.Host.csproj -o BIN\Fluxo.App.Host

copy /B ffmpeg-x86.exe BIN
git clone https://github.com/subhra74/fluxo-helper-chrome.git

xcopy /E fluxo-helper-chrome\chrome\chrome-extension BIN\chrome-extension
xcopy /E fluxo-helper-chrome\ext-loader BIN\ext-loader

xcopy /E demo BIN\demo

heat dir BIN -o net4.6.0.wxs -scom -frag -srd -sreg -gg -cg NET460 -dr INSTALLFOLDER

candle product.wxs net4.6.0.wxs
light -ext WixUIExtension -ext WixUtilExtension -cultures:en-us product.wixobj net4.6.0.wixobj -b BIN -out fluxosetup-%BUILD_VER%-x86.msi

