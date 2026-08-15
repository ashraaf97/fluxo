# Fluxo browser extensions

Fluxo uses a browser extension to take over downloads and to detect streaming
video. The extensions are **not published to any store yet**, so they have to be
loaded manually.

The extension talks to the running Fluxo app over HTTP on `127.0.0.1:8597`, so
the app must be running for the extension to show as connected.

## Chrome, Edge, Brave, Opera and other Chromium browsers

The Chromium extension is Manifest V3 and lives in
[`app/Fluxo/chrome-extension`](../app/Fluxo/chrome-extension).

1. Open `chrome://extensions` (or `edge://extensions`, `brave://extensions`, …).
2. Turn on **Developer mode**.
3. Choose **Load unpacked** and select the `app/Fluxo/chrome-extension` folder.
4. Start Fluxo. The extension icon should stop showing the disconnected state.

## Firefox

The Firefox extension is Manifest V3 and lives in
[`app/Fluxo/firefox-amo`](../app/Fluxo/firefox-amo). It requires **Firefox 115
or newer**.

Because it is unsigned, a normal Firefox release build will not install it
permanently. Either load it temporarily, or use Developer Edition / Nightly.

Temporary install (cleared when Firefox restarts):

1. Open `about:debugging#/runtime/this-firefox`.
2. Choose **Load Temporary Add-on…**.
3. Select `app/Fluxo/firefox-amo/manifest.json`.

Permanent install requires signing the add-on through
[addons.mozilla.org](https://addons.mozilla.org/developers/), or setting
`xpinstall.signatures.required` to `false` in `about:config` on Developer
Edition / Nightly only.

## Extension IDs

The Firefox add-on ID is declared in
`app/Fluxo/firefox-amo/manifest.json`:

```json
"browser_specific_settings": {
  "gecko": { "id": "fluxo-browser-helper@ashraaf97.github.io" }
}
```

Chromium does not let you choose an ID. One is derived from the folder path for
unpacked extensions, and assigned permanently when you publish to the Chrome Web
Store.

This only matters if native messaging is re-enabled. Native messaging is
currently **inactive** — every call site that would register the host is
commented out, and the extension uses the HTTP connector instead. If you do turn
it back on, replace the placeholder in
`app/Fluxo/Fluxo.App.Host/fluxo_chrome.native_host.json`:

```json
"allowed_origins": [
  "chrome-extension://REPLACE_WITH_YOUR_CHROME_EXTENSION_ID/"
]
```

with the ID shown for the extension on `chrome://extensions`.

## Publishing

Neither extension is in a store. To publish:

- **Chrome Web Store** — requires a one-time developer registration fee. After
  publishing, update the native messaging manifest with the assigned ID.
- **addons.mozilla.org** — free. Keep the `browser_specific_settings.gecko.id`
  stable across releases so updates are recognised as the same add-on.

Once listings exist, point the extension links in
`app/Fluxo/Fluxo.Core/Links.cs` at them instead of at this page.
