# TASKS — Backlog Board

Fuller backlog board for the repo. Kept in sync with [PLAN.md](PLAN.md) and committed on
the `develop` branch. Use this as the full board for larger backlogs.

**Status markers:** `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/failed

| Status | Task | Files/Notes | Commit |
| ------ | ---- | ----------- | ------ |
| [x] | Set up cross-machine task tracking | PLAN.md, TASKS.md, CLAUDE.md | e7e73aa |
| [x] | Expose public file-metadata resolver (filename + size) without starting a download | `src/Downloader/RemoteFileResolver.cs`, `RemoteFileInfo.cs`; test `RemoteFileResolverTest.cs`; wraps `SocketClient.SetRequestFileNameAsync`/`GetFileSizeAsync` | 4ac4d39 |
| [x] | Consume the metadata concept internally (dedupe) | Canonical `SocketClient.GetFileInfoAsync`; `DownloadService.StartDownload` + `RemoteFileResolver` use it; `IDownloadService.GetFileInfoAsync(url)` exposed | 9d740df |
| [x] | Document RemoteFileResolver in README + self-prompt in Downloader.Desktop | `README.md` (new section + Key Features); Downloader.Desktop `PLAN.md`/`TASKS.md` Todo to adopt it for the grid (blocked on package release >5.8.1) | 71fd5b1 |
| [x] | Release v5.9.0 (merge→master, tag, publish package) | Version 5.9.0 + PackageReleaseNotes; merged develop→master; tag `v5.9.0`; GitHub Release + assets; published to **GitHub Packages** | e205c1e / 3ed452f |
| [x] | README: Downloader Desktop promo + theme-aware `<picture>` screenshot | `README.md` (top promo block; dark/light `home-*.png` with light fallback) | 2b25e07 / c18c3ae |
| [!] | Publish 5.9.0 to NuGet.org | Blocked — no nuget.org API key in this session. Run `dotnet nuget push Downloader.5.9.0.nupkg --source nuget.org --api-key <KEY>`. Pkg already built + on GitHub Packages. | — |
| [x] | Fix issue #236 (gzip truncation with auto-decompressing custom HttpClient) | `src/Downloader/SocketClient.cs` (`HasContentEncoding`, `GetFileSizeAsync`, `IsSupportDownloadInRange`); test `Issue236Test.cs`; dummy server `GetGzipCompressedFile`. Branch pushed, [issue comment posted](https://github.com/bezzad/Downloader/issues/236#issuecomment-4929028719), merged to develop. | 607de0a / 94da811 |
| [x] | Write `scripts/release.sh` + `.github/workflows/release.yml` for Downloader (NuGet.org + GitHub Packages + GitHub Release) | Publishing runs in CI (tag-push-triggered `release.yml`, uses repo secrets `NUGET_API_KEY`/`PACKAGE_TOKEN`); `release.sh` does version bump/merge/tag + waits on the run + sets curated notes. Adapted from `../Downloader.Desktop/scripts/release.sh` + its `release.yml`. Not run yet. | fdabdba |
| [ ] | Cut release **5.9.1** | Run `scripts/release.sh 5.9.1` once ready; supersedes the still-open 5.9.0→NuGet.org gap above | pending |
