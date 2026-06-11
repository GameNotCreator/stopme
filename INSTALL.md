# Installing StopMe on Windows

StopMe ships as three pieces:

| Component | Where it lives after install | What it does |
| --- | --- | --- |
| `StopMe.Service.exe` | `C:\Program Files\StopMe\Service\` | The Windows Service. Source of truth for active sessions and rule snapshots. |
| `StopMe.NativeHost.exe` | `C:\Program Files\StopMe\NativeHost\` | Stdio bridge between the Chromium extension and the service. |
| `StopMe.App.exe` *(optional)* | `C:\Program Files\StopMe\App\` | WinUI 3 desktop UI. |

The browser extension (`extension/chromium/`) is **not** packaged inside the
installer — load it unpacked or install it from a future Web Store listing.

---

## Prerequisites

* Windows 10 22H2+ / Windows 11 (x64).
* .NET 8 SDK on the *building* machine (`winget install Microsoft.DotNet.SDK.8`).
  Production machines only need the runtime (`winget install Microsoft.DotNet.AspNetCore.8`)
  — but if you publish with `--self-contained false` (the default in `publish.ps1`)
  the target machine needs the runtime too.
* Windows App SDK 2.x runtime if you also install the WinUI app
  (`winget install Microsoft.WindowsAppRuntime.1.6`).
* PowerShell 5.1 or 7 in an **elevated** prompt for the install / uninstall scripts.

---

## 1. Build a publish layout

From the repo root:

```powershell
pwsh deploy\publish.ps1                # builds Release into .\publish
pwsh deploy\publish.ps1 -SkipApp       # skip WinUI on headless dev boxes
```

This produces:

```
publish\
├── Service\        StopMe.Service.exe + deps
├── NativeHost\     StopMe.NativeHost.exe + deps
└── App\            StopMe.App.exe + deps  (when -SkipApp not passed)
```

---

## 2. Install

Open an **elevated** PowerShell prompt and run:

```powershell
pwsh deploy\install.ps1 -SourceDir .\publish `
    -ChromeExtensionId aaaabbbbccccddddeeeeffffgggghhhh `
    -EdgeExtensionId   iiiiijjjjjkkkkkllllllmmmmmnnnnno
```

What it does, in order:

1. Asserts elevation.
2. Copies the publish trees to `C:\Program Files\StopMe\{Service,NativeHost,App}`.
3. Creates `C:\ProgramData\StopMe\` and `C:\ProgramData\StopMe\Logs\`.
4. Tightens the ACL on the data directory (Administrators + SYSTEM full, Users read).
5. Stops + removes any prior `StopMeService`, then registers a fresh one with `start= auto`.
6. Starts the service.
7. Writes per-browser Native Messaging manifests + per-machine registry pointers
   under `HKLM:\Software\<browser>\NativeMessagingHosts\com.stopme.nativehost` for
   any of `-ChromeExtensionId / -EdgeExtensionId / -BraveExtensionId` you provided.

The first time the service starts it creates `auth.token` and applies a tighter
ACL via `TokenAclHardener` (Administrators + SYSTEM full, Users read-only).

### Common variations

```powershell
# Only register Native Messaging (e.g., you already installed the service):
pwsh deploy\install.ps1 -SourceDir .\publish -SkipService -ChromeExtensionId ...

# Only (re)install the service:
pwsh deploy\install.ps1 -SourceDir .\publish -SkipNativeHost

# Custom install / data directories:
pwsh deploy\install.ps1 -SourceDir .\publish -InstallDir "D:\Apps\StopMe" -DataDir "D:\StopMeData"
```

---

## 3. Verify

```powershell
pwsh deploy\check-install.ps1
```

Output sample:

```
[PASS] Windows service 'StopMeService' is running (StartType: Automatic).
[PASS] Service exe at C:\Program Files\StopMe\Service\StopMe.Service.exe
[PASS] NativeHost exe at C:\Program Files\StopMe\NativeHost\StopMe.NativeHost.exe
[PASS] Data dir present: C:\ProgramData\StopMe
[PASS] SQLite DB present: C:\ProgramData\StopMe\stopme.db
[PASS] Auth token present: C:\ProgramData\StopMe\auth.token
[PASS] Latest log: C:\ProgramData\StopMe\Logs\stopme-service-20260609.log
[PASS] Native host registered for chrome: C:\Program Files\StopMe\nativehost\com.stopme.nativehost.chrome.json
[WARN] Native host NOT registered for edge (HKLM:\Software\Microsoft\Edge\NativeMessagingHosts\com.stopme.nativehost).
[PASS] LocalApi pipe answered status (service version: 0.1.0).

OK: 0 required checks failed, 1 warning(s).
```

Exit code is `0` when every required check passes, `1` otherwise. Suitable for
post-install gates in deployment scripts.

---

## 4. Install the browser extension

Until the extension is published to the Web Store, load it unpacked:

1. Open `chrome://extensions/` (or `edge://extensions/`).
2. Toggle **Developer mode**.
3. Click **Load unpacked** and pick the repo's `extension/chromium/` folder.
4. Note the new 32-letter extension ID — re-run `install.ps1` with it if you
   skipped it the first time, or just rerun with `-SkipService`:
   ```powershell
   pwsh deploy\install.ps1 -SourceDir .\publish -SkipService -ChromeExtensionId <id>
   ```
5. Reload the extension. The popup should now show "Connected" and the active
   session name when one is running.

---

## 5. Uninstall

```powershell
pwsh deploy\uninstall.ps1                 # keep data
pwsh deploy\uninstall.ps1 -RemoveData     # wipe %ProgramData%\StopMe too
```

What it does:

1. Asserts elevation.
2. Talks to the local pipe and **refuses to uninstall while a strict session is
   active** unless `-Force` is passed.
3. Stops + deletes the `StopMeService`.
4. Removes the HKLM Native Messaging registry pointers (Chrome / Edge / Brave).
5. Deletes `C:\Program Files\StopMe`.
6. Deletes `C:\ProgramData\StopMe` only when `-RemoveData` is set.

---

## Where data lives

```
C:\ProgramData\StopMe\
├── stopme.db, stopme.db-wal, stopme.db-shm    SQLite (WAL mode)
├── auth.token                                  Bearer token for the local API
└── Logs\stopme-service-YYYYMMDD.log            Serilog, daily rotation, 14 files retained
```

Per-user UI settings (theme, data-dir override) live in
`%LocalAppData%\StopMe\user-settings.json` — they're never synced.

CSV exports from the Statistics page land in
`%UserProfile%\Documents\StopMe\Exports\`.

---

## Privacy guarantees

* No telemetry, ever.
* The Native Host receives URLs from the extension and forwards them to the
  local service — never to a remote endpoint.
* Stats are stored locally with one of three policies, selectable in the
  Statistics page or via `settings.set { key: "stats.privacy_mode" }`:
  * `Readable` — domains + app names stored both as readable label and as
    SHA-256 hash. Top-distractions chart shows real names.
  * `Anonymized` — only SHA-256 hashes persisted. Top distractions chart is
    empty by design.
  * `Disabled` — nothing written. The "Reset local statistics" button can wipe
    existing rows at any time.

---

## Troubleshooting

* **`check-install.ps1` reports the pipe is unreachable.** Service likely
  failed to start — open `C:\ProgramData\StopMe\Logs\stopme-service-*.log`.
* **Service stops with "Database is locked".** Another StopMe.Service process
  is running. `Get-Process StopMe.Service` to find and stop it.
* **Browser extension popup shows "Disconnected".** Either the Native
  Messaging registration is missing (run `check-install.ps1`), the extension
  ID changed (reload unpacked + re-register), or the service is down.
* **Strict session prevents you from uninstalling and there's no way out.**
  Use `pwsh deploy\uninstall.ps1 -Force`. The strict-mode lockout is a
  productivity feature, not a security mechanism — the spec is explicit that
  StopMe must remain uninstallable.
