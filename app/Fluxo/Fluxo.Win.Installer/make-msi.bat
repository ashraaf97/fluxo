@echo off
REM Builds the Fluxo Windows installer.
REM
REM   make-msi          -> fluxosetup-<ver>.msi         (with install wizard)
REM   make-msi silent   -> fluxosetup-<ver>-silent.msi  (no wizard)
REM
REM Requirements:
REM   .NET 10 SDK    https://dotnet.microsoft.com/download/dotnet/10.0
REM   WiX v5         dotnet tool install --global wix --version 5.*
REM                  wix extension add --global WixToolset.UI.wixext/5.0.2
REM
REM The app is published self-contained, so the target machine needs no .NET
REM runtime installed. That is why the payload is ~165 MB and the MSI ~54 MB.

setlocal
set BUILD_VER=8.0.30
set RID=win-x86

if /I "%1"=="silent" (
    set WIXFLAGS=-d Silent=1
    set OUTPUT=fluxosetup-%BUILD_VER%-silent.msi
) else (
    set WIXFLAGS=
    set OUTPUT=fluxosetup-%BUILD_VER%.msi
)

echo === cleaning ===
if exist BIN rmdir /S /Q BIN
if exist "%OUTPUT%" del /q "%OUTPUT%"

echo === publishing self-contained %RID% ===
dotnet publish ..\Fluxo.Wpf.UI\Fluxo.Wpf.UI.csproj -c Release -r %RID% --self-contained true -o BIN
if errorlevel 1 goto :failed

echo === staging browser payloads ===
xcopy /E /I /Y ..\chrome-extension BIN\chrome-extension >nul
if errorlevel 1 goto :failed
xcopy /E /I /Y ..\ext-loader BIN\ext-loader >nul
if errorlevel 1 goto :failed

REM ffmpeg is not redistributed here. Drop ffmpeg-x86.exe beside this script to
REM bundle it; otherwise Fluxo falls back to ffmpeg on PATH or FFMPEG_HOME.
if exist ffmpeg-x86.exe copy /B /Y ffmpeg-x86.exe BIN >nul

echo === building %OUTPUT% ===
wix build product.wxs -d Version=%BUILD_VER% -d PublishDir=BIN %WIXFLAGS% ^
    -b BIN -arch x86 -ext WixToolset.UI.wixext -culture en-us -out "%OUTPUT%"
if errorlevel 1 goto :failed

echo.
echo Built %OUTPUT%
goto :eof

:failed
echo.
echo BUILD FAILED
exit /b 1
