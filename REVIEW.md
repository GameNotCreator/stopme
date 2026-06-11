# StopMe — Technical Review

**Date:** 2026-06-10
**Reviewer:** Claude Code — deep functional audit (direct file reads + live build run)
**Focus:** End-to-end functional correctness, UX gaps, real blockers

---

## 1. Executive Summary

StopMe is architecturally coherent and more complete than the previous automated review suggested. All core engines (session, URL blocking, app blocking), the IPC layer, the extension pipeline, strict mode, statistics, and the installer are implemented and largely wired together correctly. Several findings from the prior review were wrong because the agent read stale binaries rather than current source.

**What is solid:**
- Clean layered architecture, enforced via project references. Core has zero infrastructure dependencies.
- The full session → rule compiler → service tick → process kill chain is wired end-to-end.
- `UrlRuleEngine` + `UrlMatcher` correctly handle domain matching (strips `www.`), wildcards (glob-to-regex), and exception rules.
- `PasswordHasher` uses PBKDF2-SHA512, 200 000 iterations, 16-byte random salt. The prior SHA-256 placeholder has been replaced.
- `TimeProvider` is injected everywhere — no direct `DateTime.UtcNow` in hot paths.
- All 7 UI pages have `Loaded += async (_, _) => await ViewModel.RefreshAsync()` — data reloads on navigation.
- 272 tests pass on the pre-built binaries (154 Core, 54 Service, 49 App.Core, 15 NativeHost).

**What is broken or incomplete:**

1. **Build is broken (CS0535).** `IBlocklistRepository.AppendRulesAsync` was added to the interface but two test fakes (`StubBlocklistRepository`, `FakeBlocklistRepository` in `StopMe.Core.Tests`) do not implement it. `dotnet build` fails; `dotnet test --no-build` passes only because the binaries predate the interface change.
2. **No onboarding.** The single biggest reason the app "doesn't work" for a new user: there is no guided setup flow. The service, the extension, and the NativeHost registration must all be in place before any feature is functional. None of this is automated or surfaced in the UI.
3. **ViewModel Transient lifetime.** Every tab switch re-creates the ViewModel. Checkbox selections, in-progress forms, and status messages are lost on navigation.
4. **Process list is unfiltered.** `BlockAppsPage` lists every non-system process (100+ entries on a typical Windows machine). Background services dominate; user-facing apps are hard to find.
5. **Website blocking UX requires DSL knowledge.** Adding URL rules requires a multi-line text area using an undocumented syntax. Not discoverable.
6. **Anti-bypass clock drift is record-only.** `AntiBypassRunner` logs a `BypassEvent.ClockDrift` and sets a flag but does not prevent `CompleteExpiredSessionsAsync` from ending the session when the clock has been advanced past `EndAtUtc`.
7. **`auth.token` is readable by `BUILTIN\Users`.** Both `install.ps1` and `TokenAclHardener` grant `ReadAndExecute` to all local users. Intentional (the WinUI app needs it), documented as a Phase 8 limitation, but a real security risk on shared machines.

**Maturity verdict: MVP-near, not usable without guided setup.**

The code is substantially complete. The gap between "code exists" and "product works" is primarily (a) the missing onboarding / installation guide in the UI, (b) the build break, and (c) the UX friction on the two core user flows.

---

## 2. Repository Overview

```
stopme/
├── CLAUDE.md                       — project instructions for Claude Code
├── INSTALL.md                      — manual installation walkthrough
├── README.md                       — architecture + build + known limitations
├── StopMe.sln                      — solution
├── Directory.Build.props           — global: warnings-as-errors (except StopMe.App), nullable
├── Directory.Packages.props        — central package versions
├── global.json                     — .NET 8 SDK pin
│
├── src/
│   ├── StopMe.Core/                — pure .NET 8: models, engines, abstractions, enums
│   ├── StopMe.Infrastructure/      — EF Core + SQLite repositories, migrations, seeder
│   ├── StopMe.LocalApi/            — IPC contract DTOs, pipe framing, API client
│   ├── StopMe.Service/             — Worker host, IPC server, process watcher, anti-bypass
│   ├── StopMe.App.Core/            — ViewModels, services (pure .NET 8, no WinUI)
│   ├── StopMe.App/                 — WinUI 3 shell: 8 pages, converters, theme resources
│   └── StopMe.NativeHost/          — Chrome native messaging stdio bridge
│
├── extension/chromium/             — MV3 extension: background.js, nativeHost.js, popup, blocked page
├── deploy/                         — install.ps1, uninstall.ps1, NativeHost manifests, publish.ps1
└── tests/
    ├── StopMe.Core.Tests/          — 154 tests (engines, recurrence, repositories)
    ├── StopMe.App.Core.Tests/      — 49 tests (ViewModels, services)
    ├── StopMe.Service.Tests/       — 54 tests (handlers, anti-bypass, process watcher)
    └── StopMe.NativeHost.Tests/    — 15 tests (messaging channel, dispatcher)
```

**Pages in StopMe.App (8):** Dashboard, Blocklists, BlockApps, ActiveSession, Planner, Templates, Statistics, Settings.

---

## 3. Build & Test Status

| Command | Result | Notes |
|---|---|---|
| `dotnet restore` | Pass | All packages resolved |
| `dotnet build --nologo` | **Fail** | CS0535 x2 in `StopMe.Core.Tests`; file-lock warnings are dev-only (service/app running) |
| `dotnet test --nologo --no-build` | Pass (272/272) | Runs against stale pre-built binaries; masks the CS0535 break |

**Build error detail:**

```
tests/StopMe.Core.Tests/Engines/PomodoroWiringTests.cs(197): error CS0535:
  'StubBlocklistRepository' does not implement 'IBlocklistRepository.AppendRulesAsync'

tests/StopMe.Core.Tests/Engines/TemplateServiceTests.cs(100): error CS0535:
  'FakeBlocklistRepository' does not implement 'IBlocklistRepository.AppendRulesAsync'
```

`IBlocklistRepository.AppendRulesAsync(Guid, IReadOnlyList<BlockRule>, DateTime, CancellationToken)` was added to `src/StopMe.Core/Abstractions/IBlocklistRepository.cs:21` to support `AddRuleHandler`, but neither test fake was updated. The fix is two stub implementations returning `Task.FromResult(0)`.

**Test suites (all pass on pre-built binaries):**

| Suite | Tests | Result |
|---|---|---|
| StopMe.Core.Tests | 154 | Pass |
| StopMe.App.Core.Tests | 49 | Pass |
| StopMe.Service.Tests | 54 | Pass |
| StopMe.NativeHost.Tests | 15 | Pass |
| **Total** | **272** | **Pass** |

---

## 4. Architecture Review

**Layer compliance:** correct. `StopMe.Core` has no infrastructure or UI imports. `StopMe.Infrastructure` implements Core abstractions. `StopMe.LocalApi` depends only on Core enum mirrors. `StopMe.Service` references Core + Infrastructure + LocalApi. `StopMe.App.Core` references Core + LocalApi, no WinUI. `StopMe.App` is the only WinUI surface. `StopMe.NativeHost` is standalone (Core + LocalApi).

**Service as source of truth:** enforced. The extension (`background.js:80-88`) never decides alone — every `check_url` call forwards to the NativeHost which forwards to the service. Fail-open (browser silently allows if NativeHost is unreachable) is explicitly documented in code comments.

**IPC:** named pipe with bearer token. `TokenAuthenticator` validates every request. `LocalApiDispatcher` routes to `IOperationHandler` instances (Scoped). All 25 operations are registered in `Program.cs`.

**DI wiring:**
- `ServiceConnection` is `Singleton` — correct (pipe lifecycle).
- All ViewModels are `Transient` — this is the critical bug (see §10).
- `TimeProvider.System` registered as `Singleton` in both App and Service.
- `IProcessProvider`, `IProcessTerminator`, `ProcessWatcher` are `Singleton` — correct.
- `ISessionEngine`, `IRuleCompiler`, `IUsageEventRecorder` are `Scoped` via `IServiceScopeFactory` in `ServiceWorker` — correct pattern.

**Coupling risks:**
- `ServiceWorker.TickAsync` is wrapped in `try/catch Exception` that swallows all non-cancellation errors with only a log line. Persistent DB errors would silently disable enforcement with no user-visible alert.
- `EvaluateUrlHandler` reads `_state.Current.CompiledRules` (in-memory snapshot) without a lock. `ServiceState.UpdateSnapshot` uses `Interlocked.Exchange`, making reads eventually consistent but never torn. Correct.

---

## 5. Phase-by-Phase Review

### Phase 1 — Core & Infrastructure

**Status:** Done

**What exists:**
- All domain models: `Blocklist`, `BlockRule`, `FocusSession`, `UserProfile`, `UsageEvent`, `BypassEvent`, `PausePolicy`, `SessionBlocklist`, `Device`, `Setting`, `CategoryDomain`
- Engines: `UrlRuleEngine`, `UrlMatcher`, `UrlNormalizer`, `AppRuleEngine`, `RuleCompiler`, `SessionEngine`, `StrictModeGuard`, `PasswordHasher`, `StatsAggregator`, `TemplateService`, `UsageEventRecorder`, `NullPauseEngine`
- Infrastructure: `StopMeDbContext`, 2 migrations (`InitialCreate`, `AddBypassEvents`), all repositories, `CategorySeeder`
- Abstractions: 18 interfaces

**Evidence in code:**
- `src/StopMe.Core/Engines/UrlRuleEngine.cs` — full priority-ordered evaluation (exceptions → allowlist → full-internet → blacklist)
- `src/StopMe.Core/Engines/UrlMatcher.cs` — domain matching with www-stripping, wildcard glob-to-regex, exact URL with path prefix
- `src/StopMe.Core/Engines/PasswordHasher.cs` — PBKDF2-SHA512, 200 000 iterations, 16-byte salt, legacy-hash detection
- `src/StopMe.Infrastructure/Persistence/Migrations/` — 2 migrations

**What is missing:**
- `AppendRulesAsync` stub missing in two test fakes (breaks build)
- `ISyncQueue` exists as Phase 8 stub with no implementation

**Issues / Risks:**
- The two missing stub implementations cause a clean-build failure. Stale pre-built binaries mask this in `--no-build` test runs.

**Recommended next actions:**
1. Add `Task<int> AppendRulesAsync(...) => Task.FromResult(0);` to both test fakes. (~15 min)

---

### Phase 2 — Windows Service & IPC

**Status:** Done

**What exists:**
- `ServiceWorker.cs` — Worker host with tick loop, `BootRestorer`, `AntiBypassRunner`, `ProcessWatcher`
- 25 `IOperationHandler` implementations covering all CRUD operations + evaluate + stats + settings + templates + process snapshot
- `LocalApiServer.cs` + `LocalApiHostedService.cs` — named pipe server
- `TokenAuthenticator.cs` — bearer token auth on every request
- `ServiceControl.cs` — install/uninstall/start/stop via `sc.exe`
- `ServiceOptions.cs` — tick interval, graceful close timeout, data directory

**Evidence in code:**
- `src/StopMe.Service/ServiceWorker.cs:70-94` — tick loop with configurable interval
- `src/StopMe.Service/Ipc/LocalApiServer.cs` — named pipe accept loop
- `src/StopMe.Service/Ipc/Handlers/CreateSessionHandler.cs` — session creation with validation

**What is missing:**
- No tray icon or Windows toast notification for session start/end. `INotificationSink` only has `LogNotificationSink` — session events go to the log file only.
- Service runs as SYSTEM after `install.ps1`. In dev mode (`dotnet run` as a regular user), it cannot kill elevated processes.

**Issues / Risks:**
- In dev mode, `WindowsProcessTerminator.CloseOrKillAsync` returns `AccessDenied` for any process running elevated. App blocking appears broken in development.

**Recommended next actions:**
1. Add a `WindowsToastNotificationSink` using `Microsoft.Windows.AppNotifications`.
2. Surface accumulated `AccessDenied` termination count in the Dashboard or service status.

---

### Phase 3 — WinUI 3 UI

**Status:** Mostly Done

**What exists:**
- 8 pages: Dashboard, Blocklists, BlockApps, ActiveSession, Planner, Templates, Statistics, Settings
- All pages have `Loaded += async (_, _) => await ViewModel.RefreshAsync()` — data reloads on navigation
- Theme resources, typography, converters (`InverseBoolConverter`, `NullToVisibilityConverter`)

**Evidence in code:**
- `src/StopMe.App/Pages/BlockAppsPage.xaml.cs:15` — Loaded auto-refresh
- `src/StopMe.App/Pages/PlannerPage.xaml.cs:15` — Loaded auto-refresh
- `src/StopMe.App/Pages/BlocklistsPage.xaml.cs:15` — Loaded auto-refresh
- `src/StopMe.App/MainWindow.xaml.cs:47-78` — NavigateTo switch, GoTo helper for cross-page navigation

**What is missing:**
- No onboarding page. First-time users land on Dashboard showing "StopMe service is not reachable" with no guidance on setup.
- No tray icon — app must remain open for any visual feedback.
- `ActiveSessionPage` has no live countdown timer — the session end time is displayed once at load.

**Issues / Risks (critical):**

**ViewModel Transient lifetime** (`src/StopMe.App/App.xaml.cs:73-80`): All ViewModels are `AddTransient`. `MainWindow.NavigateTo` calls `sp.GetRequiredService<T>()` on every tab switch, creating a fresh ViewModel each time. Consequences:
- Checkbox selections on `BlockAppsPage` are cleared on any tab switch.
- "Start blocking" button becomes disabled again (`HasSelection = false`).
- `LastStartSummary` / `LastCreatedSummary` feedback messages vanish.
- `PlannerViewModel.Blocklists` is cleared and refetched, losing checklist state.

This is the primary reason "you cannot block apps": the user selects apps, switches tabs, comes back — all selections are gone.

**Recommended next actions:**
1. Change all ViewModels to `AddSingleton` in `App.xaml.cs`. (~2h)
2. Add an onboarding page triggered when the service is unreachable on first launch. (~1–2 days)
3. Add a live countdown timer to `ActiveSessionPage` using `DispatcherQueue`. (~1h)

---

### Phase 4 — Browser Extension & NativeHost

**Status:** Done

**What exists:**
- MV3 extension with `webNavigation.onBeforeNavigate` interception (top-frame only)
- `nativeHost.js` — 4-byte little-endian length prefix + JSON framing
- `background.js` — heartbeat alarm (1 min), 5-second redirect-loop cache, fail-open
- `blocked/blocked.html` + `blocked.js` — blocked page with session name, reason, remaining time from URL params
- `popup/popup.html` + `popup.js` — connection status popup
- `StopMe.NativeHost/NativeMessagingChannel.cs` — stdin/stdout stdio bridge
- `LocalApiServiceBridge.cs` — forwards extension messages to service IPC
- `deploy/install.ps1:135-166` — `Register-NativeHost` for Chrome, Edge, Brave in HKLM

**Evidence in code:**
- `extension/chromium/background.js:69-90` — webNavigation listener, tabId deduplication, blocked URL construction
- `src/StopMe.NativeHost/NativeMessagingChannel.cs` — framed read/write loop
- `deploy/install.ps1:149-155` — JSON manifest with `allowed_origins`

**What is missing:**
- Extension not published to Chrome/Edge Web Store — must be loaded as unpacked.
- No automation for NativeHost registration (requires knowing the extension ID first).
- Sub-resource blocking (images, XHR, fetch) via `declarativeNetRequest` not implemented.
- No incognito warning in the popup.

**Issues / Risks:**
- Fail-open design is correct for usability but means blocking silently stops if the NativeHost crashes.
- Extension ID is only known after manual load-unpacked — blocks automated install.

**Recommended next actions:**
1. Add step-by-step instructions on the Dashboard for extension setup (or in an onboarding flow).
2. Implement `declarativeNetRequest` rules fed by `RuleSnapshotHandler` (service side already exists).

---

### Phase 5 — Strict Mode & Anti-Bypass

**Status:** Mostly Done

**What exists:**
- `StrictModeGuard.cs` — evaluates `StopAttempt` against all 5 `LockPolicy` variants; checks `IClockDriftMonitor`
- `AntiBypassRunner.cs` — clock drift detection + extension stale detection; records `BypassEvent`
- `ServiceStateClockDriftMonitor.cs` — bridges `ServiceState.ClockDriftActive` to `IClockDriftMonitor`
- `BypassEvent` model + repository + `BypassEventsHandler`
- `ExtensionHealthMonitor.cs` — tracks last heartbeat per browser, flags stale

**Evidence in code:**
- `src/StopMe.Service/Antibypass/AntiBypassRunner.cs:55-79` — drift detection, records `BypassEvent.ClockDrift`
- `src/StopMe.Core/Engines/StrictModeGuard.cs` — `CanStopAsync` checks lock policy + drift monitor
- `src/StopMe.Service/Antibypass/ExtensionHealthMonitor.cs` — `SnapshotStale` returns browsers without heartbeat

**Issues / Risks:**

**Clock drift does not prevent automatic session completion.** `AntiBypassRunner.CheckClockDriftAsync` sets `_state.ClockDriftActive = true` and records a `BypassEvent`, but `ServiceWorker.TickAsync:109` calls `engine.CompleteExpiredSessionsAsync(nowUtc, ct)` unconditionally. If the user advances the system clock past `EndAtUtc`, the next tick's `nowUtc` is past the end time and the session completes normally. The `ClockDriftActive` flag only blocks *manual* stops via `StrictModeGuard` — not automatic completion. **This is a real strict-mode bypass.**

**Recommended next actions:**
1. In `SessionEngine.CompleteExpiredSessionsAsync`: if `IClockDriftMonitor.IsActive` and session `Mode == Strict`, refuse to complete — extend `EndAtUtc` by one tick interval instead. (~1h)
2. Add a visible Dashboard warning when `ServiceState.ClockDriftActive == true`.

---

### Phase 6 — Statistics

**Status:** Done

**What exists:**
- `IUsageEventRecorder` + `UsageEventRecorder` — records events with privacy modes
- `StatsAggregator.cs` — aggregates by period (Today/Week/Month/AllTime)
- `StatsSummaryHandler`, `StatsExportCsvHandler`, `StatsResetHandler` — IPC handlers
- `StatisticsViewModel.cs` + `StatisticsPage.xaml` — displays stats with export/reset

**Evidence in code:**
- `src/StopMe.Core/Engines/UsageEventRecorder.cs` — writes events respecting privacy settings
- `src/StopMe.Service/Ipc/Handlers/StatsSummaryHandler.cs` — delegates to `IStatsAggregator`

**What is missing:**
- No per-domain breakdown (only aggregate counts).
- No time-series data or visual charts.

---

### Phase 7 — Installer & Deployment

**Status:** Done (MSIX + tray icon explicitly deferred)

**What exists:**
- `deploy/install.ps1` — binary copy, data dir creation, ACL hardening, service registration, NativeHost HKLM registration
- `deploy/uninstall.ps1` — removes service, registry keys, optionally data dir
- `deploy/check-install.ps1` — validates service status + registry
- `deploy/publish.ps1` — `dotnet publish` convenience wrapper
- `TokenAclHardener.cs` — re-applies `auth.token` ACL at every service boot
- `INSTALL.md` — step-by-step instructions

**Evidence in code:**
- `deploy/install.ps1:79-100` — `Harden-DataDirAcl`: Administrators + SYSTEM full, BUILTIN\Users read
- `deploy/install.ps1:102-133` — `Install-Service`: `sc.exe create` with auto-start + failure restart (60s/120s/300s)
- `src/StopMe.Service/Installation/TokenAclHardener.cs:30-58` — file-level ACL applied at startup

**What is missing:**
- MSIX packaging — no single-click installer.
- Extension installation not automated.
- No combined build + publish + install script.

**Issues / Risks:**
- `BUILTIN\Users` `ReadAndExecute` on the data directory and `auth.token` allows any local user to read the IPC token. Intentional design trade-off (WinUI app needs it). DPAPI wrapping deferred to Phase 8.

---

### Phase 8 — Advanced Features

**Status:** Partial (contracts in place, implementations future)

**What exists:**
- `IPauseEngine` + `NullPauseEngine` (default, registered) + `PomodoroPauseEngine` (pure logic, not registered)
- `IInternetBlockingProvider` + `BrowserOnlyInternetBlockingProvider` (stub, not wired to OS)
- `ISyncQueue` — interface only, no implementation
- `ITemplateService` + `TemplateService` + `TemplateCatalog` — 6 default templates, IPC + UI wired

**Evidence in code:**
- `src/StopMe.Core/Engines/NullPauseEngine.cs` — no-op, currently registered
- `src/StopMe.Core/Abstractions/ISyncQueue.cs` — interface stub
- `src/StopMe.Service/Ipc/Handlers/TemplatesHandlers.cs` — `templates.list` + `templates.apply` functional

**What is missing:**
- `PomodoroPauseEngine` not registered in `Program.cs` — Pomodoro breaks have no effect.
- No WFP implementation — internet blocking is Chromium-extension-only.
- No sync outbox, no allowance budget, no advanced analytics.

---

## 6. Core Domain Review

**Models:** complete. `FocusSession` carries `Mode`, `LockPolicy`, `StartAtUtc`, `EndAtUtc`, `Status`, `BlocklistIds`. `BlockRule` supports 14 rule types (Domain, Url, Wildcard, Category, FullInternet, 3 Exception variants, WindowsProcessName, WindowsExecutablePath, WindowsFolderPath, WindowTitle, MicrosoftStoreApp). `UsageEvent` stores domain labels only — no full URLs — which is correct for privacy.

**Enums:** all required types present: `RuleType`, `BlocklistMode`, `SessionMode`, `SessionStatus`, `LockPolicy`, `StopReason`, `BypassEventType`, `UsageEventType`, `PauseType`.

**Value objects:** `BlockDecision`, `AppBlockDecision`, `CompiledRule`, `ActiveSessionInstance`, `StopAttempt`, `StopRequest`, `StrictModeDecision`, `RunningAppInfo` — all correctly immutable.

**Business logic quality:**
- `UrlRuleEngine.Evaluate` has a clearly documented priority order: exception rules first, then allowlists, then full-internet, then blacklist (exact URL → domain → wildcard). Correct.
- `RuleCompiler.CompileForSessionsAsync` joins sessions to their blocklists and maps rules to `CompiledRule` with session metadata. Correct.
- `StrictModeGuard.CanStopAsync` correctly chains lock-policy checks. All 5 policies implemented.

**Concern:** `SessionEngine.GetActiveSessionsAsync` calls `IPauseEngine.GetActivePausesAsync` but `NullPauseEngine` always returns empty. Pomodoro breaks are silently no-ops.

---

## 7. Session Engine Review

**Implemented and correct:**
- `StartSessionAsync` — validates overlapping strict sessions, sets `Status = Active`, records `SessionStarted`
- `CompleteExpiredSessionsAsync` — marks all sessions with `EndAtUtc <= nowUtc` as `Completed`
- `GetActiveSessionsAsync` — returns sessions with `Status = Active` and `StartAtUtc <= nowUtc`
- `CanStopSessionAsync` — delegates to `StrictModeGuard`
- `StopSessionAsync` — validates, marks `Completed`, records `SessionStopped`

**Recurrence:** `RecurrencePattern` + `RecurrenceParser` parse an iCalendar-like RRULE subset. `BootRestorer.RestoreAsync` re-activates sessions that should be active after reboot.

**Fragile:**
- Clock drift bypass (see Phase 5).
- `BootRestorer` uses `nowUtc` at boot. Sessions that were scheduled during offline time are silently skipped.

---

## 8. Blocking Engine Review

### 8.1 Website Blocking

**`UrlMatcher` behavior verified by code reading:**

| Pattern | URL | Result | Reason |
|---|---|---|---|
| `facebook.com` | `https://www.facebook.com/feed` | **Block** | `MatchesDomain` strips `www.` |
| `facebook.com` | `https://m.facebook.com` | **Block** | Subdomain suffix match |
| `facebook.com` | `https://notfacebook.com` | Allow | Suffix check fails |
| `*.youtube.com` | `https://youtube.com/watch` | **Block** | Apex-tolerant wildcard (`src/StopMe.Core/Engines/UrlMatcher.cs:67-70`) |
| `youtube.com/watch` | `https://youtube.com/watch?v=abc` | **Block** | Path-prefix match |
| `allow:google.com` | `https://google.com` | Allow | Exception rule wins (Priority 1) |
| `*.*` | any HTTP URL | **Block** | FullInternet wildcard |

**Gap:** Sub-resource requests (images, XHR, fetch) are not intercepted. Only top-frame navigations trigger `webNavigation.onBeforeNavigate`. A blocked domain's API or CDN remains reachable from other pages.

### 8.2 Application Blocking

**Flow:** `ServiceWorker.TickAsync` → `ProcessWatcher.EnforceAsync` → `AppRuleEngine.Evaluate` → `WindowsProcessTerminator.CloseOrKillAsync`

**`AppRuleEngine`** (`src/StopMe.Core/Engines/AppRuleEngine.cs`):
- `WindowsProcessName` — strips `.exe`, case-insensitive. Correct.
- `WindowsExecutablePath` — normalizes backslash, case-insensitive. Correct.
- `WindowsFolderPath` — prefix match on normalized path. Correct.
- `WindowTitle` — `Contains` with `OrdinalIgnoreCase`. Correct.
- `MicrosoftStoreApp` — matches `PackageFamilyName`. **Non-functional**: `WindowsProcessProvider` uses `Process.GetProcesses()` which does not populate `PackageFamilyName` — it is always null.

**`WindowsProcessTerminator`** (`src/StopMe.Service/Processes/WindowsProcessTerminator.cs`):
- Tries graceful `CloseMainWindow` first with configurable timeout.
- Falls back to `Kill(entireProcessTree: true)`.
- Returns `AccessDenied` for processes the service cannot kill.
- **Works correctly when service runs as SYSTEM** (via `sc.exe install`).
- **Fails with `AccessDenied` in dev mode** (regular user cannot kill elevated processes).

**`WindowsProcessProvider`** (`src/StopMe.Service/Processes/WindowsProcessProvider.cs`):
- Returns ALL processes except a small hardcoded set (`system`, `idle`, `csrss`, `winlogon`, `explorer`, etc.).
- No filtering for visible/user-facing applications.
- `SafeGetExecutablePath` returns null for protected processes.
- **Result:** the list shown in `BlockAppsPage` contains 80–150 entries on a typical machine, dominated by `RuntimeBroker`, `svchost`, `conhost`, `WmiPrvSE`, etc. The "window" badge and sort-by-window help but do not hide the noise.

---

## 9. Windows Service Review

**Lifecycle:** `StartupRoutine` (first `IHostedService`) runs `TokenAclHardener.TryHarden` and waits for DB migration. `ServiceWorker` runs the main tick loop. `LocalApiHostedService` runs the named pipe accept loop. All three run concurrently as hosted services.

**Tick interval:** Configurable via `ServiceOptions.TickIntervalSeconds`. The default in `appsettings.json` determines the maximum re-open window after a blocked process is killed.

**IPC resilience:** Each pipe connection is handled independently. Per-connection errors do not crash the host.

**Logging:** Serilog rolling daily files in `DataDir/Logs/`, 14-day retention, 50 MB per file.

**Post-reboot:** `BootRestorer.RestoreAsync` re-activates sessions. Service registered with `start= auto` and failure restart actions (60s / 120s / 300s).

---

## 10. UI Review — Page by Page

### Dashboard

Shows service status (version, last tick, active session count), active sessions list, blocklist count, recent bypass events. `DashboardViewModel.RefreshAsync` makes 4 sequential IPC calls. If the service is unreachable, a "Service unreachable" error is shown and the connection banner in `MainWindow` reflects the state. **Functional when service is running.**

Missing: no quick-start call-to-action for first-time users.

### Blocklists (`src/StopMe.App/Pages/BlocklistsPage.xaml`)

Left pane: list of blocklists. Right pane: selected blocklist rules table + delete button + create form.

**Create form:** Name field + mode dropdown (Blacklist/Allowlist/FullInternet) + multi-line text area. Placeholder text `"One rule per line — domain, *.wildcard, allow:, category:, *.* "` is the only documentation of the rule format. No inline help, no single-rule-add row, no format examples panel.

**UX problem:** A user who wants to block `facebook.com` must know to type it in the text area without any labeled field. The `allow:` prefix syntax for exceptions and the `*.*` full-internet shortcut are completely opaque to a non-technical user.

**Functional:** CRUD operations wired. `BlocklistsViewModel.ParseRules` correctly classifies rule lines.

### Block Apps (`src/StopMe.App/Pages/BlockAppsPage.xaml`)

Shows running process list (fetched on `Loaded`) with checkbox, process name, window title, executable path, "window" badge. Manual entry form for unlisted processes.

**Critical UX bug — ViewModel Transient lifetime** (`src/StopMe.App/App.xaml.cs:78`):
- All ViewModels registered as `AddTransient`.
- `MainWindow.NavigateTo` calls `sp.GetRequiredService<BlockAppsViewModel>()` on every tab switch.
- A fresh `BlockAppsViewModel` is created → `Loaded` fires → `RefreshAsync` resets `RunningApps` collection → all checkbox selections are **lost**.
- The user selects `discord.exe`, switches to Dashboard to check active sessions, returns — selections gone, "Start blocking" button disabled.

**This is the root cause of "you cannot block apps."**

**Process list noise:** Background services (`RuntimeBroker`, `svchost`, `SearchIndexer`, `MsMpEng`, etc.) appear in the list. Even with `HasVisibleWindow`-first sorting, a typical machine shows 50+ background processes mixed with user-facing apps.

**Functional when not switching tabs:** select apps → click "Start blocking" without leaving the page → full flow works: blocklist created + session started + `ProcessWatcher` kills on next tick.

### Planner (`src/StopMe.App/Pages/PlannerPage.xaml`)

Full session-creation form: name, duration (dropdown), mode (Soft/Strict), lock policy (6 options), conditional fields for RandomText phrase / Password. Blocklist picker (fetched on `Loaded`). Optional schedule toggle with `CalendarDatePicker` + `TimePicker`.

**Dependency not surfaced:** "Create a blocklist from the Blocklists page first if the list is empty." — small caption below the blocklist section. A user who opens Planner first sees an empty list with no clear call-to-action.

**Functional:** Both "Create (don't start)" and "Create + start" are wired to `CreateSessionAsync` with full validation.

### Active Session

Shows current sessions, stop controls with lock-policy-appropriate fields. **No live countdown timer** — session end time shown once at load.

### Statistics

Period selector, 4 stat cards (sessions/websites/apps/time), export CSV, reset. **Functional.**

### Settings

Service connection status, data directory override, theme, stats retention, privacy level. All wired to `IUserSettingsStore` + service IPC. **Functional.**

### Templates

6 default templates (Study, Deep Work, etc.) with apply button. `TemplatesViewModel.ApplyAsync` calls `templates.apply` IPC → `TemplateService.ApplyAsync`. **Functional.**

---

## 11. Browser Extension Review

**Decision path (correct):** Extension never decides alone. Every navigation → `checkUrl` → `nativeHost.js` → stdio → `NativeMessagingChannel` → `LocalApiServiceBridge` → `EvaluateUrlHandler` → `UrlRuleEngine.Evaluate(url, snapshot.CompiledRules)`. Usage event recorded on block.

**Fail-open** (`extension/chromium/background.js:86-89`): if `checkUrl` throws, navigation is allowed. Intentional and correct for usability.

**Blocked page:** `blocked.html` + `blocked.js` display site name, session name, reason, remaining time from URL params. Functional.

**Popup:** Shows connection state (connected / NativeHost down / service down). Functional.

**Gaps:**
- Sub-resource blocking not implemented (only top-frame).
- No incognito warning.
- Extension must be loaded as unpacked (no store distribution).

---

## 12. Native Messaging Host Review

**Protocol:** 4-byte little-endian length prefix + UTF-8 JSON. Standard Chrome native messaging framing. Implemented in both `NativeMessagingChannel.cs` and `nativeHost.js`.

**Message dispatch:** `ExtensionMessageDispatcher` maps `check_url` → `evaluate_url`, `heartbeat` → `extension.heartbeat`, `extension_health` → `extension.health`.

**Error handling:** EOF on stdin → clean shutdown. Malformed JSON → logged + skipped. Service unreachable → error response to extension.

**Registry manifest:** `deploy/install.ps1` writes JSON manifest with `allowed_origins: ["chrome-extension://<ID>/"]` and adds HKLM key. Path in manifest must exactly match installed exe.

---

## 13. Statistics Review

`UsageEventRecorder` respects `StatsRetentionPolicy` (None/Basic/Full). `StatsAggregator` aggregates by `StatsPeriod`. Statistics page shows 4 cards with period selector, export and reset. **Functional.**

Missing: per-domain breakdown, time-series data, visual charts.

---

## 14. Strict Mode & Anti-Bypass Review

**Lock policies — all implemented:**
- `None` — always allowed
- `Confirmation` — requires `UserConfirmed = true`
- `RandomText` — requires `TypedText == session.RequiredText`
- `Password` — requires `PasswordHasher.Verify(provided, stored)` (PBKDF2-SHA512)
- `TimeLocked` — denies all manual stops
- `RestartRequired` — denies all stops; session survives reboots

**Clock drift: real bypass possible.** `AntiBypassRunner` detects drift and sets `ServiceState.ClockDriftActive = true`. `StrictModeGuard` denies manual stops when active. **BUT:** `ServiceWorker.TickAsync:109` calls `CompleteExpiredSessionsAsync(nowUtc)` unconditionally — advancing the clock past `EndAtUtc` completes the session automatically. The drift flag has no effect on this code path.

**Extension stale: visible in bypass events, not actionable.** `BypassEvent.ExtensionStale` is recorded and appears on Dashboard. No toast, no banner, no mechanism to force browser re-enable.

**Verdict:** strict mode is real for all 5 manual-stop lock policies. It is bypassable via (a) clock advancement (session auto-completes) and (b) extension disable (website blocking silently stops).

---

## 15. Installer & Deployment Review

`deploy/install.ps1` covers:
- Admin check
- Binary copy (Service, NativeHost, App — App optional)
- Data dir creation + ACL hardening (Administrators + SYSTEM full; BUILTIN\Users read)
- Service registration via `sc.exe create` (auto-start, failure restart)
- NativeHost HKLM registration for Chrome, Edge, Brave

**Functional.** `deploy/uninstall.ps1` and `deploy/check-install.ps1` cover uninstall and validation.

**Missing:**
- No automated extension ID discovery or extension installation.
- MSIX packaging deferred — no `Add/Remove Programs` entry.
- No combined build + publish + install pipeline.

---

## 16. Privacy & Security Review

**No server calls:** verified across all C# source and extension JS. All data is local.

**Data stored:**
- SQLite: sessions, blocklists, rules, usage events (domain labels only, not full URLs), bypass events
- `auth.token`: 256-bit random hex string
- Local user settings JSON (theme, data dir override)

**`auth.token` ACL:** `BUILTIN\Users` ReadAndExecute on both the data dir and `auth.token`. Intentional: the WinUI app (running as the desktop user) needs to read the token. Any local account can read it and call the service API. Documented as known limitation. DPAPI deferred to Phase 8.

**PasswordHasher:** PBKDF2-SHA512, 200 000 iterations, 16-byte random salt, `CryptographicOperations.FixedTimeEquals`. Correct and secure. Legacy SHA-256 hashes explicitly rejected — forced password reset.

**Log sanitisation:** Only extracted hostnames (`ExtractHost`) are logged, never full URLs.

**Ethical design:** voluntary, transparent, fully local, uninstallable outside strict sessions.

---

## 17. Data Persistence Review

EF Core + SQLite. `StopMeDbContext` with fluent configurations. 2 migrations. Applied at startup via `StartupRoutine`. `StopMeDbContextFactory` supports `dotnet ef` commands without running the host.

**Data directory:** `%ProgramData%\StopMe` (service), `%AppDir%/.stopme-data` (dev), overridable via `--data-dir` or `STOPME_DATA_DIR`.

**Repository pattern:** all interfaces in Core, all implementations in Infrastructure. `BlocklistRepository.AppendRulesAsync` exists and is correct; two test fakes are missing the implementation (build break).

**Missing:** no full database export/import, no `Reset all data` that drops and recreates the DB file.

---

## 18. Code Quality Review

**Strengths:**
- Consistent async/await, `CancellationToken` propagated everywhere.
- Nullable reference types enabled and respected (`!` assertions rare and justified).
- Central package management — single version source.
- `TimeProvider` injected consistently — no `DateTime.UtcNow` in hot paths.
- Test fakes in `tests/<Project>/Fakes/` — correct isolation.
- `FakeTimeProvider` used in all time-dependent tests.
- `IServiceScopeFactory` used correctly for scoped services in singleton contexts.
- Error messages are actionable (`src/StopMe.App.Core/Services/ServiceConnection.cs:65-70`).

**Weaknesses:**
- `StopMe.Core.Tests` build broken (CS0535 x2).
- `ServiceWorker.TickAsync` outer `try/catch Exception` swallows persistent DB errors silently.
- `BlocklistsViewModel.ParseRules` is the only documentation for the rule DSL — no schema, no validation errors with guidance.
- No `ILogger` in `UrlRuleEngine` or `AppRuleEngine` — evaluation failures are invisible.
- `WindowsProcessProvider` does not use the UWP `PackageManager` API — `PackageFamilyName` always null, `MicrosoftStoreApp` matching broken.
- `App.xaml.cs:82` — `AddTransient<MainWindow>()` is architecturally unsound; `AddSingleton` is correct.

---

## 19. Critical Gaps — What Must Be Fixed Before MVP

1. **Build break** — Add `AppendRulesAsync` stub to both test fakes in `StopMe.Core.Tests`. (~15 min)
2. **ViewModel Transient → state loss** — Register ViewModels as `Singleton` in `App.xaml.cs`. The current behavior makes `BlockAppsPage` unusable: all selections clear on tab switch. (~2h)
3. **No onboarding** — Users land on Dashboard with "service not reachable" and no guidance. Add onboarding: install service → load extension → register NativeHost → done. (~1–2 days)
4. **Process list too noisy** — Filter to visible-window apps by default; "Show all processes" toggle for power users. (~3h)
5. **Clock drift enforcement** — In `SessionEngine.CompleteExpiredSessionsAsync`, skip completion if `IClockDriftMonitor.IsActive` for strict sessions. (~1h)
6. **Blocklists UX** — Add a single-rule add row (domain field + type dropdown + Add button) alongside the bulk text area. (~4h)

---

## 20. Bugs / Risks Found

| Severity | Area | Problem | Evidence | Fix |
|---|---|---|---|---|
| Critical | Build | CS0535: test fakes missing `AppendRulesAsync` | `tests/StopMe.Core.Tests/Engines/PomodoroWiringTests.cs:197`, `TemplateServiceTests.cs:100` | Add stub method returning `Task.FromResult(0)` |
| High | UI — Block Apps | ViewModel Transient: selections lost on every tab switch | `src/StopMe.App/App.xaml.cs:78` — `AddTransient<BlockAppsViewModel>()` | Register ViewModels as Singleton |
| High | Strict Mode | Clock drift does not prevent automatic session completion | `src/StopMe.Service/ServiceWorker.cs:109` — `CompleteExpiredSessionsAsync` unconditional | Guard with `IClockDriftMonitor.IsActive` in session engine |
| High | UX | No onboarding; service not running = silent failure across all pages | `src/StopMe.App/App.xaml.cs:30` — first action is `EnsureConnectedAsync`, no setup flow | Add onboarding page |
| High | App Blocking | Service in dev mode (regular user) returns `AccessDenied` on all kills | `src/StopMe.Service/Processes/WindowsProcessTerminator.cs:53` | Document; surface count in UI |
| High | App Blocking | `MicrosoftStoreApp` rule non-functional: `PackageFamilyName` always null | `src/StopMe.Service/Processes/WindowsProcessProvider.cs` — uses `Process.GetProcesses()` | Use `PackageManager` API or WMI |
| Medium | UI — Blocklists | Rule DSL not documented in UI; no single-rule add UX | `src/StopMe.App/Pages/BlocklistsPage.xaml:109` — placeholder only | Add single-rule row with type picker |
| Medium | UI — Block Apps | Process list includes 100+ background services | `src/StopMe.Service/Processes/WindowsProcessProvider.cs:23` — no filtering | Default to `HasVisibleWindow`; toggle for all |
| Medium | Security | `auth.token` readable by all local users | `src/StopMe.Service/Installation/TokenAclHardener.cs:49-56` | DPAPI per-user wrapping (Phase 8) |
| Medium | Strict Mode | Extension stale bypass: logged only, no alert | `src/StopMe.Service/Antibypass/AntiBypassRunner.cs:83-104` | Toast / banner when stale during strict session |
| Medium | Extension | Sub-resource requests not blocked | `extension/chromium/background.js:69` — top-frame only | Implement `declarativeNetRequest` from `rules.snapshot` |
| Low | UI | `ActiveSessionPage` has no live countdown timer | `src/StopMe.App/Pages/ActiveSessionPage.xaml.cs` | `DispatcherQueue` timer updating every second |
| Low | Phase 8 | `PomodoroPauseEngine` exists but not registered; Pomodoro breaks no-op | `src/StopMe.Service/Program.cs` — `NullPauseEngine` registered | Register when Pomodoro feature is activated |
| Low | Architecture | `AddTransient<MainWindow>()` in DI is incorrect | `src/StopMe.App/App.xaml.cs:82` | Change to `AddSingleton` |

---

## 21. Recommended Roadmap

### Step 1 — Fix the build (~30 min)
- Add `AppendRulesAsync` stubs to `StubBlocklistRepository` and `FakeBlocklistRepository` in `StopMe.Core.Tests`.
- Verify `dotnet build` + `dotnet test` both pass from a clean checkout.

### Step 2 — Fix the two core UX blockers (~half day)
- Change all ViewModels from `AddTransient` to `AddSingleton` in `App.xaml.cs`.
- Filter `BlockAppsPage` process list to `HasVisibleWindow` apps by default with "Show all" toggle.

### Step 3 — Onboarding (~2–3 days)
- Add `OnboardingPage` triggered when service is unreachable on first launch.
- Walk through: start the service → load extension → get extension ID → run `install-nativehost.ps1` → done.
- Add a "Setup complete" check that calls `check-install.ps1` and shows a green/red status.

### Step 4 — Blocklists UX (~half day)
- Add a single-rule add row to `BlocklistsPage`: value field + type dropdown + Add button.
- Keep bulk text area for import/power users.

### Step 5 — Close strict mode clock bypass (~1h)
- Guard `CompleteExpiredSessionsAsync` against clock drift for strict sessions.
- Add regression test.

### Step 6 — Notifications (~1 day)
- Implement `WindowsToastNotificationSink`.
- Toast on session start/end and on active bypass events.

### Step 7 — End-to-end smoke test (manual, ~1 day)
- Install via `install.ps1`, load extension, create blocklist, start session.
- Verify: website blocked in Chrome, app killed within tick interval, strict mode denial works.
- Document remaining limitations.

### Step 8 — MVP release candidate
- MSIX packaging.
- `declarativeNetRequest` sub-resource blocking in extension.
- DPAPI `auth.token` wrapping.

---

## 22. Final Verdict

**StopMe is architecturally sound and substantially implemented.** The core engines, IPC, strict mode, statistics, and installer all exist and are wired correctly. Prior automated reviews overstated the defects because they ran against stale binaries.

**The product does not work for a fresh user** for three concrete reasons:
1. No guided setup — the multi-step install is documented in `INSTALL.md` but not surfaced in the app.
2. ViewModel Transient registration makes `BlockAppsPage` lose all state on every tab switch.
3. The build is broken (CS0535) — a clean CI run fails.

None of these are architectural problems. They are finish-line issues fixable in hours to a few days.

**Release readiness:** Not yet. The three blockers above must be resolved first. After that, a knowledgeable user can install and use the product end-to-end. That is the bar for an initial MVP.

---

## 23. Summary Checklist

| Area | Status | Notes |
|---|---|---|
| Build | **Fail** | CS0535 x2 in `StopMe.Core.Tests`; stale binaries hide it |
| Tests (pre-built binaries) | Pass 272/272 | 154 Core + 54 Service + 49 App.Core + 15 NativeHost |
| Core Models | Done | All models, enums, value objects correct |
| Session Engine | Done | Start/stop/restore/recurrence/strict mode all implemented |
| URL Blocking (engine) | Done | Domain stripping, wildcards, exceptions, allowlist, FullInternet |
| URL Blocking (extension) | Partial | Top-frame only; sub-resource blocking missing |
| App Blocking (engine) | Done (Store apps broken) | `MicrosoftStoreApp` non-functional: `PackageFamilyName` always null |
| App Blocking (UI) | **Broken** | ViewModel Transient = selections lost on every tab switch |
| ProcessWatcher kill | Done (with privilege caveat) | Works as SYSTEM; `AccessDenied` in dev mode |
| Windows Service | Done | Worker host, tick loop, IPC server, post-reboot restore |
| UI — 8 pages | Mostly Done | All pages wired; Transient ViewModel is the critical bug |
| Active Session timer | Missing | Static display; no live countdown |
| Onboarding | Missing | No guided setup flow in the app |
| Browser Extension | Mostly Done | Top-frame blocking functional; sub-resource missing |
| NativeHost | Done | Framing, dispatch, service bridge correct |
| Strict Mode | Mostly Done | All lock policies work; clock-advance bypass possible |
| Anti-Bypass | Partial | Drift and stale logged; clock-advance bypass not enforced |
| Statistics | Done | Events, aggregation, export, reset |
| Installer | Done (no MSIX) | Service + NativeHost registration; extension manual |
| Privacy | Done | No server calls; domain labels only; DPAPI deferred |
| Security — PasswordHasher | Done | PBKDF2-SHA512, 200k iterations, random salt |
| Security — auth.token ACL | Known limitation | BUILTIN\Users read intentional; DPAPI Phase 8 |
| MVP Readiness | **No** | 3 blockers: build break, ViewModel Transient, no onboarding |
