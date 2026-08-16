<p id="downloads" align="center">
	<img src="app/Fluxo/fluxo-logo.png" height="120px"/>
	<h1 align="center">Fluxo</h1>
</p>

> **Fluxo** is a fork of [Xtreme Download Manager (XDM)](https://github.com/subhra74/xdm) by subhra74, renamed and maintained independently. All credit for the original design and engine goes to the upstream XDM project.

Fluxo is a download accelerator and video downloader. It splits downloads into parallel
segments to increase speed, saves video from streaming sites, resumes broken downloads,
and can queue and schedule transfers.

It runs on **Windows** (WPF) and **Linux** (GTK).

## Screenshots

The interface was rebuilt for Fluxo: a shared design language across both platforms,
a light theme that is a real theme rather than a fallback, and torrents shown as one
expandable row.

![Fluxo, dark theme](docs/screenshots/fluxo-dark.png)

A torrent downloads as a single entry — expand it to see the individual files, with the
parent summarising total size, combined progress and how many files are done.

| Light theme | Settings |
| --- | --- |
| ![Fluxo, light theme](docs/screenshots/fluxo-light.png) | ![Fluxo settings](docs/screenshots/fluxo-settings.png) |


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

For a torrent, Fluxo waits while AllDebrid caches it, then queues **every** file at once —
there is no file picker to work through. The files are saved into a folder mirroring the
torrent's own layout, so nested folders come out the same shape they went in.

In the download list the torrent appears as a single row that expands to show its files,
summarising total size, combined progress and how many files are done. Pausing, resuming
or deleting that row applies to the whole torrent. Only one "download complete" notice is
shown, once the last file lands.

**Add torrent / magnet** is disabled until an API key is set, since without one there is
nothing Fluxo can do with a torrent.

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

## Licence

GPL, see [LICENSE](LICENSE).


