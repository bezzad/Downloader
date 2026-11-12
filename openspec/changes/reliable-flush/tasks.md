## 1. Tests first (must fail on the current code)

- [x] 1.1 `FlushAsync` ends while another task keeps writing (slow base stream). (`ConcurrentStreamFlushTest`)
- [x] 1.2 After `FlushAsync`, every block given before it is on the base stream.
- [x] 1.3 `DownloadPackage.FlushAsync` ends while chunks keep writing.
- [x] 1.4 `FlushAsync` on a `FileStream` calls `Flush(true)`.
- [x] 1.5 `FlushAsync` fails (does not succeed, does not hang) when the stream is closed with queued blocks.
- [x] 1.6 `FlushAsync` on a faulted stream throws (already covered by `ConcurrentStreamTest.WriteErrorFaultsTheStream`).

## 2. Fix

- [ ] 2.1 Flush marker in the queue; `FlushAsync` waits only for it (check FIFO in `WaitTryTakeAsync`).
- [ ] 2.2 Writer: on a marker, flush the base stream (`Flush(true)` for `FileStream`) and complete it; no progress, no buffer size.
- [ ] 2.3 Fail waiting markers on writer error and on `Dispose`/`DisposeAsync`.
- [ ] 2.4 Remove the 20 ms delay in `DownloadPackage.FlushInternalAsync` if not needed.
- [ ] 2.5 XML docs: snapshot -> flush -> save. Check `AbstractDownloadService.UpdatePackage`.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
