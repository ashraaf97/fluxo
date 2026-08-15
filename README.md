<p id="downloads" align="center">
	<img src="https://i.stack.imgur.com/TOfqL.png" height="120px"/>
	<h1 align="center">Fluxo</h1>
</p>

> **Fluxo** is a fork of [Xtreme Download Manager (XDM)](https://github.com/subhra74/xdm) by subhra74, renamed and maintained independently. All credit for the original design and engine goes to the upstream XDM project.

Fluxo is a download accelerator and video downloader. It splits downloads into parallel
segments to increase speed, saves video from streaming sites, resumes broken downloads,
and can queue and schedule transfers.

It runs on **Windows** (WPF) and **Linux** (GTK).

## Screenshots

> These are still upstream XDM's screenshots. The interface is unchanged apart from the
> rename and the new Add Torrent dialog.

| ![fluxo_1][01] | ![fluxo_5][05] | ![fluxo_3][03] |
| --- | --- | --- |
| ![fluxo_7][07] | ![fluxo_6][06] | ![fluxo_9][09] |
| ![fluxo_4][04] | ![fluxo_2][02] |  |


## Features
- Downloads files in parallel segments, typically several times faster than a plain browser download.
- Saves video from numerous streaming sites.
- **Torrents, magnet links and premium hoster links via [AllDebrid](#torrents-via-alldebrid).**
- Built in video converter for MP3 and MP4 output.
- Supports `HTTP`, `HTTPS`, `FTP` and the streaming protocols `MPEG-DASH`, `Apple HLS` and `Adobe HDS`.
- Authentication, proxy servers, cookies and redirection, including NTLM and Kerberos.
- Clipboard monitoring, antivirus checking, scheduler, and system shutdown on completion.
- Resumes downloads broken by connection loss, power failure or an expired session.
- Browser integration for Chrome, Edge, Brave, Opera, Vivaldi, Firefox and other Chromium
  and Firefox based browsers — see [Browser integration](#browser-integration).

## Torrents via AllDebrid

Fluxo does not embed a BitTorrent client. Instead it hands torrents to
[AllDebrid](https://alldebrid.com), a paid service that downloads the torrent on its own
infrastructure and exposes the result over HTTPS. Fluxo then downloads those links
normally, so torrent files get the same segmented speed, resume and queueing as any other
download — and your machine never joins the swarm.

This requires an active AllDebrid subscription.

**Setup** — paste your API key from
[alldebrid.com/apikeys](https://alldebrid.com/apikeys/) into
**Settings → Advanced settings → AllDebrid API key**.

**Use** — toolbar **New → Add torrent / magnet**, then supply any of:

| Input | What happens |
| --- | --- |
| Magnet link | Submitted to AllDebrid, which fetches the torrent |
| `.torrent` file | Uploaded to AllDebrid (use **Browse…**) |
| Premium hoster link | Unlocked directly, no torrent involved |

For a torrent, Fluxo waits while AllDebrid caches it, then lists the contents so you can
tick the files you want. Each selected file becomes an ordinary download in the main list.

## Browser integration

The extensions are **not published to any store**, so they have to be loaded manually.
Full instructions, for both Chromium browsers and Firefox, are in
[docs/browser-extensions.md](docs/browser-extensions.md).

Both extensions are Manifest V3. The extension talks to the running app over
`127.0.0.1:8597`, so Fluxo must be running for it to connect.

## Building from source

Fluxo targets **.NET 10** — install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
cd app/Fluxo
dotnet build Fluxo.sln
```

Or open `app/Fluxo/Fluxo.sln` in Visual Studio.

| Project | Target | Platform |
| --- | --- | --- |
| `Fluxo.Wpf.UI` | `net10.0-windows` | Windows |
| `Fluxo.Gtk.UI` | `net10.0` | Linux |

Run the tests with:

```
dotnet test app/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj
```

### Packaging

- **Windows installer** — see [app/Fluxo/Fluxo.Win.Installer/ReadMe.txt](app/Fluxo/Fluxo.Win.Installer/ReadMe.txt).
  Needs WiX v5; produces a self-contained MSI, so users need no .NET runtime installed.
- **Linux packages** — `make-deb-pkg`, `make-rpm-pkg` and `make-arch-pkg` in
  `app/Fluxo/Fluxo.Linux.Installer/`.

ffmpeg is not redistributed in this repository. Fluxo finds it on `PATH` or via the
`FFMPEG_HOME` environment variable; see `app/Fluxo/FFmpegCustomBuild` for how upstream
builds it.

## Differences from upstream XDM

- Renamed throughout, with its own branding, update feed and issue tracker.
- Migrated from .NET Framework 4.x / .NET 5–6 to **.NET 10**.
- Added **torrent support through AllDebrid**.
- Firefox extension migrated from **Manifest V2 to V3**.
- TLS certificate validation is enforced. Upstream disabled it globally, which left every
  HTTPS download open to interception; it is now on by default, with an opt-out in
  settings for self-signed hosts.
- The build no longer clones any external repository, so packaging does not depend on
  another project staying online.

## Licence

GPL, inherited from XDM — see [LICENSE](LICENSE). The bundled `ext-loader` originates
from [xdm-helper-chrome](https://github.com/subhra74/xdm-helper-chrome) (GPLv3).


[//]: #ImageLinks
[01]: https://i.stack.imgur.com/s7ViA.jpg
[02]: https://i.stack.imgur.com/90TQO.jpg
[03]: https://i.stack.imgur.com/V5XF3.jpg
[04]: https://i.stack.imgur.com/aFyH5.png
[05]: https://i.stack.imgur.com/lmAr6.png
[06]: https://i.stack.imgur.com/H4yMj.png
[07]: https://i.stack.imgur.com/8ulBq.png
[08]: https://i.stack.imgur.com/Gfgae.jpg
[09]: https://i.stack.imgur.com/GlVDC.png
