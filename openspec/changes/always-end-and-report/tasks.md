## 1. Tests first (must fail on the current code)

- [ ] 1.1 C1: storage that throws after 4 KB (parallel, default retries) → the call ends, `Failed`, one event with `StorageException`.
- [ ] 1.2 C1: same with `MaximumMemoryBufferBytes` set and a slow storage → the call ends (now even cancel hangs).
- [ ] 1.3 C2: memory download whose storage fails → `Failed` and one event (now `Completed`, no event).
- [ ] 1.4 C2: file download whose storage fails → one event, the call does not throw.
- [ ] 1.5 C2: final rename fails (a folder holds the target name) → `Failed`, one event.
- [ ] 1.6 L6: a throwing `DownloadStarted` / `ChunkDownloadProgressChanged` handler → the download completes.

## 2. Fix

- [ ] 2.1 Add `StorageException`; the writer stores it; `WriteAsync`/`FlushAsync` throw it; `IsMomentumError` returns false for it.
- [ ] 2.2 Writer catch block: `ResumeAdding()`; `TryAddAsync` takes the cancel token.
- [ ] 2.3 `TrySetCompleteState`: work first, status last, always dispose the storage; return the error.
- [ ] 2.4 `SendDownloadCompletionSignal`: raise the event once with the real error.
- [ ] 2.5 L6: catch and log handler errors in `RaiseProgressChangedEvents` and `OnDownloadStarted`.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
