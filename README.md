# TouchDown

A Blazor Server web app (.NET 10, MudBlazor, EF Core/SQLite) for orchestrating
agent teams and drives. Runs as a server — locally via `dotnet run`, or in a
container via the included `Dockerfile` / `docker-compose.yml`.

## Running

```sh
dotnet run --project TouchDown
# or
docker compose up --build
```

## Releasing

There are no manual tags — releases are produced by tooling, two ways:

- **`./scripts/release-local.sh` (default, $0 in CI minutes):** publishes the
  self-contained linux-x64 zip on the dev machine, adds the README row, tags, and
  publishes the GitHub Release. TouchDown ships a server artifact, so there is no
  code signing or notarization step.
- **Actions `workflow_dispatch` (on demand):** the full cloud pipeline
  (linux publish + release). Ordinary pushes and PRs run only the cheap linux
  build job — cloud artifact builds are opt-in.

Two repo variables control the version
(`Settings → Secrets and variables → Actions → Variables`):

- **`BASE_VERSION`** — major.minor (e.g. `0.9`); the patch is the workflow run number.
- **`RELEASE_LEVEL`** — `alpha` / `beta` / `rc`, or empty for stable. While set, main
  builds version as e.g. `0.9.42-beta` and the GitHub Release is flagged prerelease;
  clear it to ship clean versions like `1.0.57`.

Pushes to other branches (and PRs) build versioned artifacts
(`0.9.42-pre.42+abc1234`) but never release. The release job prepends a row to the
[release history](#release-history) table below, commits it as
`chore: release <version> [skip ci]`, tags `v<version>`, and publishes the GitHub
Release with generated notes.

## Release history

| Version | Date | Linux (x64) | Notes |
|---|---|---|---|
