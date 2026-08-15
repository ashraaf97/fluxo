<p id="downloads" align="center">
	<img src="https://i.stack.imgur.com/TOfqL.png" height="120px"/>
	<h1 align="center">Fluxo</h1>
</p>

> **Fluxo** is a fork of [Xtreme Download Manager (XDM)](https://github.com/subhra74/xdm) by subhra74, renamed and maintained independently. All credit for the original design and engine goes to the upstream XDM project.

Fluxo is a powerful tool to increase download speeds up to 500%, save videos from popular video streaming websites, resume broken/dead downloads, schedule and convert downloads.<br>
Fluxo seamlessly integrates with Google Chrome, Mozilla Firefox Quantum, Opera, Vivaldi and other Chromium and Firefox based browsers, to take over downloads and saving streaming videos from web. Fluxo has a built in video converter which lets you convert your downloaded videos to different formats so that you can watch them on your mobile or TV (100+ devices are supported)

## Screenshots

| ![fluxo_1][01] | ![fluxo_5][05] | ![fluxo_3][03] |
| --- | --- | --- |
| ![fluxo_7][07] | ![fluxo_6][06] | ![fluxo_9][09] |
| ![fluxo_4][04] | ![fluxo_2][02] |  |


## Features
- Download files at maximum possible speed (5-6 times faster than conventional downloaders).
- Fluxo can save video from numerous video streaming sites.
- Works with all modern browsers on Windows, Linux and Mac OS X. Fluxo supports Google Chrome, Chromium, Firefox Quantum, Vivaldi, Edge and many other popular browsers.
- Fluxo has built in video converter, which lets you convert downloaded video to MP3 and MP4 formats.
- Supports `HTTP`, `HTTPS`, `FTP` as well as video streaming protocols like `MPEG-DASH`, `Apple HLS`, and `Adobe HDS`.
- Fluxo also supports authentication, proxy servers, cookies, redirection etc.
- Video download, clipboard monitoring, automatic antivirus checking, scheduler, system shutdown on download completion.
- Resumes broken / dead downloads caused by connection problem, power failure or session expiration.
- Works with Windows ISA, auto proxy scripts, proxy servers, NTLM, Kerberos authentication.

## Building from source
Fluxo targets **.NET 10**, so you need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
<pre>
cd app/Fluxo
dotnet build Fluxo.sln
</pre>
Or open <code>app/Fluxo/Fluxo.sln</code> in Visual Studio.

The Windows app (<code>Fluxo.Wpf.UI</code>) targets <code>net10.0-windows</code>; the GTK app
(<code>Fluxo.Gtk.UI</code>) targets <code>net10.0</code> and builds on Linux.


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
