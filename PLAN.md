# PLAN — Living Plan

This is the **living plan** for the repo. It is the single source of truth for what is
being worked on, kept current at all times and committed on the `develop` branch so any
machine can pull the latest state and continue. Always update this file together with the
code change it describes.

**Status markers:** `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/failed

---

- **Last updated:** 2026-07-09
- **Branch:** develop
- **Now working on:** — (issue #236 fix shipped to develop; next release (5.9.1) not yet cut —
  run `scripts/release.sh` when ready)

---

## Active

_(tasks currently in progress — marked `[~]`)_

## Todo

_(queued tasks — marked `[ ]`)_

- [ ] Cut the next release (suggest **5.9.1**) once ready: `scripts/release.sh` merges
  develop→master, tags, and `.github/workflows/release.yml` runs the test suite, then publishes to
  nuget.org + GitHub Packages using the `NUGET_API_KEY`/`PACKAGE_TOKEN` repo secrets (now
  configured). Release-tooling hardening applied (audit fixes): snupkg symbol format, `--no-symbols`
  on the GitHub Packages push, XML-escaped release notes, GitHub Release created before the feed
  pushes, commit-SHA run matching in release.sh, and a CI test gate before publish.

## Done

- [x] Fix issue #236 (file truncated when a caller's HttpClient auto-decompresses gzip content):
  `SocketClient.GetFileSizeAsync`/`IsSupportDownloadInRange` now treat a non-identity
  `Content-Encoding` on the probe response as unknown size + no range support, routing such
  downloads through the existing single-connection/unknown-size path (issue #230) instead of
  chunking against the compressed byte count and truncating. Added `Issue236Test` +
  `DummyFileController.GetGzipCompressedFile` gzip test endpoint. Verified: new test passes.
  Branch `fix/issue-236-gzip-content-truncation` pushed, explanatory comment posted on the issue
  (https://github.com/bezzad/Downloader/issues/236#issuecomment-4929028719), merged into develop
  (94da811).
- [x] Added `scripts/release.sh` + `.github/workflows/release.yml` for the NuGet release process.
  Publishing (nuget.org + GitHub Packages) happens in CI via the tag-push-triggered workflow,
  since `NUGET_API_KEY`/`PACKAGE_TOKEN` live in GitHub Actions secrets, not a local shell —
  release.sh only does the version bump/merge/tag mechanics and then waits on the workflow +
  sets curated release notes. Modeled on `../Downloader.Desktop/scripts/release.sh` +
  `release.yml`. Not run yet — no version has been supplied to release. (fdabdba)

- [x] Set up cross-machine task tracking (PLAN.md, TASKS.md, CLAUDE.md workflow section) — committed on develop (e7e73aa)
- [x] Expose public file-metadata resolver — added `RemoteFileResolver` + `RemoteFileInfo` so consumers can fetch a remote file's name/size (and range support) without starting a download; wraps `SocketClient.SetRequestFileNameAsync`/`GetFileSizeAsync`. Tests in `RemoteFileResolverTest`. (4ac4d39)
- [x] Use the metadata concept internally (no duplication) — added canonical `SocketClient.GetFileInfoAsync` (name+size+range in one probe); `DownloadService.StartDownload` now consumes it instead of separate `GetFileSizeAsync`/`IsSupportDownloadInRange` calls; `RemoteFileResolver` delegates to it; exposed `IDownloadService.GetFileInfoAsync(url)`. 165 regression tests + new tests pass. (9d740df)
- [x] Document `RemoteFileResolver` in README (new "get file name and size without downloading" section + Key Features bullet) and left a self-prompt in the **Downloader.Desktop** repo (its PLAN.md/TASKS.md) to migrate the downloads grid to it — note: blocked there until a package release >5.8.1 ships the API. (71fd5b1)
- [x] Released **v5.9.0**: bumped version + PackageReleaseNotes (e205c1e), merged develop→master (3ed452f), tagged `v5.9.0`, created the GitHub Release with notes + assets, and published the NuGet package to **GitHub Packages** and **nuget.org** (https://www.nuget.org/packages/Downloader/5.9.0).
- [x] README: added the **Downloader Desktop** promo + screenshot at the top (2b25e07) and made the screenshot theme-aware via `<picture>` — `home-dark.png` in dark mode, `home-light.png` in light mode, light fallback (c18c3ae).

## Blocked/Failed

_(none)_

- [x] ~~Publish Downloader **5.9.0 to NuGet.org**~~ — RESOLVED: 5.9.0 is live on nuget.org
  (https://www.nuget.org/packages/Downloader/5.9.0). The earlier "no API key in this session" block
  is stale — going forward the CI `release.yml` publishes to nuget.org + GitHub Packages using the
  `NUGET_API_KEY`/`PACKAGE_TOKEN` Actions secrets, so the next `scripts/release.sh` run covers both
  feeds automatically.
