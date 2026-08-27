# Services

The external services this application depends on and where they're managed. Update this
file in the same change that adds, removes, or reconfigures a service. Platform-wide map:
`Platform-Standards/services/registry.md` (sibling repo,
github.com/CodeLifter-Platform/Platform-Standards).

**This inventory is incomplete.** It was started on 2026-08-26 when the release notes
push was added, and covers only that. TouchDown's AI provider, database, and container
dependencies still need auditing into it.

Last reviewed: 2026-08-26.

## Release notes API (codelifter.net)

- **Usage:** `.github/workflows/release-notes.yml` pushes the pull requests each release
  shipped to `POST https://codelifter.net/api/releases` as app `touchdown`. The site
  curates them into a user-facing feature list and posts the note into TouchDown's
  **Release Notes** thread in the forum. Nothing here writes release notes by hand.
- **Managed at:** the repo secret `RELEASE_NOTES_TOKEN` — this app's own push token, minted
  on the site with `npm run release-tokens -- mint touchdown` and stored in 1Password as
  `TouchDown — RELEASE_NOTES_TOKEN`. Not an org secret: a token only ever writes one app's
  notes.
- **Fails soft:** no token, or an API that is down, produces a warning annotation, never a
  failed release.
- **Dates of interest:** this repo still publishes a *full* release on every merge into
  main (retired `BASE_VERSION` scheme, `RELEASE_LEVEL` unset), so it will post a forum note
  per merge until it moves to the tag-based scheme. Park it with
  `npm run release-tokens -- policy touchdown manual` in the meantime.
- **Detail:** `Platform-Standards/process/release-notes.md`.
