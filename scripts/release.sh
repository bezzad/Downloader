#!/usr/bin/env bash
# Downloader — one-shot NuGet release routine.
#
# What it does, in order:
#   1. Asks you for the new version (or takes it as the first argument).
#   2. Bumps <Version> (and rewrites <PackageReleaseNotes>) in the library csproj on `develop`
#      and pushes.
#   3. Merges `develop` -> `master` (the default branch) and pushes `master`.
#   4. Tags `vX.Y.Z` on `master` and pushes the tag.
#   5. Packs the NuGet package (Release, to src/Downloader/bin/nupkg/ per this repo's convention).
#   6. Publishes the package to NuGet.org and to GitHub Packages (each individually skippable).
#   7. Creates the GitHub Release on the tag with the release notes (markdown) and attaches the
#      .nupkg/.snupkg.
#
# Usage:
#   scripts/release.sh                     # prompts for the version (suggests next patch)
#   scripts/release.sh 5.9.1                # non-interactive version
#   scripts/release.sh 5.9.1 --yes          # also skip the confirmation prompt
#   scripts/release.sh 5.9.1 --notes-file notes.md
#   scripts/release.sh 5.9.1 --skip-nuget-org        # e.g. no nuget.org API key available yet
#   scripts/release.sh 5.9.1 --skip-github-packages
#   scripts/release.sh 5.9.1 --skip-release          # skip creating the GitHub Release
#
# Requirements: git, dotnet, gh (authenticated: `gh auth status`).
# Secrets (as environment variables, not stored anywhere in this repo):
#   NUGET_API_KEY             - nuget.org API key (skip with --skip-nuget-org if unset)
#   GITHUB_PACKAGES_TOKEN     - a PAT with the `write:packages` scope (the `gh` CLI's own token
#                                usually does NOT have this scope). Skip with --skip-github-packages.
set -euo pipefail

REPO="bezzad/Downloader"
MAIN_BRANCH="master"
DEV_BRANCH="develop"
CSPROJ="src/Downloader/Downloader.csproj"
PACK_PROJECT="src/Downloader/Downloader.csproj"
PACK_OUTPUT="src/Downloader/bin/nupkg"
GITHUB_PACKAGES_SOURCE="https://nuget.pkg.github.com/bezzad/index.json"

# --- helpers ---------------------------------------------------------------
c_blue=$'\033[1;34m'; c_green=$'\033[1;32m'; c_red=$'\033[1;31m'; c_yellow=$'\033[1;33m'; c_off=$'\033[0m'
step() { echo "${c_blue}==>${c_off} $*"; }
ok()   { echo "${c_green}  \xE2\x9C\x93${c_off} $*"; }
warn() { echo "${c_yellow}  !${c_off} $*"; }
die()  { echo "${c_red}error:${c_off} $*" >&2; exit 1; }

# Portable in-place sed: GNU (Linux) takes -i with no arg, BSD (macOS) needs -i ''.
sedi() {
  if sed --version >/dev/null 2>&1; then sed -i "$@"; else sed -i '' "$@"; fi
}

# --- args --------------------------------------------------------------------
# Usage: release.sh [VERSION] [--yes] [--notes-file PATH] [--skip-nuget-org]
#                    [--skip-github-packages] [--skip-release]
VERSION=""; ASSUME_YES="no"; NOTES_FILE=""
SKIP_NUGET_ORG="no"; SKIP_GITHUB_PACKAGES="no"; SKIP_RELEASE="no"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --yes|-y) ASSUME_YES="yes" ;;
    --notes-file) shift; NOTES_FILE="${1:-}" ;;
    --skip-nuget-org) SKIP_NUGET_ORG="yes" ;;
    --skip-github-packages) SKIP_GITHUB_PACKAGES="yes" ;;
    --skip-release) SKIP_RELEASE="yes" ;;
    -*) die "unknown flag: $1" ;;
    *) [[ -z "$VERSION" ]] && VERSION="$1" || die "unexpected argument: $1" ;;
  esac
  shift
done

# Move to the repo root so relative paths work regardless of where it's invoked.
cd "$(git rev-parse --show-toplevel)" || die "not inside a git repository"

# --- preflight ---------------------------------------------------------------
step "Preflight checks"
command -v git    >/dev/null 2>&1 || die "git is required"
command -v dotnet >/dev/null 2>&1 || die "the .NET SDK ('dotnet') is required"
command -v gh     >/dev/null 2>&1 || die "the GitHub CLI 'gh' is required (https://cli.github.com)"
gh auth status >/dev/null 2>&1 || die "gh is not authenticated — run: gh auth login"
[[ -z "$(git status --porcelain)" ]] || die "working tree is dirty — commit or stash first"
if [[ "$SKIP_NUGET_ORG" != "yes" && -z "${NUGET_API_KEY:-}" ]]; then
  die "NUGET_API_KEY is not set — export it, or pass --skip-nuget-org to publish everything else now and push to nuget.org later"
fi
if [[ "$SKIP_GITHUB_PACKAGES" != "yes" && -z "${GITHUB_PACKAGES_TOKEN:-}" ]]; then
  die "GITHUB_PACKAGES_TOKEN is not set (needs the 'write:packages' scope) — export it, or pass --skip-github-packages"
fi
ok "tools present, gh authenticated, tree clean"

git fetch origin --tags --quiet
CUR_VERSION="$(sed -n 's|.*<Version>\([^<]*\)</Version>.*|\1|p' "$CSPROJ" | head -1)"
[[ -n "$CUR_VERSION" ]] || die "cannot read <Version> from $CSPROJ"
ok "current version: $CUR_VERSION"

# Suggest the next patch version.
IFS='.' read -r MA MI PA <<<"$CUR_VERSION"
SUGGEST="$MA.$MI.$((PA + 1))"

if [[ -z "$VERSION" ]]; then
  read -r -p "New version to release [$SUGGEST]: " VERSION
  VERSION="${VERSION:-$SUGGEST}"
fi
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "version must be MAJOR.MINOR.PATCH, got '$VERSION'"
TAG="v$VERSION"
git rev-parse -q --verify "refs/tags/$TAG" >/dev/null && die "tag $TAG already exists locally"
git ls-remote --exit-code --tags origin "$TAG" >/dev/null 2>&1 && die "tag $TAG already exists on origin"

# --- make sure develop is current and actually has something to ship -------
step "Syncing $DEV_BRANCH"
git checkout "$DEV_BRANCH" >/dev/null 2>&1 || die "cannot checkout $DEV_BRANCH"
git pull --ff-only origin "$DEV_BRANCH"
if git rev-parse -q --verify "origin/$MAIN_BRANCH" >/dev/null; then
  AHEAD="$(git rev-list --count "origin/$MAIN_BRANCH..$DEV_BRANCH")"
  [[ "$AHEAD" -gt 0 ]] || die "$DEV_BRANCH has no commits beyond $MAIN_BRANCH — nothing to release"
  ok "$DEV_BRANCH is $AHEAD commit(s) ahead of $MAIN_BRANCH"
fi

# --- release notes (mandatory: every release must say what changed) --------
# Sources, in order: --notes-file PATH -> interactive prompt -> commit subjects since the last tag.
NOTES_BODY=""
if [[ -n "$NOTES_FILE" ]]; then
  [[ -f "$NOTES_FILE" ]] || die "--notes-file not found: $NOTES_FILE"
  NOTES_BODY="$(cat "$NOTES_FILE")"
elif [[ "$ASSUME_YES" != "yes" ]]; then
  echo
  echo "  Write the release notes / highlights for $TAG (what changed for users)."
  echo "  Enter lines; finish with a single '.' on its own line:"
  while IFS= read -r line; do [[ "$line" == "." ]] && break; NOTES_BODY+="$line"$'\n'; done
fi
if [[ -z "${NOTES_BODY// /}" ]]; then
  warn "no notes given — falling back to commit subjects since the last tag"
  NOTES_BODY="$(git log --no-merges --pretty='- %s' "v$CUR_VERSION..$DEV_BRANCH" 2>/dev/null | head -40)"
fi
[[ -n "${NOTES_BODY// /}" ]] || die "release notes are required and could not be derived — re-run with --notes-file"

echo
echo "  Release plan:"
echo "    version : $CUR_VERSION -> $VERSION   (tag $TAG)"
echo "    merge   : $DEV_BRANCH -> $MAIN_BRANCH"
echo "    publish : $( [[ "$SKIP_NUGET_ORG" == "yes" ]] && echo "nuget.org (SKIPPED)" || echo "nuget.org" ) · $( [[ "$SKIP_GITHUB_PACKAGES" == "yes" ]] && echo "GitHub Packages (SKIPPED)" || echo "GitHub Packages" ) · $( [[ "$SKIP_RELEASE" == "yes" ]] && echo "GitHub Release (SKIPPED)" || echo "GitHub Release" )"
echo "    notes   : $(printf '%s' "$NOTES_BODY" | head -1)…"
echo
if [[ "$ASSUME_YES" != "yes" ]]; then
  read -r -p "Proceed? [y/N] " reply
  [[ "$reply" =~ ^[Yy]$ ]] || die "aborted"
fi

# --- 1. bump the version + release notes on develop -------------------------
step "Bumping version to $VERSION on $DEV_BRANCH"
if [[ "$CUR_VERSION" != "$VERSION" ]]; then
  sedi "s|<Version>$CUR_VERSION</Version>|<Version>$VERSION</Version>|" "$CSPROJ"
  grep -q "<Version>$VERSION</Version>" "$CSPROJ" || die "failed to bump <Version> in $CSPROJ"
else
  warn "<Version> already $VERSION — no version-tag change"
fi

# Rewrite the <PackageReleaseNotes> block's inner content in place (preserves the open/close tags).
awk -v notes="$NOTES_BODY" '
  /<PackageReleaseNotes>/ { print; print notes; skip=1; next }
  /<\/PackageReleaseNotes>/ { skip=0 }
  skip { next }
  { print }
' "$CSPROJ" > "$CSPROJ.tmp" && mv "$CSPROJ.tmp" "$CSPROJ"
grep -q "<PackageReleaseNotes>" "$CSPROJ" || die "PackageReleaseNotes block went missing from $CSPROJ — aborting before commit"

if [[ -n "$(git status --porcelain "$CSPROJ")" ]]; then
  git add "$CSPROJ"
  git commit -q -m "chore(release): bump version to $VERSION"
  git push origin "$DEV_BRANCH"
  ok "committed + pushed version bump"
else
  warn "no changes to $CSPROJ — skipping bump commit"
fi

# --- 2. merge develop -> master ----------------------------------------------
step "Merging $DEV_BRANCH -> $MAIN_BRANCH"
git checkout "$MAIN_BRANCH"
git pull --ff-only origin "$MAIN_BRANCH" || true
git merge --no-ff "$DEV_BRANCH" -m "release: $TAG (merge $DEV_BRANCH)"
git push origin "$MAIN_BRANCH"
ok "$MAIN_BRANCH updated"

# --- 3. tag + push -----------------------------------------------------------
step "Tagging $TAG"
git tag -a "$TAG" -m "Downloader $TAG"
git push origin "$TAG"
ok "pushed $TAG"

# --- 4. pack the NuGet package -----------------------------------------------
step "Packing $PACK_PROJECT (Release) -> $PACK_OUTPUT"
rm -rf "$PACK_OUTPUT"
dotnet pack "$PACK_PROJECT" -c Release -o "$PACK_OUTPUT"
NUPKG="$PACK_OUTPUT/Downloader.$VERSION.nupkg"
[[ -f "$NUPKG" ]] || die "expected package not found: $NUPKG"
ok "packed $NUPKG"

# --- 5. publish to nuget.org --------------------------------------------------
if [[ "$SKIP_NUGET_ORG" != "yes" ]]; then
  step "Publishing to nuget.org"
  dotnet nuget push "$NUPKG" --source nuget.org --api-key "$NUGET_API_KEY" --skip-duplicate
  ok "published to nuget.org"
else
  warn "skipped nuget.org publish (--skip-nuget-org)"
fi

# --- 6. publish to GitHub Packages -------------------------------------------
if [[ "$SKIP_GITHUB_PACKAGES" != "yes" ]]; then
  step "Publishing to GitHub Packages"
  dotnet nuget push "$NUPKG" --source "$GITHUB_PACKAGES_SOURCE" --api-key "$GITHUB_PACKAGES_TOKEN" --skip-duplicate
  ok "published to GitHub Packages"
else
  warn "skipped GitHub Packages publish (--skip-github-packages)"
fi

# --- 7. create the GitHub Release --------------------------------------------
if [[ "$SKIP_RELEASE" != "yes" ]]; then
  step "Creating GitHub Release $TAG"
  NOTES_TMP="$(mktemp)"
  printf '%s\n' "$NOTES_BODY" > "$NOTES_TMP"
  ASSETS=("$NUPKG")
  SNUPKG="$PACK_OUTPUT/Downloader.$VERSION.snupkg"
  [[ -f "$SNUPKG" ]] && ASSETS+=("$SNUPKG")
  gh release create "$TAG" --repo "$REPO" --title "$TAG" --notes-file "$NOTES_TMP" "${ASSETS[@]}"
  rm -f "$NOTES_TMP"
  ok "GitHub Release $TAG created"
else
  warn "skipped GitHub Release (--skip-release)"
fi

# --- done ----------------------------------------------------------------
echo
ok "Release $TAG is out."
echo
echo "  Verify:"
echo "    GitHub Release   : gh release view $TAG --repo $REPO"
echo "    nuget.org        : https://www.nuget.org/packages/Downloader/$VERSION"
echo "    GitHub Packages  : https://github.com/bezzad/Downloader/pkgs/nuget/Downloader"
echo
echo "  Remember: update PLAN.md/TASKS.md on develop to record this release (per CLAUDE.md)."
