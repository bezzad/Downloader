## 1. Tests first (must fail on the current code)

- [ ] 1.1 Test: run 3 downloads (4 chunks each) with the same `DownloadConfiguration`; afterwards the invocation list of `PropertyChanged` has the same length as before the first download.

## 2. Fix

- [ ] 2.1 Make `ChunkDownloader` implement `IDisposable`; `Dispose` removes the `PropertyChanged` handler.
- [ ] 2.2 In `DownloadService.DownloadChunk`, create the chunk downloader with `using`.
- [ ] 2.3 Check that the live speed-limit change (issue #247 tests) still reaches running chunks.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
