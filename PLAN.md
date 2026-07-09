# PLAN — Living Plan

This is the **living plan** for the repo. It is the single source of truth for what is
being worked on, kept current at all times and committed on the `develop` branch so any
machine can pull the latest state and continue. Always update this file together with the
code change it describes.

**Status markers:** `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/failed

---

- **Last updated:** 2026-07-09
- **Branch:** fix/issue-236-gzip-content-truncation (off develop)
- **Now working on:** issue #236 fix — pushing branch, commenting on the issue, then release.sh

---

## Active

_(tasks currently in progress — marked `[~]`)_

- [~] Issue #236 follow-up: push `fix/issue-236-gzip-content-truncation`, comment on the issue,
  write `scripts/release.sh` for the next NuGet release (5.9.1). Fix itself is committed; branch
  push/issue comment are pending user go-ahead (public/visible actions).

## Todo

_(queued tasks — marked `[ ]`)_

## Done

- [x] Fix issue #236 (file truncated when a caller's HttpClient auto-decompresses gzip content):
  `SocketClient.GetFileSizeAsync`/`IsSupportDownloadInRange` now treat a non-identity
  `Content-Encoding` on the probe response as unknown size + no range support, routing such
  downloads through the existing single-connection/unknown-size path (issue #230) instead of
  chunking against the compressed byte count and truncating. Added `Issue236Test` +
  `DummyFileController.GetGzipCompressedFile` gzip test endpoint. Verified: new test passes.

- [x] Set up cross-machine task tracking (PLAN.md, TASKS.md, CLAUDE.md workflow section) — committed on develop (e7e73aa)
- [x] Expose public file-metadata resolver — added `RemoteFileResolver` + `RemoteFileInfo` so consumers can fetch a remote file's name/size (and range support) without starting a download; wraps `SocketClient.SetRequestFileNameAsync`/`GetFileSizeAsync`. Tests in `RemoteFileResolverTest`. (4ac4d39)
- [x] Use the metadata concept internally (no duplication) — added canonical `SocketClient.GetFileInfoAsync` (name+size+range in one probe); `DownloadService.StartDownload` now consumes it instead of separate `GetFileSizeAsync`/`IsSupportDownloadInRange` calls; `RemoteFileResolver` delegates to it; exposed `IDownloadService.GetFileInfoAsync(url)`. 165 regression tests + new tests pass. (9d740df)
- [x] Document `RemoteFileResolver` in README (new "get file name and size without downloading" section + Key Features bullet) and left a self-prompt in the **Downloader.Desktop** repo (its PLAN.md/TASKS.md) to migrate the downloads grid to it — note: blocked there until a package release >5.8.1 ships the API. (71fd5b1)
- [x] Released **v5.9.0**: bumped version + PackageReleaseNotes (e205c1e), merged develop→master (3ed452f), tagged `v5.9.0`, created the GitHub Release with notes + assets, and published the NuGet package to **GitHub Packages**. NuGet.org publish is pending — no nuget.org API key available to this session (see Blocked/Failed).
- [x] README: added the **Downloader Desktop** promo + screenshot at the top (2b25e07) and made the screenshot theme-aware via `<picture>` — `home-dark.png` in dark mode, `home-light.png` in light mode, light fallback (c18c3ae).

## Blocked/Failed

- [!] Publish Downloader **5.9.0 to NuGet.org** — blocked: no nuget.org API key is available to this
  session (only the source URL is configured, not a key). To finish: `dotnet nuget push
  Downloader.5.9.0.nupkg --source nuget.org --api-key <YOUR_NUGET_ORG_KEY>` (package already built;
  also published to GitHub Packages). The `v5.9.0` tag + GitHub Release are already live.
