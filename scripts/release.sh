#!/usr/bin/env bash
# Downloader — one-shot NuGet release routine.
#
# What it does, in order:
#   1. Asks you for the new version (or takes it as the first argument).
#   2. Bumps <Version> (and rewrites <PackageReleaseNotes>) in the library csproj on `develop`
#      and pushes.
#   3. Merges `develop` -> `master` (the default branch) and pushes `master`.
#   4. Tags `vX.Y.Z` on `master` and pushes the tag. The tag push triggers
#      .github/workflows/release.yml, which packs the NuGet package and publishes it to
#      nuget.org + GitHub Packages using the repo's Actions secrets (NUGET_API_KEY,
#      PACKAGE_TOKEN — this script never sees or needs those values), then attaches the
#      .nupkg/.snupkg to the GitHub Release for the tag.
#   5. Waits for that workflow run to finish, then sets the curated release notes (your
#      highlights + GitHub's auto-generated "What's Changed") on the release it created.
#
# Usage:
#   scripts/release.sh                     # prompts for the version (suggests next patch)
#   scripts/release.sh 5.9.1                # non-interactive version
#   scripts/release.sh 5.9.1 --yes          # also skip the confirmation prompt
#   scripts/release.sh 5.9.1 --notes-file notes.md
#   scripts/release.sh 5.9.1 --skip-wait    # push the tag and exit; don't wait on CI / set notes
#
# Requirements: git, dotnet (only used locally for the release-notes/version bump — packing and
# publishing happen in CI), gh (authenticated: `gh auth status`).
set -euo pipefail

REPO="bezzad/Downloader"
MAIN_BRANCH="master"
DEV_BRANCH="develop"
CSPROJ="src/Downloader/Downloader.csproj"
WORKFLOW_FILE="release.yml"

# --- helpers -----------------------------------------------------------------
c_blue=$'\033[1;34m'; c_green=$'\033[1;32m'; c_red=$'\033[1;31m'; c_yellow=$'\033[1;33m'; c_off=$'\033[0m'
step() { echo "${c_blue}==>${c_off} $*"; }
ok()   { echo "${c_green}  \xE2\x9C\x93${c_off} $*"; }
warn() { echo "${c_yellow}  !${c_off} $*"; }
die()  { echo "${c_red}error:${c_off} $*" >&2; exit 1; }

# Portable in-place sed: GNU (Linux) takes -i with no arg, BSD (macOS) needs -i ''.
sedi() {
  if sed --version >/dev/null 2>&1; then sed -i "$@"; else sed -i '' "$@"; fi
}

# --- args ----------------------------------------------------------------------
# Usage: release.sh [VERSION] [--yes] [--notes-file PATH] [--skip-wait]
VERSION=""; ASSUME_YES="no"; NOTES_FILE=""; SKIP_WAIT="no"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --yes|-y) ASSUME_YES="yes" ;;
    --notes-file) shift; NOTES_FILE="${1:-}" ;;
    --skip-wait) SKIP_WAIT="yes" ;;
    -*) die "unknown flag: $1" ;;
    *) [[ -z "$VERSION" ]] && VERSION="$1" || die "unexpected argument: $1" ;;
  esac
  shift
done

# Move to the repo root so relative paths work regardless of where it's invoked.
cd "$(git rev-parse --show-toplevel)" || die "not inside a git repository"

# --- preflight -----------------------------------------------------------------
step "Preflight checks"
command -v git    >/dev/null 2>&1 || die "git is required"
command -v dotnet >/dev/null 2>&1 || die "the .NET SDK ('dotnet') is required"
command -v gh     >/dev/null 2>&1 || die "the GitHub CLI 'gh' is required (https://cli.github.com)"
gh auth status >/dev/null 2>&1 || die "gh is not authenticated — run: gh auth login"
[[ -z "$(git status --porcelain)" ]] || die "working tree is dirty — commit or stash first"
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

# --- make sure develop is current and actually has something to ship -----------
step "Syncing $DEV_BRANCH"
git checkout "$DEV_BRANCH" >/dev/null 2>&1 || die "cannot checkout $DEV_BRANCH"
git pull --ff-only origin "$DEV_BRANCH"
if git rev-parse -q --verify "origin/$MAIN_BRANCH" >/dev/null; then
  AHEAD="$(git rev-list --count "origin/$MAIN_BRANCH..$DEV_BRANCH")"
  [[ "$AHEAD" -gt 0 ]] || die "$DEV_BRANCH has no commits beyond $MAIN_BRANCH — nothing to release"
  ok "$DEV_BRANCH is $AHEAD commit(s) ahead of $MAIN_BRANCH"
fi

# --- release notes (mandatory: every release must say what changed) ------------
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
echo "    publish : nuget.org + GitHub Packages (via .github/workflows/$WORKFLOW_FILE, triggered by the tag push)"
echo "    notes   : $(printf '%s' "$NOTES_BODY" | head -1)…"
echo
if [[ "$ASSUME_YES" != "yes" ]]; then
  read -r -p "Proceed? [y/N] " reply
  [[ "$reply" =~ ^[Yy]$ ]] || die "aborted"
fi

# --- 1. bump the version + release notes on develop -----------------------------
step "Bumping version to $VERSION on $DEV_BRANCH"
if [[ "$CUR_VERSION" != "$VERSION" ]]; then
  sedi "s|<Version>$CUR_VERSION</Version>|<Version>$VERSION</Version>|" "$CSPROJ"
  grep -q "<Version>$VERSION</Version>" "$CSPROJ" || die "failed to bump <Version> in $CSPROJ"
else
  warn "<Version> already $VERSION — no version-tag change"
fi

# XML-escape the notes before injecting them into the csproj — a literal &, < or > in the notes
# would otherwise produce a malformed csproj and fail `dotnet pack`. Use sed (not bash ${//}, whose
# `&` handling differs between bash 5.2+ patsub_replacement and the old bash 3.2 on macOS); escape
# `&` first. In sed's replacement `&` means the match, so a literal ampersand is written as `\&`.
NOTES_XML="$(printf '%s' "$NOTES_BODY" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g')"

# Rewrite the <PackageReleaseNotes> block's inner content in place (preserves the open/close tags).
awk -v notes="$NOTES_XML" '
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

# --- 2. merge develop -> master --------------------------------------------------
step "Merging $DEV_BRANCH -> $MAIN_BRANCH"
git checkout "$MAIN_BRANCH"
git pull --ff-only origin "$MAIN_BRANCH" || true
git merge --no-ff "$DEV_BRANCH" -m "release: $TAG (merge $DEV_BRANCH)"
git push origin "$MAIN_BRANCH"
ok "$MAIN_BRANCH updated"

# --- 3. tag + push (triggers release.yml) ----------------------------------------
step "Tagging $TAG"
git tag -a "$TAG" -m "Downloader $TAG"
git push origin "$TAG"
ok "pushed $TAG — release.yml is now packing + publishing to nuget.org and GitHub Packages"

if [[ "$SKIP_WAIT" == "yes" ]]; then
  echo
  ok "Tag pushed. Skipping the wait/notes step (--skip-wait) — check progress with:"
  echo "    gh run list --repo $REPO --workflow $WORKFLOW_FILE"
  exit 0
fi

# --- 4. wait for the release workflow to finish -----------------------------------
# Match the run by the tag's commit SHA — a tag-triggered run's headBranch is the tag ref, so
# `gh run list --branch <tag>` is unreliable; filtering on headSha of the pushed tag is exact.
step "Waiting for $WORKFLOW_FILE to finish for $TAG"
TAG_SHA="$(git rev-parse "$TAG^{commit}")"
deadline=$(( $(date +%s) + 900 ))   # up to 15 minutes
run_id=""
while [[ -z "$run_id" ]]; do
  run_id="$(gh run list --repo "$REPO" --workflow "$WORKFLOW_FILE" --event push \
              --json databaseId,headSha \
              --jq "map(select(.headSha==\"$TAG_SHA\")) | .[0].databaseId" 2>/dev/null || true)"
  [[ -n "$run_id" ]] && break
  [[ $(date +%s) -ge $deadline ]] && die "timed out waiting for the $WORKFLOW_FILE run to appear"
  echo "    ...waiting for the workflow run to start"
  sleep 10
done
ok "found run $run_id"

if ! gh run watch "$run_id" --repo "$REPO" --exit-status; then
  die "release.yml run $run_id failed — inspect with: gh run view $run_id --repo $REPO --log-failed"
fi
ok "release.yml completed successfully"

# --- 5. set curated release notes (highlights + auto-generated changelog) --------
step "Writing release notes for $TAG"
AUTO_NOTES="$(gh api -X POST "repos/$REPO/releases/generate-notes" \
  -f tag_name="$TAG" -f previous_tag_name="v$CUR_VERSION" --jq '.body' 2>/dev/null || true)"
NOTES_TMP="$(mktemp)"
{
  printf '## Highlights\n\n%s\n' "$NOTES_BODY"
  [[ -n "$AUTO_NOTES" ]] && printf '\n%s\n' "$AUTO_NOTES"
} > "$NOTES_TMP"
if gh release edit "$TAG" --repo "$REPO" --notes-file "$NOTES_TMP" >/dev/null 2>&1; then
  ok "release notes set"
else
  warn "could not set release notes (the release may not exist yet) — set them manually: gh release edit $TAG --repo $REPO"
fi
rm -f "$NOTES_TMP"

# --- done --------------------------------------------------------------------
echo
ok "Release $TAG is out."
echo
echo "  Verify:"
echo "    GitHub Release   : gh release view $TAG --repo $REPO"
echo "    nuget.org        : https://www.nuget.org/packages/Downloader/$VERSION"
echo "    GitHub Packages  : https://github.com/bezzad/Downloader/pkgs/nuget/Downloader"
echo
echo "  Remember: update PLAN.md/TASKS.md on develop to record this release (per CLAUDE.md)."
