## Why

From the review of 7.0.1 (2026-11-11): issues **M4, M8, M9, M13, L3, L5**.

- **M4 (Medium).** A timeout while asking the server for the file info is reported as a user cancel
  (`Stopped`, `Cancelled = true`). It is also not tried again (`DownloadService.cs` line ~168,
  `SocketClient.IsSupportDownloadInRangeWithRetry`).
- **M8 (Medium). `Stop()` inside an event handler deadlocks.** `Download.Stop()` waits for the download
  to end, but the handler runs on the download itself.
- **M9 (Medium).** The failure counter of a chunk is never reset. With a small `MaxTryAgainOnFailure`, a
  long download fails after that many errors in total, even when each one was recovered. The read
  timeout also only grows.
- **M13 (Medium).** On a server without range support, one network error fails the download, even with
  retries on (`ChunkDownloader.Download` lines ~93-94).
- **L3 (Low).** `Pause()` at the exact end can leave the status `Paused` after `Completed`, because the
  check and the write are two steps. The next download on that service then starts paused.
- **L5 (Low).** 500 and 502 are never retried, and a plain `OperationCanceledException` from a custom
  handler is not seen as a timeout.

## What Changes

- M4: `Stopped` only when the token was really cancelled. A timeout is `Failed` with a
  `TimeoutException`, and the probe retries it like a 503.
- M8: `Stop()` only asks for the cancel; a new `StopAsync()` waits.
- M9: reset `FailureCount` and `Timeout` when the chunk gets bytes again.
- M13: start the single chunk again from byte 0, inside the retry limit.
- L3: do the "is it finished" check inside `SetState`, under its lock.
- L5: retry 500 and 502; a cancel the user did not ask for is a timeout.

## Capabilities

### New Capabilities
- `error-handling`: how errors are classified, retried and reported.

## Impact

- `DownloadService.cs`, `SocketClient.cs`, `Download.cs`, `IDownload.cs` (`StopAsync`), `Chunk.cs`,
  `ChunkDownloader.cs`, `DownloadPackage.cs`, `AbstractDownloadService.cs`, `Extensions/ExceptionHelper.cs`.
- M8 changes what `Stop()` waits for: note it in the CHANGELOG.
