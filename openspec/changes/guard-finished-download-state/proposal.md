## Why

**Priority: Medium.**

1. **A finished download can change its status.** `DownloadPackage.SetState` (`DownloadPackage.cs`,
   line ~242) has no guard. `CancelAsync()` calls `Resume()` (`AbstractDownloadService.cs`, line ~276),
   and `Resume()` sets `Running`; `Pause()` sets `Paused`. So calling `Stop()`, `CancelAsync()`,
   `Pause()` or `Resume()` on a **Completed** or **Failed** download changes `Status` to Running or
   Paused, and `IsBusy` stays true for ever. Downloader.Desktop had to add its own guards for this.
2. **`Clear()` / `Dispose()` can hang until the download ends.** `Clear()` cancels only when the status
   is Running or Paused (line ~321). While the first header probe runs, the status is `Created`, so
   `Clear()` does not cancel and then waits on `SingleInstanceSemaphore`, which `StartDownload` holds
   for the whole download. Disposing a service right after start waits until the file is fully
   downloaded.
3. `Clear()` releases `SingleInstanceSemaphore` in `finally` even when it never got it (when
   `CancelTaskAsync` throws), which can throw `SemaphoreFullException`.

## What Changes

- `Pause()` and `Resume()` act only while the status is Running or Paused.
- `CancelAsync()` resumes the pause token (so paused chunks can see the cancel) but does not set the
  status to Running.
- `Clear()` cancels when the status is `Created`, `Running` or `Paused`.
- `Clear()` releases the semaphore only if it took it.

## Capabilities

### New Capabilities
- `download-lifecycle`: which status changes are allowed.

## Impact

- `src/Downloader/AbstractDownloadService.cs`, maybe `DownloadPackage.cs`.
- Callers that called `Resume()` on a Stopped download to "restart" it see no change of status; they
  must call `DownloadFileTaskAsync` again (that is already the documented way).
