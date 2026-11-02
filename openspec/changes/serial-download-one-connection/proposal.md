## Why

**Priority: Medium.**

`ParallelDownload = false` should download the chunks one after another, with one connection. It does
not:

- `ParallelSemaphore` is created with `Options.ParallelCount` (`src/Downloader/AbstractDownloadService.cs`,
  line ~369), and `ParallelCount` defaults to `ChunkCount` (`DownloadConfiguration.cs`, line ~138).
- `DownloadService.SerialDownload` (line ~515) calls `GetChunksTasks(pauseToken).ToList()`, which
  **starts every chunk task at once**; each task only waits on that semaphore.

So `ChunkCount = 8, ParallelDownload = false` opens up to 8 connections at the same time. The
"serial" test class (`SerialDownloadIntegrationTest`) sets `ParallelCount = 4`, so it really runs 4 at
a time, and no test notices. The cancellation check inside the loop is also useless, because all tasks
already started before the loop.

## What Changes

- `SerialDownload` creates and awaits one chunk at a time inside the loop (no `.ToList()` up front).
- The cancellation check in the loop then really stops the next chunks.
- `ParallelDownload = true` is not changed.

## Capabilities

### New Capabilities
- `download-modes`: serial versus parallel behavior.

## Impact

- `src/Downloader/DownloadService.cs` (`SerialDownload`, `GetChunksTasks`).
- The comment in `AbstractDownloadService.UpdatePackage` about "both dispatch loops start every chunk
  eagerly" must be checked; the null-storage guard there stays.
- Serial downloads get slower for callers who (by accident) got parallel speed. That is the documented
  behavior, so it is a fix, but say it in the CHANGELOG.
