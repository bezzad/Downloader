## Why

From the review of 7.0.1 (2026-11-11): issues **C1, C2, L6**.

- **C1 (Critical). A disk write error never ends the download.** When the storage fails in the middle
  of a download (disk full, USB drive removed, network folder lost), the download stays Running for
  ever and asks the server again every few seconds. `CancelTaskAsync()` and `Stop()` then wait for
  ever too.
  - `ExceptionHelper.IsMomentumError` treats every `IOException` and `ObjectDisposedException` as a
    network error, so a disk error is retried (default `MaxTryAgainOnFailure` is `int.MaxValue`).
  - When the writer dies while the memory buffer is full, nobody opens the buffer again: the chunks wait
    in `ConcurrentPacketBuffer.TryAddAsync` for ever, and that wait has no cancel token.
- **C2 (Critical). The end of a download can be lost.** `DownloadPackage.TrySetCompleteState` sets the
  status first and then closes the file. If the close throws, the status is already final, so the
  second try to report the error is refused: no `DownloadFileCompleted`, wrong status.

  | Case | Status | DownloadFileCompleted | File |
  |---|---|---|---|
  | Memory download, storage write failed | Completed | never raised | bytes missing |
  | File download, storage write failed | Failed | never raised, the call throws | handle stays open |
  | The final rename fails (target locked, or name taken) | Completed | never raised | only `name.download` |
- **L6 (Low).** An exception in a `ChunkDownloadProgressChanged` or `DownloadStarted` handler fails the
  download. Only `DownloadProgressChanged` is protected.

## What Changes

- New `StorageException` (an `IOException`) for every error of the storage writer. It is never retried:
  the chunk fails at once and the download is `Failed` with that error.
- When the writer dies, the buffer opens again (`ResumeAdding`) so waiting chunks wake up and see the
  error; `TryAddAsync` takes the cancel token.
- `TrySetCompleteState` does the work first (flush, close, rename) and sets the status last. If the work
  for `Completed` fails, the status is `Failed` and the error is returned. The storage is always closed.
- `DownloadFileCompleted` is raised exactly once for every download.
- Handler errors of `ChunkDownloadProgressChanged` and `DownloadStarted` are caught and logged.

## Capabilities

### New Capabilities
- `download-completion`: every download ends, and the caller is told once.

## Impact

- `ConcurrentStream.cs`, `ConcurrentPacketBuffer.cs`, `Extensions/ExceptionHelper.cs`,
  `DownloadPackage.cs`, `DownloadService.cs`, `AbstractDownloadService.cs`, new
  `Exceptions/StorageException.cs`.
- No public API break (a new public exception type).
