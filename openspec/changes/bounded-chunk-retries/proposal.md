## Why

**Priority: Medium.**

- `MaxTryAgainOnFailure` defaults to `int.MaxValue` (`DownloadConfiguration.cs`, line ~121). A server
  that always answers 503 / 429 / 408, or always closes the stream early, makes the download **retry
  for ever**: `DownloadFileCompleted` never comes and the caller has no error to show.
- The retry is recursion: `ChunkDownloader.Download` → `ContinueWithDelay` → `Download`
  (`ChunkDownloader.cs`, lines ~77 and ~112). Each retry keeps one more async frame alive until the
  chunk ends, so a long outage keeps growing memory.
- `Chunk.Timeout += _timeoutIncrement` on every retry (line ~110) is never reset or capped. After many
  retries it overflows `int`, and `CancelAfter` with a negative value throws.

## What Changes

- Retry in a loop inside `Download` instead of recursion. Same backoff (`GetBackoffDelay`).
- Cap the per-read timeout growth (for example at `BlockTimeout * 10`, or 60 seconds).
- Owner decision: keep the `int.MaxValue` default (only document it, as the README now does), or use a
  finite default (for example 10). A finite default is a behavior change.

## Capabilities

### New Capabilities
- `chunk-retry`: how a chunk retries.

## Impact

- `src/Downloader/ChunkDownloader.cs`, maybe `DownloadConfiguration.cs` (default).
- Tests that count retries (`ChunkDownloaderTest`, issue #231 / #226 tests) must still pass.
