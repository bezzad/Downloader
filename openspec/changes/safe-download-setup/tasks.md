## 1. Tests first (must fail on the current code)

- [x] 1.1 H1: `Content-Type` in `Headers` → the call throws, then `Dispose()` ends in time and a second download call ends.
- [x] 1.2 H1: a `CustomHttpClientFactory` that throws → same result.
- [ ] 1.3 L1: `new Request("example.com/file.zip")` throws `ArgumentException`.
- [ ] 1.4 L2: a second call on a busy service throws and the first download completes.

## 2. Fix

- [x] 2.1 H1: in `InitialDownloader`, a setup error sets the status Failed and ends the completion task, then is thrown to the caller (as before).
- [ ] 2.2 L1: validate the URL in `Request`.
- [ ] 2.3 L2: throw while busy instead of calling `Clear()` on a running download.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
