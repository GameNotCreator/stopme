# StopMe

A Windows focus application for managing distraction-blocking sessions.

StopMe combines a desktop interface, a Windows service and a Chromium browser extension. Rules and session data are processed locally, with SQLite persistence.

**Status:** development project. Read the installation guide and the documented limitations before installing.

## Features

- Blocklists for websites and Windows applications.
- Scheduled focus sessions, recurring rules and session templates.
- Soft and strict session modes.
- Local activity statistics and CSV export.
- A WinUI 3 interface and a Chromium extension connected through a Native Messaging host.

## Architecture

| Component | Role |
| --- | --- |
| `StopMe.Core` | Domain models, rule evaluation and session logic |
| `StopMe.Infrastructure` | Entity Framework Core and SQLite persistence |
| `StopMe.LocalApi` | Local Named Pipes protocol and client |
| `StopMe.Service` | Windows service and process monitoring |
| `StopMe.App.Core` | View models and UI services |
| `StopMe.App` | WinUI 3 desktop application |
| `StopMe.NativeHost` | Bridge between the browser extension and service |
| `extension/chromium/` | Chromium Manifest V3 extension |

## Build and test

Use a Windows x64 development environment with the **.NET 8 SDK** and the Windows/WinUI build prerequisites described in [INSTALL.md](INSTALL.md).

From the repository root:

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet test
```

The test projects cover the core, service, application view models and Native Messaging host. Run them against the checked-out revision to obtain current results; historical test totals are not a current build guarantee.

## Install

Follow [the Windows installation guide](INSTALL.md) for publishing, service registration, browser extension setup, verification and removal.

The installation scripts configure a Windows service and Native Messaging registrations. Read the guide before running them with administrator privileges.

## Scope and limitations

StopMe is a productivity tool. Strict mode is not a security boundary against a machine administrator.

Full network isolation through a Windows Firewall or WFP provider is not part of the documented implementation. Review the local authentication, platform support and browser-enforcement limitations in the technical notes before relying on a particular blocking mode.

## Documentation

- [Installation and troubleshooting](INSTALL.md)
- [Technical specification](TECHNICAL_SPEC.md)
- [Development history and implementation notes](DEVELOPMENT-NOTES.md)
- [Audit findings](AUDIT.md)
- [Code review](REVIEW.md)
- [Review fixes](CHANGELOG-REVIEW-FIXES.md)

The audit, review and development notes record different points in the project's history. Consult the corresponding fixes and current source when checking the status of a finding.
