# TouchDown — Living Spec

The current state of the application. Maintained alongside the code: a change that alters
what TouchDown does updates this file in the same commit.

## What it is

TouchDown is a **Blazor Server web app for orchestrating agent teams and drives**. It runs
as a server — locally via `dotnet run`, or in a container using the included `Dockerfile`
and `docker-compose.yml`. Unlike the desktop apps on the platform, there is nothing to
install: you run it and open a browser.

## Status

**Beta.** `BASE_VERSION` 0.9, `RELEASE_LEVEL` beta.

## Features

- **Agent team and drive orchestration** through a Blazor Server UI, with SignalR hubs for
  live updates (`Hubs/`).
- **MudBlazor component library** as the UI toolkit, with the CodeLifter accent
  `#ff7a45` carried as the MudBlazor theme Primary.
- **MVVM structure** (`MVVM/`, `Areas/`, `Components/`) rather than code-behind pages.
- **Dark and light themes**, both deliberate, with a persisted runtime toggle. The
  dark-only exception recorded for CodeLifter.Net does not apply here.

## Architecture

A single ASP.NET Core project (`TouchDown/`) using Blazor Server's InteractiveServer render
mode, so UI state lives on the server and the browser holds a SignalR circuit. EF Core over
SQLite for persistence.

`Program.cs` is the composition root. `RULES.md` inside the project carries app-specific
conventions.

## Data and state

SQLite via EF Core. In a container the database must live on a mounted volume — anything
written inside the container image is lost on restart.

Because this is Blazor **Server**, per-user UI state is held server-side for the life of the
SignalR circuit. A dropped connection or a server restart resets that state; it is not a
client-side app that survives a reload with its state intact.

## External services

GitHub only. The platform service registry records TouchDown as needing no `SERVICES.md`.

## Platform matrix

| Target | Ships | Format |
|---|---|---|
| Container | ✅ | `Dockerfile` + `docker-compose.yml` |
| Any .NET 10 host | ✅ | `dotnet run` / published output |

There are no desktop or mobile clients. It is a server application, and "platform" here
means where you host it.

## Known gaps

- **No `global.json`, `Directory.Build.props`, or `Directory.Packages.props`.** This repo
  is one of those the conformance checker flags for missing .NET pins, so the SDK version
  and package versions are not centrally controlled. Tracked in
  `Platform-Standards/FOLLOWUPS.md`.
- **Data Protection keys.** If this app grows authentication, the keys need persisting to
  the mounted volume the way StageZero does it — otherwise every container restart
  invalidates auth cookies and antiforgery tokens. Worth doing before it matters rather
  than after.
