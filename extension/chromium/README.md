# StopMe Browser Guard (Chromium MV3)

This is the Phase 4 browser extension. It intercepts top-frame navigations and asks the
**StopMe Native Messaging Host** (`com.stopme.nativehost`, see `src/StopMe.NativeHost/`)
whether the URL is blocked by the current active focus session. The native host forwards
the question to the **StopMe Windows Service** via the local Named Pipe API. Decisions are
local — no URL ever leaves the machine.

## Files

```
extension/chromium/
├── manifest.json              MV3, requires Chrome 108+
├── background.js              service-worker (type=module): webNavigation interception
├── nativeHost.js              connectNative wrapper, FIFO queue, auto-reconnect
├── css/base.css               shared palette mirroring the WinUI theme
├── blocked/                   blocked.html + css + js (full-page block screen)
├── popup/                     popup.html + css + js (toolbar popup: status + sessions)
├── icons/                     icon16/48/128 PNG generated from the StopMe palette
└── README.md                  (this file)
```

## Dev install

Prerequisites:
- StopMe service running (`dotnet run --project src/StopMe.Service`)
- `StopMe.NativeHost.exe` built (`dotnet publish src/StopMe.NativeHost -c Release -r win-x64 --self-contained false`)

Steps:

1. Open `chrome://extensions/` (or `edge://extensions/`).
2. Enable **Developer mode**.
3. Click **Load unpacked**, pick this directory.
4. **Copy the extension ID** that Chrome shows for the extension.
5. Register the native messaging host:

   ```powershell
   pwsh deploy\install-nativehost.ps1 `
     -ExePath "C:\path\to\StopMe.NativeHost.exe" `
     -ChromeExtensionId "<paste-id-from-step-4>" `
     -EdgeExtensionId "<id-if-using-edge>"
   ```

6. Open the extension popup. It should say **Connected**. If not, check
   `%ProgramData%\StopMe\Logs\stopme-nativehost-YYYYMMDD.log`.

## Protocol (extension ↔ native host)

Each message is a JSON object framed by Chrome's native messaging codec (4-byte little-endian
length + UTF-8 JSON). The extension keeps a single long-lived port; the native host processes
messages sequentially and replies in order.

```jsonc
// extension → host
{ "type": "CHECK_URL", "url": "https://youtube.com/watch?v=…", "tabId": 17, "browser": "chromium" }
{ "type": "GET_STATUS" }
{ "type": "GET_ACTIVE_SESSIONS" }

// host → extension
{ "type": "CHECK_URL_RESULT", "url": "…", "blocked": true, "reason": "…", "sessionName": "Focus", "remainingSeconds": 2400, "matchedRuleType": "Domain" }
{ "type": "STATUS", "connected": true, "serviceVersion": "0.1.0", "activeSessionCount": 1, "compiledRuleCount": 42 }
{ "type": "ACTIVE_SESSIONS", "sessions": [{ "id": "…", "name": "Focus", "mode": "Strict", "lockPolicy": "TimeLocked", "endAtUtc": "…", "remainingSeconds": 2400 }] }
{ "type": "ERROR", "errorCode": "service_unreachable", "errorMessage": "…" }
```

## Phase 4 known limitations

- **Sub-resources are not intercepted.** Only top-frame navigations (`webNavigation.onBeforeNavigate`,
  `frameId === 0`) are checked. Embedded iframes, XHR/fetch, and image/media requests pass
  through. Phase 5 will add `declarativeNetRequest` dynamic rules driven by the compiled
  rule snapshot.
- **Fail-open on connection error.** If the native host is unreachable, navigations are
  allowed by default. The user can see the disconnect state in the popup. Phase 5 will
  add the *unsupported browser auto-close* / *extension health check* behaviour from §14.
- **Single-shot redirect with 5-second cache.** The same blocked URL won't be re-checked
  for 5s after a redirect; this prevents loop-redirects when the blocked page itself
  navigates. Slightly stale until the cache decays.
- **Incognito mode** needs to be explicitly enabled in `chrome://extensions/` ("Allow in
  Incognito") — the manifest does not pre-authorise it; the user spec wants this to be
  consented to.
- **Firefox is out of scope.** Firefox extension is a separate Phase 4 task per §31 and
  uses `browser.*` APIs + a different `app.json` manifest layout.
