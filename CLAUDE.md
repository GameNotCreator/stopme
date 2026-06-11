# Claude context — StopMe

This file is auto-loaded by Claude Code in this project. Keep it current; don't bloat it.

## What is StopMe

A privacy-first Windows distraction blocker, built per a long spec the user provided. Decisions are local; nothing leaves the machine. Multi-phase build:

| Phase | Status | Owns |
| --- | --- | --- |
| 1 | ✅ | `StopMe.Core` (domain) + `StopMe.Infrastructure` (EF Core / SQLite) |
| 2 | ✅ | `StopMe.LocalApi` (IPC contract) + `StopMe.Service` (worker, IPC server, process watcher) |
| 3 | ✅ | `StopMe.App.Core` (ViewModels) + `StopMe.App` (WinUI 3 shell + 7 pages) |
| 4 | ✅ | `StopMe.NativeHost` (stdio bridge) + `extension/chromium/` (MV3 service worker, popup, blocked page) + `deploy/` install scripts |
| 5 | ✅ | Strict-mode guard + write locks, extension heartbeat, BypassEvent + AntiBypassRunner (clock drift + stale extension), `rules.snapshot` / `extension.{heartbeat,health}` / `bypass.events` ops |
| 6 | ✅ | `IUsageEventRecorder` (privacy modes), `StatsAggregator`, `stats.*` + `settings.*` IPC, Statistics WinUI page, bypass-events card on Dashboard |
| 7 | ✅ | PowerShell `install.ps1` / `uninstall.ps1` / `check-install.ps1` / `publish.ps1`, INSTALL.md, `TokenAclHardener` at every service boot — MSIX + tray icon explicitly deferred |
| 8 | 🟡 | Templates (6 defaults + WinUI page + `CalendarDatePicker`/`TimePicker` for scheduled start) + Pomodoro wired end-to-end (`SessionEngine` → `ActiveSessionInstance.PausePhase` → `RuleCompiler` skips Break; default `IPauseEngine` is `PomodoroPauseEngine`) + `IInternetBlockingProvider` + `ISyncOutbox` (contracts only). Allowance + firewall/WFP still deferred. |
| review | ✅ | **2026-06-10 batch**: Connecting-bug fix (DispatcherQueue marshal + TokenStore.TryLoad), PBKDF2-SHA512 password hashing, RRULE COUNT enforced, UrlRuleEngine/SessionRepository TimeProvider, clock-drift bypass prevention (ServiceState flag + IClockDriftMonitor), 3 new IPC ops (`processes.snapshot` / `sessions.create` / `blocklists.add_rule`), new `BlockAppsPage`, real `PlannerPage`, Dashboard quick-start. **272 tests passing**. auth.token DPAPI still deferred. See `CHANGELOG-REVIEW-FIXES.md`. |

The README has the architecture, build commands, and known limitations per shipped phase. **Read it before doing surgery on existing code.** Spec quirks (rule-priority order, recurrence semantics, lock-policy matrix) are encoded in tests — those are the source of truth.

## Layering rules (don't break these)

```
StopMe.Core               pure .NET 8, no infra / no UI
StopMe.Infrastructure     EF Core, SQLite, repositories — implements Core abstractions
StopMe.LocalApi           IPC contract; depends only on Core enum mirrors
StopMe.Service            net8.0-windows; Worker host; references Core + Infra + LocalApi
StopMe.App.Core           pure .NET 8 ViewModels; no WinUI; references Core + LocalApi
StopMe.App                net8.0-windows10.0.26100.0, x64; WinUI 3 host
StopMe.NativeHost         net8.0 console exe; forwards Chrome native-messaging frames to the service
extension/chromium/       MV3 service worker, popup, blocked page — vanilla JS / HTML / CSS
deploy/                   PowerShell install / uninstall scripts for the native-messaging host
```

## Build

```bash
dotnet build                                                # everything except WinUI
dotnet build src/StopMe.App/StopMe.App.csproj -p:Platform=x64   # WinUI app
dotnet test --nologo --no-build                            # all tests
```

WinUI gotchas the build dance taught us — captured in detail in the memory file `build_conventions.md`:
- Always pass `-p:Platform=x64` for `StopMe.App`.
- Use **WinAppSDK 2.1.3** (not the 1.x line — XamlCompiler.exe crashes silently).
- `TextBlock` has no `LetterSpacing` / `TextTransform`. Use `CharacterSpacing` + uppercase strings.
- `NavigationView.PaneTitleTemplate` doesn't exist — use `PaneHeader`.

## Conventions

- **Central package management.** Versions in `Directory.Packages.props`. csproj files have no versions.
- **Warnings as errors.** Enabled globally in `Directory.Build.props`; `StopMe.App` opts out (WinUI generated code trips analyzers).
- **Nullable enabled** everywhere.
- **Tests**: xUnit + FluentAssertions. In-memory SQLite for repo tests. `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) for time. Fakes live in `tests/<Project>/Fakes`.
- **Memory at `~/.claude/projects/.../memory/`** is updated every phase. Check `phase_state.md` and the others when starting work.

## How the user collaborates

- Senior engineer at EPFL. French-primary; code in English. Don't over-explain.
- Expects deep deliverables per phase, with explicit "known limitations" — never hidden TODOs.
- Doesn't want emojis in code or commit messages. Tray icon implementations should not assume them either.
- The repo is intentionally not `git init`-ed. Do not commit unless explicitly asked.
- Says *"a toi de jouer"* / *"ne me décois pas"* — go drive the work, report concrete numbers at the end.

## Where to look first when something breaks

1. `dotnet --list-sdks` shows 8.0.x?
2. WinUI failing? `dotnet test --nologo --no-build` first to localise (Core / Service / App.Core all build to net8.0; only `StopMe.App` is the WinUI surface).
3. XamlCompiler silent failure? Use the official template (`dotnet new winui`) in a scratch dir to confirm the toolchain.
4. Service startup smoke: `dotnet run --no-build --project src/StopMe.Service -- --data-dir C:\temp\stopme-smoke` then watch `Logs\stopme-service-YYYYMMDD.log`.
