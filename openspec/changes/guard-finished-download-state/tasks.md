## 1. Tests first (must fail on the current code)

- [ ] 1.1 Test: after a Completed download, `CancelAsync()`, `Pause()` and `Resume()` keep `Status == Completed` and `IsBusy == false`.
- [ ] 1.2 Test: same for a Failed download.
- [ ] 1.3 Test: start a download against a slow-header endpoint, call `Clear()` while the status is `Created`; `Clear()` returns quickly and the download ends as Stopped.

## 2. Fix

- [ ] 2.1 Guard `Pause()` / `Resume()` by status.
- [ ] 2.2 `CancelAsync()`: call `PauseTokenSource.Resume()` directly, without changing the status.
- [ ] 2.3 `Clear()`: cancel for `Created` too; release the semaphore only after a successful wait.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
