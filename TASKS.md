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
