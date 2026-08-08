# CLAUDE.md — TouchDown

> **Platform standards:** this repo follows the CodeLifter harness in the sibling
> `Platform-Standards` repo (`../Platform-Standards/HARNESS.md` locally, loaded
> automatically via the folder-level CLAUDE.md symlink; otherwise
> `github.com/CodeLifter-Platform/Platform-Standards`). If that file isn't on disk —
> CI, cloud, or a lone clone — fetch it before doing UI, architecture, or CI work.

<!-- App-specific rules only. Platform-wide standards live in the harness. -->

TouchDown is a Blazor Server web app (.NET 10, MudBlazor, EF Core/SQLite) for
orchestrating agent teams and drives. It runs as a server: locally via `dotnet run`, or in
a container via the included `Dockerfile` / `docker-compose.yml`.

## Quick start

```bash
dotnet run --project TouchDown
```

```bash
docker compose up --build
```

## App-specific notes

- **MudBlazor is the theming surface.** TouchDown carries its accent `#ff7a45` as the
  MudBlazor theme Primary. Token values still come from
  `Platform-Standards/design/tokens.md` — the MudBlazor theme is a port of them, not an
  independent palette.
- **Web app, but both themes are required.** The dark-only exception in the harness is
  recorded for CodeLifter.Net alone; TouchDown ships a deliberate light palette and a
  persisted runtime toggle like every other app.
