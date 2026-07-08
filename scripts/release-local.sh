#!/usr/bin/env bash
# Zero-CI-cost release from this machine: build → README row → tag → GitHub
# Release. Produces the same artifacts and bookkeeping as the CI release job,
# without spending a single Actions minute.
#
#   usage: ./scripts/release-local.sh
#
# TouchDown is a Blazor Server web app — the release artifact is a self-contained
# linux-x64 publish zip. No code signing or notarization applies (nothing mac or
# desktop ships from this repo; the org notary profile "codelifter" is unused here).
#
# Versioning matches CI conventions: BASE_VERSION + RELEASE_LEVEL repo variables;
# the patch is (highest existing v<BASE>.N tag) + 1. Don't mix local and CI releases
# within one BASE_VERSION unless you're sure the CI run counter is behind the tags.
set -euo pipefail
cd "$(dirname "$0")/.."

die() { echo "error: $*" >&2; exit 1; }

# ── preconditions ──────────────────────────────────────────────────
[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || die "release from main only"
git diff-index --quiet HEAD || die "uncommitted changes — commit or stash first"
git fetch -q --tags origin
git pull -q --ff-only origin main || die "local main has diverged from origin — reconcile first"

# ── version ────────────────────────────────────────────────────────
BASE="$(gh variable get BASE_VERSION 2>/dev/null || echo 0.9)"
LEVEL="$(gh variable get RELEASE_LEVEL 2>/dev/null || echo "")"
LAST_PATCH="$(git tag -l "v${BASE}.*" | sed -E "s/^v${BASE}\.([0-9]+).*$/\1/" | sort -n | tail -1)"
PATCH=$(( ${LAST_PATCH:-0} + 1 ))
VERSION="${BASE}.${PATCH}${LEVEL:+-$LEVEL}"
TAG="v${VERSION}"
echo "── releasing ${TAG} (base ${BASE}, level '${LEVEL}')"

# ── build ──────────────────────────────────────────────────────────
rm -rf publish dist
mkdir -p dist
dotnet publish TouchDown/TouchDown.csproj -c Release -r linux-x64 --self-contained \
  -p:Version="$VERSION" -o publish
ZIP="dist/TouchDown-${VERSION}-linux-x64.zip"
(cd publish && zip -qr "../${ZIP}" .)
rm -rf publish

[ -f "$ZIP" ] || die "expected artifact missing: $ZIP"

# ── README release-history row ─────────────────────────────────────
REPO_URL="https://github.com/CodeLifter-Platform/TouchDown"
DL="${REPO_URL}/releases/download/${TAG}"
NEW_ROW="| ${TAG} | $(date -u +%Y-%m-%d) | [zip](${DL}/TouchDown-${VERSION}-linux-x64.zip) | [Release notes](${REPO_URL}/releases/tag/${TAG}) |"
awk -v row="$NEW_ROW" '/\|[-]+\|[-]+\|[-]+\|[-]+\|/{print; print row; next} {print}' README.md > README.tmp \
  && mv README.tmp README.md

# ── commit, tag, push, release ─────────────────────────────────────
git add README.md
git commit -q -m "chore: release ${VERSION} [skip ci]"
git tag "$TAG"
git push -q origin main --tags

RELEASE_FLAGS=(--title "TouchDown ${TAG}" --generate-notes)
[ -n "$LEVEL" ] && RELEASE_FLAGS+=(--prerelease)
gh release create "$TAG" "$ZIP" "${RELEASE_FLAGS[@]}"

echo "── released: ${REPO_URL}/releases/tag/${TAG}"
