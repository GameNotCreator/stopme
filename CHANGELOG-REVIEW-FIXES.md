# Review-driven fixes — 2026-06-10

This document tracks the response to REVIEW.md (dated 2026-06-09) and the
"app feels unusable / stays on Connecting" feedback that came with it.

## 1. Connecting bug — fixed

**Symptom:** the WinUI app banner stayed on "Connecting…" / "Disconnected"
even when the service was running.

**Root cause:** `ServiceConnection.SetState` raises `StateChanged` from the
thread-pool / IO completion thread. `MainViewModel.OnConnectionStateChanged`
ran synchronously on that thread and fired `INotifyPropertyChanged`.
`MainWindow.OnViewModelPropertyChanged` then called `UpdateConnectionBanner`
which touched `ConnectionLabel.Text`, `ConnectionBanner.Visibility` and
`ConnectionRing.IsActive` directly. WinUI throws cross-thread access
exceptions silently in this path — the banner never updated.

**Fix:**

- `MainWindow.OnViewModelPropertyChanged` now marshals back to the UI thread
  via `DispatcherQueue.TryEnqueue` before touching XAML controls.
- `ServiceConnection.EnsureConnectedAsync` switched from
  `TokenStore.LoadOrCreate` to `TokenStore.TryLoad`. The UI no longer writes a
  bogus token when the service has never run; instead it shows
  `"StopMe service is not running. Start the service (or run deploy\\install.ps1), then click Retry."`
- `ConnectAsync` and `UnauthorizedAccessException` paths now produce
  actionable error strings ("Could not connect to the StopMe service within
  5s. Is StopMe.Service running?", "Service refused the auth token. The token
  may have been rotated — restart the service.").

**Files:** `src/StopMe.App/MainWindow.xaml.cs`,
`src/StopMe.App.Core/Services/ServiceConnection.cs`.

## 2. UX — Block Apps + Planner + Dashboard

**Problem:** the spec's central use case (pick apps → choose duration →
start) was inaccessible. The Planner page was empty. App blocking required
manually crafting a blocklist.

**Fixes:**

### New "Block Apps" page

- `src/StopMe.App.Core/ViewModels/BlockAppsViewModel.cs`
- `src/StopMe.App/Pages/BlockAppsPage.{xaml,xaml.cs}`
- Nav entry inserted between Dashboard and Blocklists.
- Lists running apps via the new `processes.snapshot` IPC op, grouped by
  process name. Checkbox each, plus a manual `discord.exe` text-box entry.
- Duration dropdown (15/30/60/120/240 min) + Soft/Strict toggle.
- "Start blocking" creates a one-shot blocklist of
  `WindowsProcessName` rules and a session via `sessions.create`, then starts
  it immediately. End-to-end the flow is **5 clicks: open page → tick an app
  → pick duration → pick mode → Start blocking**.

### Real Planner page

- `src/StopMe.App.Core/ViewModels/PlannerViewModel.cs` (was a 7-line stub)
- `src/StopMe.App/Pages/PlannerPage.{xaml,xaml.cs}` (was a one-card
  explainer)
- Form: name, duration, mode, lock policy (six options), conditional
  RandomText / Password fields, blocklist multi-select, optional
  `CalendarDatePicker`+`TimePicker` schedule. Two actions: "Create + start"
  and "Create (don't start)".

### Dashboard quick-start

- Top panel: "Start a focus block" → buttons that jump to Block Apps /
  Planner via `MainWindow.GoTo(key)`.
- "Service status" card showing the resolved status string from the
  `status` IPC op.

## 3. Three new IPC operations

| Op | Purpose |
| --- | --- |
| `processes.snapshot` | Returns the running apps the service can see (grouped by name) so the UI can offer a picker. |
| `sessions.create` | Creates a one-shot `FocusSession` from explicit StartAt/EndAt + mode + lock policy + blocklist IDs. Optional `StartImmediately`. |
| `blocklists.add_rule` | Appends rules to an existing blocklist (consults `IStrictModeGuard`). |

DTOs in `src/StopMe.LocalApi/Dtos/ProcessSnapshotDtos.cs`. Handlers in
`src/StopMe.Service/Ipc/Handlers/{ProcessSnapshot,CreateSession,AddRule}Handler.cs`.
Client methods on `StopMeLocalApiClient`. Tests in
`tests/StopMe.Service.Tests/Ipc/Phase9HandlerTests.cs`.

## 4. REVIEW.md fixes — critical / high

### PasswordHasher: SHA-256 → PBKDF2-SHA512

- `src/StopMe.Core/Engines/PasswordHasher.cs`
- New hash format: `pbkdf2-sha512$v=1$i=200000$salt=<b64>$hash=<b64>`.
- 200 000 iterations, 16-byte salt, SHA-512 (64-byte output).
- `Verify` rejects legacy SHA-256 hashes (forces a password reset rather
  than silently accepting the weak placeholder). `IsLegacyHash` helper
  exposed for migration paths.
- Tests: `tests/StopMe.Core.Tests/Engines/PasswordHasherPbkdfTests.cs`.

### TimeProvider injection — UrlRuleEngine + SessionRepository

- `src/StopMe.Core/Engines/UrlRuleEngine.cs` — accepts a `TimeProvider`
  through a new constructor; `RemainingSeconds` calls
  `_clock.GetUtcNow().UtcDateTime`. Default ctor preserves
  `TimeProvider.System` for callers that don't inject one.
- `src/StopMe.Infrastructure/Repositories/SessionRepository.cs` — accepts
  `TimeProvider`; `UpdateStatusAsync` writes `UpdatedAtUtc` via the injected
  clock.
- Tests: `tests/StopMe.Core.Tests/Engines/UrlRuleEngineTimeProviderTests.cs`.

### RRULE COUNT enforcement

- `src/StopMe.Core/Recurrence/RecurrencePattern.cs` — `IsActiveOn` now
  enforces `COUNT=`. Implementation walks days from the anchor counting
  matching occurrences; correct under `BYDAY` ordering and `INTERVAL` skips.
- Tests: `tests/StopMe.Core.Tests/Recurrence/RecurrenceCountEnforcementTests.cs`.

### Clock drift bypass prevention

- `src/StopMe.Service/State/ServiceState.cs` — new
  `ClockDriftActive` flag with `SetClockDriftActive(bool)`.
- `src/StopMe.Service/Antibypass/AntiBypassRunner.cs` — sets the flag when
  drift is sampled, clears it on a clean tick (or when no strict session is
  active).
- `src/StopMe.Core/Abstractions/IClockDriftMonitor.cs` — Core-side
  abstraction (with `NullClockDriftMonitor` default for tests / Core-only
  hosts).
- `src/StopMe.Service/Antibypass/ServiceStateClockDriftMonitor.cs` —
  bridges `ServiceState` onto the Core interface.
- `src/StopMe.Core/Engines/SessionEngine.cs` — `CanStopSessionAsync` now
  consults the monitor: while drift is active, every strict-mode stop is
  denied regardless of lock policy. Soft sessions are unaffected.
- Tests: `tests/StopMe.Core.Tests/Engines/ClockDriftBypassPreventionTests.cs`.

## 5. What is NOT in this batch

Documented but deferred to a follow-up:

- **auth.token DPAPI wrap.** Adding DPAPI requires touching `TokenStore`
  (which lives in `StopMe.LocalApi` — cross-platform). The cleanest path
  goes through an `ITokenProtector` abstraction with a Windows DPAPI
  implementation; doing this alongside the Connecting fix would have risked
  bricking the connection during this session. Listed as the next item.
- **MV3 alarm-recreation guard** in the Chrome extension.
- **PackageFamilyName lookup** for Microsoft Store apps.
- **Tray icon** and **StartAtBoot** implementations.

## 6. Test counts after this batch

```
Core      154 / 154   (+14 vs baseline: PBKDF2 ×6, RRULE COUNT ×4,
                       UrlRuleEngine TimeProvider ×1, ClockDrift bypass ×3)
Service    54 / 54    (+4 : Phase9HandlerTests — ProcessSnapshot, CreateSession,
                       AddRule)
App.Core   49 / 49    (+9 : BlockAppsViewModel ×4, PlannerViewModel ×5)
NativeHost 15 / 15
TOTAL     272 / 272   (vs 245 baseline)
```

Build: 0 errors, 1 pre-existing CA1859 warning in StopMe.App (WinUI opt-out).
A bug surfaced during the test run — `BlocklistRepository.UpdateAsync` cannot
handle tracked entities with new navigation children. Fixed by adding
`IBlocklistRepository.AppendRulesAsync(blocklistId, rules, updatedAtUtc)`
which tracks the new BlockRule entities directly on the DbContext.

## 7. Manual end-to-end test plan

1. Stop the running service: `Stop-Process -Id 20296 -Force` (or
   `sc.exe stop StopMeService`).
2. `dotnet build` — clean.
3. `dotnet test` — green.
4. `dotnet run --project src/StopMe.Service -- --data-dir C:\temp\stopme-data`
   in one terminal.
5. `dotnet run --project src/StopMe.App/StopMe.App.csproj -p:Platform=x64`
   in another. Settings → Data directory = `C:\temp\stopme-data` → Save →
   Retry. Banner clears (no more Connecting).
6. Block Apps → tick a running app (e.g. Notepad or Discord) → 15 min →
   Soft → Start blocking. App should close within ~2 seconds (next service
   tick).
7. Reopen the same app. It re-closes.
8. Active session page shows the countdown. Stop with Confirmation (Soft
   mode).
9. Planner → Create a session with the same blocklist, Strict +
   TimeLocked, 5 min. Try to stop → denied with "Strict mode: cannot stop
   before session ends."
10. (Optional) Advance the system clock by +10 min while the strict session
    is active. Next tick: `BypassEvent { Type: ClockDrift }` recorded;
    Anti-bypass card on Dashboard shows it; stop attempts denied with
    "Clock drift detected — strict session cannot be stopped until the
    system clock stabilises."
