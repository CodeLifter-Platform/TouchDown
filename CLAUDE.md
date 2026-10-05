# CLAUDE.md — TouchDown

> **Platform standards:** this repo follows the CodeLifter harness in the sibling
> `Platform-Standards` repo (`../Platform-Standards/HARNESS.md` locally, loaded
> automatically via the folder-level CLAUDE.md symlink; otherwise
> `github.com/CodeLifter-Platform/Platform-Standards`). If that file isn't on disk —
> CI, cloud, or a lone clone — fetch it before doing UI, architecture, or CI work.
>
> **Design system:** new UI is built from the **CodeLifter Design System** in the sibling
> `Platform-Design` repo (`../Platform-Design/readme.md` locally; otherwise
> `github.com/CodeLifter-Platform/Platform-Design`). Read `readme.md`, then `tokens/`, then
> the component's `.prompt.md` — before any markup. If it doesn't have the component, token,
> accent, or pattern the work needs, stop and ask for it to be added — don't invent or
> approximate one. Rules: `Platform-Standards/design/design-system.md`.

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
- **No authentication, by design.** TouchDown is a single-user app on a trusted host. Every
  page and the SignalR hub are open to anything that can reach the port, including the
  hub's client-callable `SendLog` / `UpdateAgentStatus` / `DriveCompleted` methods, which
  let any connected client write to another viewer's board. That is the recorded trusted-host
  posture, not an oversight: do not expose the port beyond a network you trust, and do not
  add per-route auth piecemeal. The one closed surface is the Hangfire dashboard (off outside
  Development, loopback-only wherever it is on), because it can trigger and delete jobs.

## Testing

Run the suite locally (about a minute; git must have `user.email` / `user.name` set):

```bash
dotnet build TouchDown.sln -c Release
dotnet test tests/TouchDown.Tests -c Release --no-build
```

How the rows of `Platform-Standards/process/testing.md` map onto `tests/TouchDown.Tests`:

| Row | Where |
|---|---|
| Persistence | `PersistenceRoundTripTests` (every field of every entity, upsert, cascades), `DrivesNewServiceDATests`, `TeamsCrudTests`, `DataAccessTests`, `MigrationTests` (chain applies, model matches snapshot), `DatabaseMigratorTests` (a legacy EnsureCreated database upgrades in place) |
| Wire contracts | `AgentHubWireContractTests`: the real hub hosted in-process, every message the orchestrator publishes read back by a real client with the property names the monitor page uses. The hub is the only wire contract; there is no HTTP API client. |
| Auth and trust boundaries | Mostly N/A: there is no credential to refuse (see the posture note above). The one closed route is covered by `HangfireDashboardHostedTests` (absent in Production, open to a local caller, refused for a remote one) and `HangfireDashboardFilterTests`. |
| Parsers and importers | `ClaudeStreamParserTests` (stream-json), `CodexParserTests` (codex exec), `PlanParserServiceTests` / `PlanExtractionTests` / `PlanSourceTests` (the Quarterback's plan). The CLI fixtures are reconstructed from the parsed shapes, not captured transcripts; swap in a capture when one exists. |
| User-facing rules | `TeamsIndexPageVMTests`, `DrivesNewPageVMTests`, `HuddleVMTests`, `TeamsCrudTests` |
| Failure paths | `OrchestratorFailurePathTests` (every turnover reason, a failed play, cancellation), `TeamsIndexPageVMTests` (a dead service keeps the editor and the edits), `DrivesNewServiceTests` (a provider error reaches the screen), `TelemetryServiceTests` |
| Secrets | **N/A.** TouchDown holds no secrets of its own: no credentials, keys or tokens. The agent CLIs keep their own authentication outside the app, and nothing here reads or stores it. |
| Startup guards | `TelemetryStartupGuardTests` (a bad `Telemetry:OtlpEndpoint` refuses to start, naming the setting), `UserPreferencesServiceTests` (a corrupt preferences file never blocks a launch), `StartupTests`, `ProcessStartupTests` |
| The integration canary | `DriveCanaryTests`: the shipped wiring in-process (New Drive service → orchestrator → plan parser → SQLite → SignalR client), with only the model CLI faked |

Platforms: the app ships only as a Linux container (`PLATFORMS.md`), so `tests.yml` runs the
suite on ubuntu with coverage printed into the job summary, then builds the image and
smoke-tests it (start on a named volume, `GET /` is 200, `/health` is 200 or 503, restart,
the database file survived). There are no macOS or Windows legs because nothing is packaged
for those platforms.

Known gaps, deliberately: the monitor page opens its SignalR connection while prerendering,
so `/drive/{id}` cannot be rendered inside TestServer and is not GET-tested in-process; and
the suite is one project rather than the per-layer layout the standard prefers, which is a
follow-up.
