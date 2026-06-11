# StopMe — Forensic Audit

**Date:** 2026-06-09  
**Scope:** All phases 1–6 (Phase 7/8 deferred). Every finding is backed by a specific file:line reference; nothing is inferred from docs.

---

## Audit summary

| Severity | Count |
|----------|-------|
| Critical | 2 |
| High     | 4 |
| Medium   | 5 |
| Low / info | 7 |

**Build:** passes clean (0 errors when service is not running; the CS0535 cited in REVIEW.md was already fixed). Only build failures observed are MSB3027 file-lock errors because the service exe is running during incremental builds — not a code defect.

**Tests:** 272 pass, **1 fails**.

---

## Critical

### C-1 — Clock-advance bypass for strict sessions

**File:** `src/StopMe.Service/ServiceWorker.cs:117,139` + `src/StopMe.Core/Engines/SessionEngine.cs:153`

**Exploit:** Set system clock forward past `session.EndAtUtc`. On the next tick, `CompleteExpiredSessionsAsync` (line 117) marks the strict session `Completed` before `AntiBypassRunner.RunAsync` (line 139) has a chance to set the drift flag. The manual-stop path (`CanStopSessionAsync:183`) correctly refuses while drift is active, but natural expiry is unguarded.

**Root cause:** Tick order.
```
117  await engine.CompleteExpiredSessionsAsync(nowUtc, ct);   // no drift check
...
139  await _antiBypass.RunAsync(nowUtc, active, ct);           // sets flag — too late
```

`SessionEngine.CompleteExpiredSessionsAsync:153` has no `IClockDriftMonitor` injection at all.

**Fix options:**
1. Move `AntiBypassRunner.RunAsync` before `CompleteExpiredSessionsAsync` in the tick, and add a pre-check inside `CompleteExpiredSessionsAsync` that skips strict sessions when `_clockDrift.IsClockDriftActive`.
2. Inject `IClockDriftMonitor` into `SessionEngine` and check it in `CompleteExpiredSessionsAsync` for `SessionMode.Strict` sessions.

---

### C-2 — All ViewModels registered as `AddTransient`

**File:** `src/StopMe.App/App.xaml.cs:73-80`

Every tab navigation calls `sp.GetRequiredService<T>()` in `MainWindow.xaml.cs:NavigateTo`, which creates a fresh ViewModel instance. Every field, checkbox, and selection is reset on every page switch. This is why "you can't block apps" — the user checks apps, navigates to Dashboard, returns, and all selections are gone.

**Fix:** Change `AddTransient` to `AddSingleton` for all ViewModels (lines 73–80). All ViewModels are stateful by design; this is the correct lifetime. Pages already call `Loaded += async (_, _) => await ViewModel.RefreshAsync()` so initial data load still happens on first show.

---

## High

### H-1 — Failing test: `steam` vs `steam.exe` in `BlockAppsViewModelTests`

**File:** `tests/StopMe.App.Core.Tests/ViewModels/BlockAppsViewModelTests.cs:76`

```
Expected: { "steam", "discord.exe" }
Actual:   { "steam.exe", "discord.exe" }
```

`BlockAppsViewModel.StartBlockingAsync:193` normalizes every process name to include `.exe`:
```csharp
.Select(n => n!.EndsWith(".exe", ...) ? n : n + ".exe")
```

`AppRuleEngine.NormalizeProcessName` strips `.exe` from both sides before comparison, so matching works correctly. The test expectation is stale.

**Fix:** Change test line 76 to `new[] { "steam.exe", "discord.exe" }`.

---

### H-2 — `ExtractDomains` upgrades Url rules to domain-level blocks

**File:** `src/StopMe.Service/ServiceWorker.cs:155-158`

```csharp
case RuleType.Url:
    if (Uri.TryCreate(r.Value, UriKind.Absolute, out var uri))
        set.Add(uri.Host);
    break;
```

A rule `RuleType.Url` with value `https://www.reddit.com/r/gaming` is intended to block only that path. `ExtractDomains` adds `www.reddit.com` to the hosts file, which blocks the entire domain (all of Reddit) for every application. The browser extension handles path-prefix matching correctly, but the hosts-file layer over-blocks.

This is the primary reason "website blocking is not intuitive" — a URL rule silently escalates to a full domain block at the OS level.

**Fix:** Skip `RuleType.Url` entries in `ExtractDomains`. The extension handles them; the hosts file cannot.

---

### H-3 — `DeleteBlocklistHandler` returns HTTP 200 for non-existent IDs

**File:** `src/StopMe.Service/Ipc/Handlers/DeleteBlocklistHandler.cs:40,54`

`CanDeleteBlocklist` is evaluated before the entity is fetched. A non-existent GUID has no active sessions referencing it, so the guard returns `Allowed`. `_repo.DeleteAsync` is then called on a ghost ID, which is a no-op in SQLite. The handler returns `LocalApiResponse.Ok()` — a 200 with no body — instead of a 404.

**Fix:** Fetch the entity first. If null, return `LocalApiResponse.Error(ErrorCodes.NotFound, "Blocklist not found.")` before calling the guard.

---

### H-4 — `MicrosoftStoreApp` blocking always silently fails

**File:** `src/StopMe.Service/Processes/WindowsProcessProvider.cs` + `src/StopMe.Core/Engines/AppRuleEngine.cs:79-81`

`WindowsProcessProvider` uses `Process.GetProcesses()`, which never populates `PackageFamilyName` (UWP metadata). `AppRuleEngine.MatchStoreApp` checks `!string.IsNullOrEmpty(app.PackageFamilyName)` — always false. Any `MicrosoftStoreApp` rule is silently ignored.

**Fix:** Use `Windows.Management.Deployment.PackageManager.FindPackagesForUser("")` to build a PFN→ProcessName map once per tick, and populate `PackageFamilyName` via cross-referencing by main module basename.

---

## Medium

### M-1 — Named pipe has no `PipeSecurity` ACL

**File:** `src/StopMe.Service/Ipc/LocalApiServer.cs:103-109`

`CreateServerStream` passes no `PipeSecurity`. Windows default grants the interactive logon session access. Any process in the same user desktop session can connect to `\\.\pipe\StopMe.LocalApi.v1`. The bearer token is the only authentication control. If the token leaks (e.g., via file read by malicious software running as the same user), the IPC is fully open.

Combining this with S-3 below (token readable by all local users), any local user on a multi-user machine can authenticate.

**Fix (Phase 7):** Set a `PipeSecurity` that grants access only to the installing user SID and SYSTEM.

---

### M-2 — `auth.token` readable by `BUILTIN\Users` (documented, deferred)

**File:** `src/StopMe.Service/Installation/TokenAclHardener.cs:53-58`

The WinUI app requires token read access. The current solution grants `BUILTIN\Users ReadAndExecute`, meaning any local Windows account can read the bearer token and make authenticated IPC calls. This is documented as a Phase 8 limitation ("DPAPI per-user wrap").

**Impact:** On a shared Windows machine, any local user account can control the distraction blocker. For a single-user home machine, this is acceptable.

---

### M-3 — Protocol version field is declared but never validated

**File:** `src/StopMe.LocalApi/LocalApiRequest.cs:8` + `src/StopMe.Service/Ipc/LocalApiDispatcher.cs`

`LocalApiRequest.Version` is initialized to `LocalApiProtocol.CurrentVersion = 1` on both sides, but `LocalApiDispatcher.DispatchAsync` never reads or checks it. A client with any version value is accepted. There is no forward-compatibility gate.

**Impact:** When the protocol version is bumped in a future phase, the service will silently accept old-version frames until the handler logic happens to fail.

---

### M-4 — `DateTime.UtcNow` in UI countdown logic

**Files:** `src/StopMe.App/Pages/DashboardPage.xaml.cs:201,290`, `src/StopMe.App/MainWindow.xaml.cs:161,219`

Four sites in the UI compute remaining session time using `DateTime.UtcNow` directly rather than the injected `TimeProvider`. The enforcement layer always uses `_clock.GetUtcNow()`, so this is display-only and untestable in isolation. In a `FakeTimeProvider` test environment, the countdown would not advance correctly.

---

### M-5 — `TickAsync` is not atomic; partial-tick state is possible

**File:** `src/StopMe.Service/ServiceWorker.cs:88`

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Tick failed; will retry next interval.");
}
```

If a tick crashes after `CompleteExpiredSessionsAsync` but before `AntiBypassRunner.RunAsync` (e.g., hosts file write fails), the session is marked completed but the drift flag is never cleared, and the hosts file is not updated. The next tick will re-evaluate from a partially committed state. Not catastrophic — the next tick self-heals — but worth knowing.

---

## Low / Info

### L-1 — Token never rotates

`TokenStore.LoadOrCreate` generates one token for the lifetime of the installation. No expiry, no rotation. Phase 8 concern.

### L-2 — `IsAuthorized` leaks token length via early return

**File:** `src/StopMe.Service/Ipc/TokenAuthenticator.cs:21`

```csharp
if (candidate.Length != _expected.Length) return false;
```

This returns early on length mismatch, technically leaking whether the token length is correct. Since the token is always `base64(48 bytes) = 64 chars`, this is not exploitable — the length is fixed. `CryptographicOperations.FixedTimeEquals` is correct for the comparison itself.

### L-3 — `HostsFile.WriteAtomic` is not atomic

**File:** `src/StopMe.Service/Blocking/HostsFile.cs:177-186`

`File.Copy(tmp, path, overwrite: true)` followed by `File.Delete(tmp)` — not atomic. A crash between the two leaves a `.tmp` file. `File.Move` with overwrite is atomic on NTFS and would be safer.

### L-4 — `HostsFileInternetBlockingProvider._lastApplied` is not thread-safe

**File:** `src/StopMe.Service/Blocking/HostsFileInternetBlockingProvider.cs:20`

`_lastApplied` is a plain `HashSet<string>` written by `ApplyDomainsAsync`. Currently only called from `ServiceWorker.TickAsync` (single writer), so it's safe. Adding a second caller would create a race.

### L-5 — `PauseEngine.CyclesBeforeLongBreak` ↔ `MaxPausesPerSession` semantic mismatch risk

**File:** `src/StopMe.Core/Engines/SessionEngine.cs:81`

```csharp
CyclesBeforeLongBreak = policy.MaxPausesPerSession,
```

`CyclesBeforeLongBreak` (engine concept) is mapped to `MaxPausesPerSession` (policy field). If "cycles before long break" means "number of focus cycles" and "max pauses per session" means "total pause count", these are different. Needs a spec cross-check.

### L-6 — `HostsFile` comment timestamp uses `DateTime.UtcNow`

**File:** `src/StopMe.Service/Blocking/HostsFile.cs:52`

```csharp
sb.AppendLine($"# Updated {DateTime.UtcNow:O}");
```

Cosmetic — just a comment line in the hosts file — but inconsistent with the rest of the codebase.

### L-7 — Running process list includes all non-system processes (UX, not security)

**File:** `src/StopMe.Service/Processes/WindowsProcessProvider.cs`

The service returns all non-system processes to the UI, including background services, CLI tools, and helper processes with no windows. The Block Apps page shows a list of ~50–100 processes where the user needs to identify "steam" among dozens of `conhost`, `RuntimeBroker`, etc. The `HasVisibleWindow` field is populated but the UI does not filter on it.

---

## Missing tests (by priority)

1. `CompleteExpiredSessionsAsync` with an active clock drift flag — the bypass (C-1) has no regression test
2. `ExtractDomains` with `RuleType.Url` entries — the over-blocking (H-2) has no test
3. `DeleteBlocklistHandler` with a non-existent blocklist ID — expected 404, currently 200 (H-3)
4. `HostsFile.StripStopMeBlock` with a malformed block (missing end marker) — partial coverage in existing tests but edge cases missing
5. `StopSessionHandler` for all `LockPolicy × SessionMode` combinations — only a subset is tested end-to-end through the handler
6. `BlockAppsViewModelTests.StartBlocking_creates_blocklist_and_session_with_process_rules` — currently failing (see H-1)
