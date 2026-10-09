# StopMe

> Block distractions before they block your future.

Privacy-first focus app for Windows. Lets the user create focus sessions during which
selected websites, applications, categories, or the whole Internet become unreachable.
Decisions are taken locally on the machine — browsing history is never sent to a server.

This repository contains the production-grade .NET 8 implementation. The full product
spans nine modules (UI, Windows Service, browser extensions, native messaging host,
installer, …). This commit covers **Phase 1** (domain core + persistence),
**Phase 2** (Windows Service + local IPC + process watcher), **Phase 3** (WinUI 3
desktop shell + ViewModels + 5 pages), **Phase 4** (Chromium MV3 extension +
Native Messaging Host), and **Phase 5** (strict-mode reinforcement, settings lock,
extension health monitoring, anti-bypass detection).

---

## What ships

| Component | Responsibility | Status |
| --- | --- | --- |
| `StopMe.Core` | Domain models, value objects, rule engines, recurrence | ✅ Phase 1 |
| `StopMe.Infrastructure` | EF Core + SQLite persistence, repositories, seed | ✅ Phase 1 |
| `StopMe.LocalApi` | Named-Pipes contract + framing + DTO + client | ✅ Phase 2 |
| `StopMe.Service` | Worker host, session/process loop, IPC server | ✅ Phase 2 |
| `StopMe.App.Core` | ViewModels, IServiceConnection, settings store (no WinUI) | ✅ Phase 3 |
| `StopMe.App` | WinUI 3 shell + 7 pages + theme system | ✅ Phase 3 + 6 |
| `StopMe.NativeHost` | Chrome Native Messaging bridge to the service | ✅ Phase 4 |
| `extension/chromium/` | MV3 service worker + popup + blocked page | ✅ Phase 4 |
| Strict mode reinforcement | ✅ Phase 5 |
| Statistics + privacy mode + CSV export | ✅ Phase 6 |
| PowerShell installer + ACL hardening | ✅ Phase 7 |
| Session templates + Pomodoro/Internet abstractions | ✅ Phase 8 (templates real, rest documented stubs) |
| Test totals | 135 Core / 50 Service / 33 App.Core / 15 NativeHost — **233 tests passing** |

The Phase 1 deliverable is the **noyau métier testable** described by the spec: every rule
priority, every session-active rule, and every strict-mode lock policy is implemented and
covered by tests. Once the service and UI layers come online they will consume these engines
as-is.

---

## Repository layout

```
StopMe/
├── Directory.Build.props          shared compiler/analyzer settings (Nullable, warnings as errors)
├── Directory.Packages.props       central NuGet versions
├── global.json                    pins .NET 8 SDK
├── StopMe.sln
├── src/
│   ├── StopMe.Core/               pure domain — no infrastructure deps
│   │   ├── Abstractions/          ports (repositories, engines)
│   │   ├── Enums/                 BlocklistMode, RuleType, SessionStatus, SessionMode, LockPolicy …
│   │   ├── Models/                EF-mappable entities (§5)
│   │   ├── ValueObjects/          BlockDecision, CompiledRule, ActiveSessionInstance …
│   │   ├── Engines/               UrlRuleEngine, AppRuleEngine, SessionEngine, RuleCompiler
│   │   ├── Recurrence/            RecurrencePattern + RFC 5545 mini-parser
│   │   └── Import/                BlocklistTextParser (TXT import/export)
│   └── StopMe.Infrastructure/
│       ├── Persistence/
│       │   ├── StopMeDbContext.cs
│       │   ├── Configurations/    one IEntityTypeConfiguration<T> per entity
│       │   └── Migrations/        EF Core migrations (InitialCreate)
│       ├── Repositories/          IBlocklistRepository, ISessionRepository, …
│       ├── Seed/                  default-categories.json + CategorySeeder
│       └── DependencyInjection/   AddStopMeInfrastructure(IServiceCollection, …)
└── tests/
    └── StopMe.Core.Tests/         xUnit + FluentAssertions + Microsoft.Extensions.TimeProvider.Testing
```

Clean-architecture guarantee: `StopMe.Core` has **zero** references to EF Core, SQLite,
Windows APIs, or WinUI. All persistence and OS interaction live behind interfaces in
`Abstractions/` and are implemented by `StopMe.Infrastructure` (and, in later phases, the
Windows service).

---

## Rule engine semantics

The rule evaluation order matches §8 of the spec exactly. Active rules are first compiled
by `RuleCompiler` (which resolves Category rules against `CategoryDomains` and synthesises a
full-Internet rule for blocklists whose `Mode = FullInternet`), then evaluated in this
order:

| # | Predicate | Outcome |
| --- | --- | --- |
| 1 | `ExceptionUrl` match | Allow |
| 2 | `ExceptionDomain` match | Allow |
| 3 | `ExceptionWildcard` match | Allow |
| 4 | Every `Allowlist`-mode blocklist contains the URL | Continue, else Block |
| 5 | Any `FullInternet`-type rule or `FullInternet`-mode blocklist active | Block |
| 6 | Blacklist-mode `Url` match | Block |
| 7 | Blacklist-mode `Domain` match | Block |
| 8 | Blacklist-mode `Wildcard` match | Block |
| 9 | Else | Allow |

`MatchesDomain` strips a leading `www.` from both rule and URL and matches the apex plus
any subdomain. `MatchesExactUrl` is host-tolerant (also matches subdomains) and does
path-prefix matching when the rule has no query string. `MatchesWildcard` converts globs
to `^…$`-anchored regexes (cached) and gives apex-tolerance to `*.host` patterns.

App matching (`AppRuleEngine`) handles `WindowsProcessName` (case-insensitive, `.exe`
optional), `WindowsExecutablePath`, `WindowsFolderPath` (prefix), `WindowTitle` (substring,
case-insensitive), and `MicrosoftStoreApp` (Package Family Name).

Strict-mode lock policies (`SessionEngine.CanStopSessionAsync`):

| Mode  | LockPolicy        | Behavior |
| ----- | ----------------- | --- |
| Soft  | None              | Allow |
| Soft  | Confirmation      | Require `UserConfirmed == true` |
| Soft  | RandomText        | Exact phrase match against `RequiredText` |
| Soft  | Password          | SHA-256 verify against `PasswordHash` *(Phase 1 placeholder, Phase 2 → Argon2id)* |
| Soft  | TimeLocked        | Allow only after `EndAtUtc` |
| Soft  | RestartRequired   | Always deny |
| Strict| Password          | SHA-256 verify (else deny) |
| Strict| RandomText        | Exact phrase verify (else deny) |
| Strict| any other         | Allow only after `EndAtUtc` |

---

## Recurrence

`RecurrenceParser` accepts a minimal subset of RFC 5545 RRULE: `FREQ=DAILY|WEEKLY`,
`INTERVAL`, `BYDAY` (`MO,TU,WE,TH,FR,SA,SU`), `UNTIL` (ICS date-time), and `COUNT`
(parsed, not yet enforced by the matcher). Recurring sessions store a UTC anchor
(`StartAtUtc`, `EndAtUtc`) and a `TimeZoneId`; `SessionEngine.GetActiveSessionsAsync` uses
the anchor's local time-of-day and the user's timezone to derive the current occurrence
window. Sessions that span midnight are detected and resolved against the previous day's
recurrence.

---

## Building and testing

Prerequisites: .NET 8 SDK (≥ 8.0.421 verified).

```bash
dotnet restore
dotnet build               # 0 warnings, 0 errors (warnings-as-errors is on)
dotnet test                # 99 / 99 passing
```

To regenerate EF Core migrations (e.g. after adding an entity):

```bash
dotnet tool install --global dotnet-ef --version 8.0.10   # once

dotnet-ef migrations add <Name> \
  --project src/StopMe.Infrastructure/StopMe.Infrastructure.csproj \
  --startup-project src/StopMe.Infrastructure/StopMe.Infrastructure.csproj \
  --output-dir Persistence/Migrations
```

To apply the migrations to a local SQLite database:

```bash
STOPME_DB_PATH=/path/to/stopme.db dotnet-ef database update \
  --project src/StopMe.Infrastructure/StopMe.Infrastructure.csproj \
  --startup-project src/StopMe.Infrastructure/StopMe.Infrastructure.csproj
```

Production install path will be `%ProgramData%\StopMe\stopme.db` (Phase 7).

---

## Wiring into a host

`AddStopMeInfrastructure` registers the DbContext, repositories, engines, and
`TimeProvider.System`:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddStopMeInfrastructure(
    sqliteConnectionString: $"Data Source={dbPath}");

builder.Services.AddHostedService<MyWorker>();   // Phase 2
```

Inside the worker:

```csharp
var clock = sp.GetRequiredService<TimeProvider>();
var sessionEngine = sp.GetRequiredService<ISessionEngine>();
var ruleCompiler = sp.GetRequiredService<IRuleCompiler>();
var urlEngine = sp.GetRequiredService<IUrlRuleEngine>();

var nowUtc = clock.GetUtcNow().UtcDateTime;
var active = await sessionEngine.GetActiveSessionsAsync(nowUtc, ct);
var rules = await ruleCompiler.CompileForSessionsAsync(active, ct);

var decision = urlEngine.Evaluate("https://youtube.com/watch?v=…", rules);
```

The `IAppRuleEngine` is symmetric for `RunningAppInfo` snapshots that the Phase 2 service
will collect from `System.Diagnostics.Process`.

---

## Known Phase 1 limitations

These are intentional — they belong to later phases of the spec and are documented so
nothing is silently broken later.

* **Password hashing** uses `SHA-256` only. Phase 2 swaps in Argon2id (or PBKDF2-SHA512)
  when the UI/service wires up a real credential store. The verify path already uses
  `CryptographicOperations.FixedTimeEquals`.
* **RRULE `COUNT=`** is parsed but not yet enforced by `IsActiveOn`. UNTIL is enforced.
* **DST around recurring sessions:** `TimeZoneInfo.ConvertTimeToUtc` is used directly,
  which is correct for ambiguous/invalid local times in most cases but does not exhaust
  every fall-back/spring-forward edge case. Phase 2 should add explicit tests for the two
  transitions per year.
* **Allowlist-mode interaction across multiple sessions** uses the most-restrictive
  intersection: if any active session declares an allowlist and the URL is not in it, the
  URL is blocked unless an Exception rule matches. This is documented behaviour, not a
  bug — but worth flagging because the spec does not exhaustively define multi-session
  conflicts.
* **No driver-level Internet blocking yet.** `FullInternet` mode currently only blocks
  navigation when paired with the browser extension and the process watcher (both Phase 2+).
  The Windows Filtering Platform integration referenced by §12 is explicitly Phase 8.
* `StopMeDbContextFactory` writes to `%TEMP%\stopme-design.db` for `dotnet-ef` only — the
  production database location is set by the host via `AddStopMeInfrastructure`.

---

## Test coverage map (§28.1)

| Spec line | Test class |
| --- | --- |
| domain matching | `UrlMatcherTests`, `UrlRuleEngineTests.Domain_rule_blocks_subdomain` |
| wildcard matching | `UrlMatcherTests.Wildcard_match_handles_host_and_path_globs` |
| allowlist priority | `UrlRuleEngineTests.Allowlist_mode_…` |
| full internet priority | `UrlRuleEngineTests.FullInternet_…` |
| recurrence rules | `RecurrenceParserTests`, `RecurrencePatternTests` |
| session active detection | `SessionEngineTests.GetActive_…` |
| strict mode stop denial | `SessionEngineTests.Strict_mode_…`, `Random_text_lock_…`, `Soft_mode_with_confirmation_…` |
| app process matching | `AppRuleEngineTests` |
| import/export parsing | `BlocklistTextParserTests` |

Plus an integration tier in `DatabaseRoundTripTests` that creates an in-memory SQLite
schema and round-trips real entities through the repositories — this is the only test
class that touches `StopMe.Infrastructure`.

---

## Phase 2 — Windows Service

`StopMe.Service` is a .NET Worker host that:

* Registers as a Windows Service via `Microsoft.Extensions.Hosting.WindowsServices`. Falls
  back to a console process when started outside SCM (so dev runs work without admin).
* On startup applies EF Core migrations, ensures a default local user profile, and seeds
  the 11 built-in domain categories. Boot restoration then settles any session statuses
  that survived a reboot — strict-mode sessions resume automatically because the engine
  recomputes the active window from the anchor + RRULE.
* Drives the §4.2 tick loop (`Task.Delay(2s)` by default): completes expired sessions,
  resolves currently active sessions, compiles the rule snapshot, publishes it to the
  shared `ServiceState`, calls the `ProcessWatcher`, then sleeps.
* `ProcessWatcher` snapshots `Process.GetProcesses()` (guarded by a small block-list of
  OS-critical names), evaluates each through `IAppRuleEngine`, and asks `IProcessTerminator`
  to graceful-close-then-kill matching processes. Successful terminations are recorded as
  `UsageEvent` and forwarded to the `INotificationSink`.
* Exposes a **token-authenticated Named Pipes server** (`StopMe.LocalApi.v1`) with these
  operations: `status`, `sessions.active`, `sessions.start`, `sessions.stop`,
  `blocklists.{list,create,update,delete}`, `rules.evaluate_url`, `rules.evaluate_app`.
  The protocol is a 4-byte little-endian length prefix + UTF-8 JSON envelope. Phase 4's
  native messaging host and Phase 3's UI will consume `StopMe.LocalApi.Client.StopMeLocalApiClient`.
* Logs through Serilog: console + daily-rolling file at `<DataDir>\Logs\stopme-service-YYYYMMDD.log`.

### Running it

```bash
# From a console (no admin required for the run itself)
dotnet run --project src/StopMe.Service -- --data-dir C:\temp\stopme-smoke

# Install / uninstall as a real Windows Service (requires an elevated prompt)
dotnet publish -c Release -r win-x64 --self-contained false src/StopMe.Service
src\StopMe.Service\bin\Release\net8.0-windows\win-x64\publish\StopMe.Service.exe install
src\StopMe.Service\bin\Release\net8.0-windows\win-x64\publish\StopMe.Service.exe start
```

Service-control verbs: `install`, `uninstall`, `start`, `stop`, `status`. They shell out
to `sc.exe`; the wrapping is in `StopMe.Service\Installation\ServiceControl.cs`.

### Data layout

```
%ProgramData%\StopMe\           (or --data-dir override)
├── stopme.db                   SQLite (WAL mode by EF Core default)
├── stopme.db-wal
├── stopme.db-shm
├── auth.token                  Base64(48 random bytes); the IPC bearer token
└── Logs\
    └── stopme-service-YYYYMMDD.log   daily-rotated; max 14 retained
```

For dev, the service picks `<bin>\.stopme-data` when not running as a Windows Service.

### Talking to the service

The `StopMeLocalApiClient` wraps the pipe + framing + auth header:

```csharp
var token = new TokenStore(@"C:\ProgramData\StopMe").LoadOrCreate();
await using var client = new StopMeLocalApiClient(token: token);
await client.ConnectAsync(TimeSpan.FromSeconds(5));

var response = await client.StatusAsync();
var status = response!.PayloadAs<StatusDto>()!;

var blocked = await client.EvaluateUrlAsync(
    new EvaluateUrlRequest { Url = "https://youtube.com/" });
```

### Phase 2 known limitations
(See also the "Phase 3 known limitations" section further down for UI-side stubs.)

* `WindowsProcessProvider` uses `Process.MainModule.FileName`, which throws on bitness
  mismatches (32-bit process / 64-bit service). For the tested cases this falls back to
  `null` and the executable-path / folder-path rule types simply don't match — process-name
  matching still works. Phase 5 will replace this with `QueryFullProcessImageName`.
* Microsoft Store app detection is not implemented: `IsMicrosoftStoreApp` is always
  `false` and `PackageFamilyName` always `null`, so `MicrosoftStoreApp` rules currently
  never match anything the service collects on its own. (They do still work when the
  caller supplies the package family name through `rules.evaluate_app`.)
* `auth.token` is created with default ACL inherited from `%ProgramData%`. The Phase 7
  installer will tighten this to admins + the install user.
* The §4.2 extension-health monitor and anti-bypass monitor are intentionally NOT in this
  loop — they require the browser extension to exist (Phase 4) and a hardened strict-mode
  policy (Phase 5).
* `LocalApiServer` enforces auth via a token, not via a per-process ACL on the pipe. Any
  local process that knows the token can connect. The installer-controlled ACL hardening
  belongs to Phase 7.
* CLI verbs `install`/`uninstall`/`start`/`stop` shell out to `sc.exe` and need an
  elevated prompt — the Phase 7 installer will own this.

---

## Phase 3 — WinUI 3 desktop UI

`StopMe.App` is a WinUI 3 application (`net8.0-windows10.0.26100.0`, **x64**). MVVM split
across two projects:

* `StopMe.App.Core` (`net8.0`, no WinUI deps) — `ViewModels/`, `Services/`, `Models/`.
  Built on `CommunityToolkit.Mvvm` source generators (`ObservableProperty`, `RelayCommand`).
  Every view model has unit tests against `FakeServiceConnection`.
* `StopMe.App` (`net8.0-windows10.0.26100.0`, **x64 only**) — XAML, code-behind, theme
  resources. Pure plumbing; no business logic here.

### Shell + pages

`MainWindow` is a `NavigationView` with a connection banner on top. Pages:

| Page | Status | What it does |
| --- | --- | --- |
| Dashboard | ✅ | Three stat cards (active sessions / blocklists / service info) + live session list + error banner. Calls `status`, `sessions.active`, `blocklists.list`. |
| Blocklists | ✅ | Master list + detail (rules table) + creation form parsing one rule per line (`domain`, `*.wild`, `path/`, `allow:`, `category:`, `*.*`). Calls `blocklists.{list,create,delete}`. |
| Active session | ✅ | Active session cards + stop card with confirmation / random-text / password fields. Surfaces the strict-mode denial reason from the service. |
| Settings | ✅ | Theme (System / Light / Dark, applied live via `ElementTheme`), start-at-boot, tray-icon toggle, data-directory override. Persists to `%LocalAppData%\StopMe\user-settings.json`. |
| Planner | 🟡 stub | One-card explainer; the drag/drop calendar + RRULE preview ship with Phase 4. |
| Statistics | 🟡 stub | One-card explainer; charts + CSV export ship with Phase 6. |

### Theme system

The user-pinned palette (warm parchment / dark stone) is translated to XAML at
`src/StopMe.App/Resources/Theme.xaml` as two `ResourceDictionary.ThemeDictionaries`
(`Light` + `Dark`) plus a `HighContrast` fallback. Built-in WinUI brushes
(`ApplicationPageBackgroundThemeBrush`, `AccentFillColorDefaultBrush`,
`NavigationViewExpandedPaneBackground`, …) are overridden in the same dictionary so stock
controls inherit the palette.

Typography in `Resources/Typography.xaml`: `Oxanium` (sans), `Merriweather` (serif),
`Fira Code` (mono), each with system fallback chains. Branded fonts will be embedded by
the Phase 7 installer.

Button / card / textbox styles in `Resources/Controls.xaml`: `StopMeCard`,
`StopMePrimaryButton`, `StopMeGhostButton`, `StopMeDestructiveButton`,
`StopMeBadge`, `StopMeTextBoxOutline`.

### Service connection lifecycle

`ServiceConnection` (in `StopMe.App.Core/Services`) wraps `StopMeLocalApiClient`:

* On `EnsureConnectedAsync()`, it loads the bearer token from the configured data
  directory (`%ProgramData%\StopMe` by default, or the user override), connects, and
  publishes a `ConnectionState` (`Disconnected → Connecting → Connected | Failed`).
* Every typed method (`GetStatusAsync`, `ListBlocklistsAsync`, …) calls
  `EnsureConnectedAsync` first; on `IOException` / framing errors it transitions to
  `Failed` and surfaces `LastError` to `MainViewModel`, which drives the top banner.
* The reconnect button on the banner re-runs `EnsureConnectedAsync`.

### Building and running

```bash
dotnet build src/StopMe.App/StopMe.App.csproj -p:Platform=x64
dotnet test --nologo --no-build                    # all 151 tests
```

To actually launch the UI you need the **Windows App SDK 2.x runtime** (this repo pins
`Microsoft.WindowsAppSDK` 2.1.3). Install via `winget install Microsoft.WindowsAppRuntime.1.6`
or newer.

### Phase 3 known limitations

* **Cannot be visually verified in a headless dev environment.** Build success + 26
  ViewModel tests are the only validation in this commit; opening the UI on a real
  desktop is left to the user.
* **Tray icon is a settings toggle without a sink.** `ShowTrayIcon` persists, but
  no `INotifyIcon`/`H.NotifyIcon` host is wired up — Phase 7 packaging will provide it.
* **Start-at-boot is a settings toggle without an implementation.** The Phase 7
  installer will register the autostart shortcut / scheduled task.
* **Planner and Statistics pages are stubs** that explicitly describe what ships in
  Phase 4 / Phase 6.
* **Connection is one-way push-style.** The UI calls the service via the Named Pipe
  on demand; there is no notification channel from service → UI yet. Live-update on
  the Dashboard relies on the user pressing **Refresh**. Phase 6 will add a
  pipe-level event stream so notifications fan out without polling.
* **No DI auto-discovery** — `App.xaml.cs` registers every VM by hand. Acceptable at
  current scope; a small `IServiceCollection` extension would clean this up before
  Phase 5.
* **Font files not embedded.** Branded fonts (Oxanium / Merriweather / Fira Code) fall
  back to system fonts (Segoe UI Variable / Cambria / Cascadia Mono). Phase 7
  packaging embeds the actual TTFs as `Content` items.

---

## Phase 4 — Chromium MV3 extension + Native Messaging Host

### `StopMe.NativeHost` (net8.0 console exe)

A pure forwarder between the browser extension and the Windows Service. The browser
launches it as a child process; communication is over **Chrome Native Messaging** (4-byte
little-endian length prefix + UTF-8 JSON on stdin/stdout, max 1 MiB inbound).

```
extension ──connectNative──▶ StopMe.NativeHost.exe ──Named Pipe──▶ StopMe.Service
                              │
                              └── Serilog → %ProgramData%\StopMe\Logs\stopme-nativehost-YYYYMMDD.log
```

The host translates three extension message types onto the existing `StopMe.LocalApi`
operations:

| Extension request | Maps to | Reply |
| --- | --- | --- |
| `CHECK_URL` | `rules.evaluate_url` | `CHECK_URL_RESULT` (blocked, reason, sessionName, remainingSeconds, …) |
| `GET_STATUS` | `status` | `STATUS` (connected, serviceVersion, activeSessionCount, …) |
| `GET_ACTIVE_SESSIONS` | `sessions.active` | `ACTIVE_SESSIONS` (sessions array with mode/lockPolicy/remainingSeconds) |

Anything else returns `ERROR { errorCode, errorMessage }`. Token comes from
`%ProgramData%\StopMe\auth.token` (same file the UI uses) via `TokenStore`.

Stdout is reserved for native-messaging frames; **never write to stdout from anywhere
else.** All logging goes through Serilog to a file.

### `extension/chromium/` (Manifest V3)

```
extension/chromium/
├── manifest.json              MV3, Chrome 108+, permissions: tabs, storage, webNavigation, nativeMessaging, alarms
├── background.js              service worker (type=module): webNavigation.onBeforeNavigate → checkUrl → redirect to blocked.html
├── nativeHost.js              connectNative wrapper, FIFO queue, auto-reconnect
├── css/base.css               palette mirroring the WinUI theme
├── blocked/blocked.{html,css,js}    full-page block screen with countdown
├── popup/popup.{html,css,js}        toolbar popup: connection badge + status + active sessions list
└── icons/icon{16,48,128}.png        generated solid-amber squares with a white "S"
```

`background.js` only intercepts **top-frame navigations**. Sub-resources, XHR, fetch and
iframes pass through. Phase 5 will swap to `declarativeNetRequest` dynamic rules driven
by the compiled rule snapshot.

A 5-second per-tab cache prevents redirect loops when the blocked page itself triggers
a navigation. Connection failures fail open (the user can see the disconnect state in the
popup) — Phase 5 owns extension health monitoring and the *unsupported browser auto-close*
behaviour from §14.

### Native messaging registration

```powershell
# Register the host for Chrome + Edge + Brave under HKCU
pwsh deploy\install-nativehost.ps1 `
  -ExePath "C:\path\to\StopMe.NativeHost.exe" `
  -ChromeExtensionId "<id-from-chrome://extensions>" `
  -EdgeExtensionId "<id-from-edge://extensions>"

# To remove
pwsh deploy\uninstall-nativehost.ps1
```

The script writes the manifest JSON to `%LOCALAPPDATA%\StopMe\nativehost\` and the registry
pointer at `HKCU\Software\<Vendor>\NativeMessagingHosts\com.stopme.nativehost`. Both
operations are HKCU-only — **no admin needed for the dev workflow**.

The Phase 7 installer will handle this registration automatically for packaged installs.

### Phase 4 known limitations

* **Sub-resources, iframes, XHR/fetch are not intercepted.** Only top-frame navigations.
  Phase 5 will add `declarativeNetRequest` dynamic rules driven by the rule snapshot the
  service already publishes through `ServiceState`.
* **Fail-open on host disconnect.** Navigations proceed if the native host is unreachable.
  The popup surfaces the disconnect state. Phase 5 hardens this for strict-mode sessions.
* **5-second blocked-page cache.** Per-tab, per-URL. Prevents redirect loops; means the
  block decision can be up to 5 s stale.
* **Incognito mode is opt-in.** Manifest doesn't pre-authorise incognito; users have to
  toggle "Allow in Incognito" in `chrome://extensions/` — matching the spec's consent model.
* **Firefox is out of scope.** Firefox uses `browser.*` APIs and a different native
  messaging registration; the spec treats it as a separate Phase 4 deliverable.
* **Extension ID is dev-time.** Unpacked extensions get a fresh ID per machine; the
  install script takes it as a parameter. The Phase 7 installer will use the Web Store
  fixed ID once the extension is published.

---

## Phase 5 — Strict-mode reinforcement, anti-bypass, settings lock

### Settings lock — `IStrictModeGuard`

A pure-logic guard lives in `StopMe.Core/Engines/StrictModeGuard.cs`:

| Action | Allowed when |
| --- | --- |
| `CanMutateBlocklist(blocklistId, active)` | No **strict** session is currently using this blocklist |
| `CanDeleteBlocklist(blocklistId, active)` | **No** active session (strict OR soft) uses this blocklist |
| `CanMutateSession(sessionId, active)` | The session is not active in strict mode |

The IPC handlers `UpdateBlocklistHandler` and `DeleteBlocklistHandler` consult the
guard against `ServiceState.Current.ActiveSessions`. When the guard denies they:

1. Return `Conflict` with the denial reason from the guard.
2. Persist a `BypassEvent` (`BlocklistEditAttempted` / `BlocklistDeleteAttempted`) so the
   user — and the Phase 6 statistics — see the attempt.

`CreateBlocklist` is intentionally **not** guarded: creating a new list doesn't affect
active rules. Session start / stop already had their strict-mode handling in Phase 1
(`SessionEngine.CanStopSessionAsync`) — Phase 5 leaves it untouched.

### Extension heartbeat

The MV3 extension uses `chrome.alarms` to send a `HEARTBEAT` over Native Messaging once a
minute. The native host forwards it to the new `extension.heartbeat` op. The service's
singleton `ExtensionHealthMonitor` keeps the last-seen timestamp per browser
(`chrome` / `edge` / `brave` / `opera`).

```jsonc
// extension → native host
{ "type": "HEARTBEAT", "browser": "chrome", "extensionId": "abc…", "extensionVersion": "0.1.0" }

// native host → service
{ "op": "extension.heartbeat",
  "payload": { "browser": "chrome", "extensionId": "abc…", "extensionVersion": "0.1.0" } }
```

`extension.health` returns the per-browser state (last heartbeat, seconds since, stale
flag). Threshold is configurable via `AntiBypassOptions.ExtensionStaleAfterSeconds`
(default 180s).

### Anti-bypass runner

`AntiBypassRunner` is invoked at the end of every `ServiceWorker.TickAsync`. It only
runs when at least one **strict** session is active — soft-mode sessions are deliberately
permissive.

Each tick the runner:

1. **Clock drift** — `ClockDriftDetector` compares the wall-clock delta since the last
   sample to a monotonic counter (`Environment.TickCount64` by default; injectable for
   tests). When the absolute drift exceeds `AntiBypassOptions.ClockDriftThresholdSeconds`
   (default 300s) it persists a `BypassEvent` (`ClockDrift`) and logs at warning level.
2. **Stale extension** — `ExtensionHealthMonitor.SnapshotStale(now)` returns browsers
   whose last heartbeat is older than the threshold. Each stale browser yields one
   `BypassEvent` (`ExtensionStale`). The runner de-dupes per browser-name so the same
   stale Chrome doesn't spam the log; the dedupe set clears when no strict session is
   active so the next session re-emits.

`BypassEvent` rows persist to the `BypassEvents` SQLite table (migration
`20260609122506_AddBypassEvents`); `bypass.events` exposes the most recent 100 entries
to the UI / extension.

### New IPC operations

| Op | Purpose |
| --- | --- |
| `rules.snapshot` | Returns the current compiled rule snapshot — Phase 6+ will feed it to `declarativeNetRequest.updateDynamicRules` for sub-resource blocking. |
| `extension.heartbeat` | Records a heartbeat against `ExtensionHealthMonitor`. |
| `extension.health` | Returns the per-browser health snapshot with `isStale` flags. |
| `bypass.events` | Returns the 100 most recent `BypassEvent` rows. |

### Phase 5 known limitations

* **No file-integrity watcher.** §14 lists `stopme.db` / `auth.token` watchdogs and a
  service-stop watchdog — the `BypassEventType` values
  (`CriticalFileTampered` / `ServiceStopAttempt` / `UnsupportedBrowser`) are reserved in
  the wire format but the detectors are deferred. The current runner only covers
  clock-drift and extension-stale detection.
* **No `declarativeNetRequest` push.** `rules.snapshot` returns the compiled snapshot,
  but the extension still uses `webNavigation.onBeforeNavigate` top-frame interception
  only. Subresources / XHR / iframes still pass.
* **No UI surface for bypass events.** The Phase 3 Dashboard does not call
  `bypass.events` yet. The endpoint is there for Phase 6 to render.
* **Strict-mode UX in the WinUI app is service-side only.** Settings page, Blocklists
  page etc. don't grey themselves out during an active strict session — they call the
  service and discover the lock by getting a `Conflict` error. Phase 6 / 7 will reflect
  the lock proactively in the UI.
* **Heartbeat timeout is 60s; tolerance is 180s by default.** Browser sleep / OS sleep
  can drop the alarm; the tolerance window is deliberately generous. Tighter detection
  needs a service-side push channel (Phase 6).

---

## Phase 6 — Statistics, privacy mode, CSV export

### Local stats pipeline

`UsageEvent` rows are the source of truth for every Statistics view. Three write
paths now feed the table:

| Origin | Event type | Notes |
| --- | --- | --- |
| `EvaluateUrlHandler` (extension → service) | `WebsiteBlocked` | Host extracted via `Uri.Host`; passes through the recorder. |
| `ProcessWatcher` (terminated apps) | `AppBlocked` | Replaces the Phase 2 direct repo write; same hashing now via recorder. |
| `SessionEngine.{Start,Stop,CompleteExpired}` | `SessionStarted` / `SessionCompleted` / `SessionCancelled` | Recorder gets the session name + ID. |

### `IUsageEventRecorder` — privacy-aware façade

`StopMe.Core/Engines/UsageEventRecorder.cs` reads the `stats.privacy_mode`
setting on each write and applies one of three filters:

| `StatsRetentionPolicy` | DisplayLabel | DomainHash / AppNameHash | Notes |
| --- | --- | --- | --- |
| `Readable` (default) | kept | kept | Top-distractions chart shows real names. |
| `Anonymized` | dropped | kept | Counts still accurate; labels gone. |
| `Disabled` | n/a | n/a | Skips the write entirely. |

Privacy mode is stored in the new `Settings` key/value table via
`SettingsRepository`. `IUsageEventRecorder.GetActivePolicyAsync` defaults to
`Readable` when no setting is present.

### `IStatsAggregator` — pure aggregation

`StopMe.Core/Engines/StatsAggregator.cs` takes the raw rows + completed
sessions and produces a `StatsSnapshot` per `StatsWindow` (Today / Last7Days /
Last30Days / AllTime). It computes:

* Summary: sessions completed, focus minutes, blocked counts, bypass count,
  productive-day streak (longest consecutive run).
* Daily buckets covering every day in the window — even empty days, so charts
  can render gaps without special-casing.
* Top distractions: grouped by `(Type, DisplayLabel)`, anonymous events
  skipped, sorted by count.

### New IPC operations

| Op | Purpose |
| --- | --- |
| `stats.summary` | Returns `StatsSummaryResponse` for the requested period. |
| `stats.export_csv` | Returns CSV text + suggested filename — UI writes to disk. |
| `stats.reset` | Wipes `UsageEvents` only (blocklists/sessions/settings kept). |
| `settings.get` | Returns one entry or all settings as a dict. |
| `settings.set` | Writes a key/value pair, returns the updated entry. |

### UI

* `StatisticsPage` (was a stub) — period selector, four summary cards (focus,
  sessions, website blocks, app blocks), two secondary cards (bypass, streak),
  top-distractions list, daily-buckets list, privacy selector, Export CSV,
  Reset.
* `DashboardPage` — adds an "Anti-bypass events" card pulling the 10 most
  recent `bypass.events` rows.

CSV exports land in `%UserProfile%\Documents\StopMe\Exports\` via
`DocumentsStatsCsvWriter`.

### Phase 6 known limitations

* **No DPAPI on persisted secrets.** Privacy mode + auth.token live in plain
  text on disk. Phase 8 follow-up will wrap them through `ProtectedData` for
  per-user encryption.
* **No declarativeNetRequest push to the extension.** Phase 5's `rules.snapshot`
  endpoint is still polling-only; sub-resource interception in the browser is
  unchanged.
* **No real-time push from service → UI.** The Statistics page is pull-only on
  Refresh; no SSE-style stream. Phase 8 may add a notification channel on the
  pipe.
* **CSV layout is fixed.** Three sections (daily / top / summary). Header
  comments at the top carry context. The exporter doesn't accept per-call
  shape overrides.
* **`SessionFailed` is not emitted.** `SessionEngine.StopSessionAsync` maps the
  `Error` reason to `SessionStatus.Failed` but does not emit a usage event for
  it — failures are rare and tracked separately via the service log.

---

## Phase 7 — Installer + ACL hardening

PowerShell scripts under `deploy/`:

| Script | Purpose |
| --- | --- |
| `publish.ps1` | `dotnet publish` Service / NativeHost / App into a layout `install.ps1` expects. |
| `install.ps1` | Elevated install: copy payloads, create `%ProgramData%\StopMe\` with a tight ACL, register the Windows service, register the Native Messaging manifest under HKLM. |
| `uninstall.ps1` | Elevated uninstall: refuses to run while a strict session is active (talks to the pipe directly), removes service + HKLM registrations + program files, optionally wipes data. |
| `check-install.ps1` | Read-only health check. PASS/FAIL/WARN per item; exit code 0 when every required check passes. |
| `install-nativehost.ps1` / `uninstall-nativehost.ps1` | Pre-existing HKCU-only dev workflow for unpacked extensions. |

`INSTALL.md` walks through the full install + verify + uninstall flow.

### ACL hardening

`StopMe.Service/Installation/TokenAclHardener.cs` runs at every service boot
and re-applies the `auth.token` ACL: `BUILTIN\Administrators` + `NT
AUTHORITY\SYSTEM` Full Control, `BUILTIN\Users` ReadAndExecute. `install.ps1`
applies the same matrix to the parent `%ProgramData%\StopMe\` directory before
the service starts — defense in depth.

### Phase 7 known limitations

* **No MSIX package.** The scripts are first-class, but a signed MSIX would
  let the WinUI app surface in Start menu searches and benefit from app
  identity. Deferred — the spec calls MSIX out as "optional packaging".
* **No tray icon yet.** The `ShowTrayIcon` settings toggle persists but no
  `H.NotifyIcon.WinUI` sink is wired in. Owned by a future Phase 8 follow-up;
  the build/test loop is more valuable than a fragile tray prototype.
* **`auth.token` is per-machine, not per-user.** Any local user (in
  `BUILTIN\Users`) can read it and impersonate the IPC client. The right
  long-term fix is DPAPI wrapping per local user — listed in Phase 8.
* **Service runs as LocalSystem.** Same as the Phase 2 default. A future
  managed-service-account install is a backlog item.
* **Uninstall pipe poke is best-effort.** If the service died mid-session,
  `uninstall.ps1` cannot tell whether a strict session was active and proceeds
  with the removal. `-Force` exists for the same reason.

---

## Phase 8 — Templates + Pomodoro/Internet abstractions

### Session templates

`StopMe.Core/Engines/TemplateCatalog.cs` ships six templates that match §31
of the spec:

| Key | Name | Duration | Mode | Lock | Default RRULE |
| --- | --- | --- | --- | --- | --- |
| `study` | Study Mode | 2 h | Soft | Confirmation | — |
| `work` | Work Mode | 4 h | Soft | Confirmation | `FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR` |
| `sleep` | Sleep Mode | 8 h | Strict | RandomText | `FREQ=DAILY` |
| `exam` | Exam Mode | 3 h | Strict | TimeLocked | — |
| `deep-work` | Deep Work | 90 min | Strict | TimeLocked | — |
| `writing` | Writing Mode | 2 h | Soft | Confirmation | — |

`ITemplateService.ApplyAsync(key, startUtc, userId)` materialises the recipe
into real `Blocklist` + `FocusSession` rows (one blocklist per
`TemplateBlocklist`, one session linking them via `SessionBlocklist`).
Categories are resolved against the seeded `CategoryDomains` — unknown ones
silently produce 0 rules so the template applies even if the user pruned a
category.

Two new IPC ops:

* `templates.list` → `TemplatesListResponse` with each template's metadata.
* `templates.apply { templateKey, startAtUtc }` → returns the created session
  ID and blocklist IDs.

### Pause / Pomodoro abstraction

`IPauseEngine` is the new contract for §16 pause logic:

* `NullPauseEngine` — default DI registration, treats every session as
  permanent Focus.
* `PomodoroPauseEngine` — pure state machine: given session start + now +
  policy (25/5/4/15 default), returns whether we're in Focus or Break and how
  far into each phase.

The Pomodoro engine is **not yet wired into the rule pipeline**; the spec is
clear that pause enforcement is a Phase 8 follow-up. The contract is there so
when `SessionEngine.CompleteExpiredSessionsAsync` learns to suspend rules
during Break, the wire format doesn't shift.

### Full-Internet provider abstraction

`IInternetBlockingProvider` reserves the §12 contract:

* `BrowserOnlyInternetBlockingProvider` (default) — no-op; relies on the
  existing extension `CHECK_URL` pipeline to enforce `BlocklistMode.FullInternet`.
* Future `WindowsFirewallInternetBlockingProvider` / WFP variants slot in
  without changing rule compilation.

### Sync preparation

`ISyncOutbox` + `SyncWorkItem` + `SyncMetadata` + `SyncOperation` +
`SyncStatus` are declared in `StopMe.Core.Abstractions`. **No implementation
is registered today** — the contract reserves the wire format so a future
backend doesn't force a breaking change.

### Templates page (WinUI)

`StopMe.App.Pages.TemplatesPage` (nav entry "Templates", inserted between
Blocklists and Active session) lists every template returned by
`templates.list`. Each card shows display name + description, mode / lock /
duration badges, a bullet preview of each `TemplateBlocklist` it would create
(e.g. `• Study — Distractions (Blacklist) — 4 categories`), and an "Apply now"
button.

The page also exposes a `CalendarDatePicker` + `TimePicker` (5-minute
increments) at the top. Leave them blank to apply the next click immediately;
fill both to queue the session for a specific local time — the VM combines
them, marks the value as `DateTimeKind.Local`, and converts to UTC before
calling `templates.apply`. An "Apply immediately" button clears both pickers.
The page banner reflects the resolved time on success
(`Applied 'Study Mode' for 2026-06-10 12:30 UTC — session <guid>, …`).

### Pomodoro pipeline (Phase 8 follow-up)

`PomodoroPauseEngine` is now wired end-to-end:

1. `SessionRepository.GetEnabledAsync` includes the navigated `PausePolicy`.
2. `SessionEngine.GetActiveSessionsAsync` calls `IPauseEngine.Evaluate` for
   every session with `AllowPauses && PausePolicy != null`, populating
   `ActiveSessionInstance.PausePhase` + `PausePhaseRemainingSeconds`.
3. `RuleCompiler.CompileForSessionsAsync` short-circuits any session whose
   `IsOnBreak` is true — its blocklists contribute no rules during Break.
4. `Mapping.ToDto` surfaces `pausePhase` + `pausePhaseRemainingSeconds` on
   `ActiveSessionDto`, so the UI and the browser extension can both render
   "On break — 4 min left" instead of "blocked".

`AddStopMeInfrastructure` now registers `PomodoroPauseEngine` as the default
`IPauseEngine`. The engine is safe as a global default: any session with a
non-Pomodoro `PauseType` (or with `AllowPauses == false`) is handled exactly
as if `NullPauseEngine` were registered (i.e. permanent Focus). Tests in
`PomodoroWiringTests.cs` cover both Focus and Break phases at the engine +
compiler boundaries.

### Phase 8 known limitations

* **Allowance is the only PauseType still unimplemented.** Pomodoro and the
  generic "no policy" path are wired (see *Pomodoro pipeline* below). Allowance
  needs a per-user activity tracker (foreground window + mouse/keyboard idle)
  before the engine can decide whether to count the minute.
* **No EF migration for sync fields.** `Blocklist` and `FocusSession` do NOT
  carry `RemoteId` / `LastModifiedUtc` columns yet. The plan is to ship those
  in the same migration as the V2 sync backend, not retrofit a column
  on an unused table.
* **No firewall / WFP provider.** Spec is explicit: V1 ships
  `BrowserOnly`. A signed driver is out of scope for the foreseeable future.

---

## Test totals

```
dotnet test --nologo
  Core      140 / 140
  Service    50 / 50
  App.Core   40 / 40
  NativeHost 15 / 15
  TOTAL     245 passing
```
