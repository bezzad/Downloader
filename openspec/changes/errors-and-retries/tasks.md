## 1. Tests first (must fail on the current code)

- [ ] 1.1 M4: slow headers → `Failed`, `Cancelled = false`, probe retried.
- [ ] 1.2 M8: `Stop()` inside `DownloadStarted` → ends in time as `Stopped`.
- [ ] 1.3 M9: several recovered errors with a small limit → completes.
- [ ] 1.4 M13: no-range server that fails once → completes.
- [ ] 1.5 L3: `Pause()` racing the end → status stays `Completed`.
- [ ] 1.6 L5: one 500 / 502 → completes; a stray `OperationCanceledException` from a handler → retried.

## 2. Fix

- [ ] 2.1 M4: timeout handling in `StartDownload` and the probe.
- [ ] 2.2 M8: `Stop()` / `StopAsync()`.
- [ ] 2.3 M9: reset counters on progress.
- [ ] 2.4 M13: restart from 0 for no-range.
- [ ] 2.5 L3: finished check inside `SetState`.
- [ ] 2.6 L5: `IsMomentumError`.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
