# Onboarding — Docker

The intended way to run TouchDown.

## Prerequisites

| Need | Version | Check |
|---|---|---|
| Docker | any recent | `docker info` |
| Docker Compose | v2 (bundled with Docker) | `docker compose version` |

## Get the code and run

```bash
git clone https://github.com/CodeLifter-Platform/TouchDown.git
cd TouchDown
docker compose up --build
```

**What you should see:** the app builds, the container starts, and the site answers on the
mapped port. Open it in a browser and you should get the TouchDown UI, not an error page.

## Gotchas

- **Persist the database on a volume.** SQLite written inside the container image is lost
  the moment the container is replaced. `docker-compose.yml` is the place to get this
  right; a bare `docker run` without a volume will look like it works until the first
  restart.
- **Blazor Server needs a working WebSocket connection.** If you put a proxy in front of
  it, forward the SignalR circuit or the UI will load and then go inert — which reads as
  "the app is broken" rather than "the proxy dropped the socket".
- **There is a `Dockerfile.debug` and a `docker-compose.debug.yml`** alongside the
  production pair. Use those for development; the default pair is the production shape.
