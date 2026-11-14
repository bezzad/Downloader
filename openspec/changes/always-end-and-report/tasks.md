## 1. Tests first (must fail on the current code)

- [x] 1.1 C1: storage that throws after 4 KB (parallel, default retries) → the call ends, `Failed`, one event with `StorageException`.
- [x] 1.2 C1: same with `MaximumMemoryBufferBytes` set and a slow storage → the call ends (now even cancel hangs).
- [x] 1.3 C2: memory download whose storage fails → `Failed` and one event (now `Completed`, no event).
- [x] 1.4 C2: file download whose storage fails → one event, the call does not throw.
- [x] 1.5 C2: final rename fails (a folder holds the target name) → `Failed`, one event.
- [ ] 1.6 L6 (not done yet: only C1 and C2 were fixed in this step): a throwing `DownloadStarted` / `ChunkDownloadProgressChanged` handler → the download completes.

## 2. Fix

- [x] 2.1 Add `StorageException`; the writer stores it; `WriteAsync`/`FlushAsync` throw it; `IsMomentumError` returns false for it.
- [x] 2.2 Writer catch block: `ConcurrentPacketBuffer.Close()` refuses new packets and wakes every waiting writer and flush. (No cancel token was needed in `TryAddAsync`: nobody waits for room once the writer is gone.)
- [x] 2.3 `TrySetCompleteState`: work first, status last, always dispose the storage; return the error.
- [x] 2.4 `SendDownloadCompletionSignal`: raise the event once with the real error.
- [x] 2.6 A refused block returns its buffer once (`ChunkBufferOwnershipTest`).
- [ ] 2.5 L6: catch and log handler errors in `RaiseProgressChangedEvents` and `OnDownloadStarted`.

## 3. Finish

- [x] 3.1 CHANGELOG entry.
- [x] 3.2 Build with 0 warnings; all tests green (C1, C2 part; L6 is still open).
