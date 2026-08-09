# Onboarding — Hosted / local .NET

Running TouchDown directly on a machine with the .NET SDK, without a container.

## Prerequisites

| Need | Version | Check |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |

Note this repo has **no `global.json`**, so the SDK version is not pinned — whatever .NET 10
SDK you have will be used. That is a gap, not a design choice.

## Get the code, build, run

```bash
git clone https://github.com/CodeLifter-Platform/TouchDown.git
cd TouchDown
dotnet run --project TouchDown
```

**What you should see:** Kestrel logs a listening URL; opening it gives the TouchDown UI.
The SQLite database is created on first run.

## Test

```bash
dotnet test
```

## Gotchas

- **UI state lives on the server.** Blazor Server keeps per-user state for the life of the
  SignalR circuit, so restarting the app resets every connected session. This surprises
  people expecting a client-side SPA.
- **The database is a file.** Deleting it resets the app; copying it moves the app's whole
  state. There is no migration story beyond EF Core's own.
- **Theming goes through MudBlazor.** The accent `#ff7a45` is the MudBlazor theme Primary,
  and token values come from `Platform-Standards/design/tokens.md` — the MudBlazor theme is
  a port of them, not an independent palette.
