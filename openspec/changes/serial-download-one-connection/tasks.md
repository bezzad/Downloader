## 1. Tests first (must fail on the current code)

- [x] 1.1 Test: `ParallelDownload = false`, `ChunkCount = 4`, `ParallelCount = 4` → the highest `ActiveChunks` seen in progress events is 1.
- [ ] 1.2 Test (stronger): the dummy server counts open requests for the file; the maximum is 1. (Not done: the 1.1 test already fails on the old code with 4 active chunks.)

## 2. Fix

- [x] 2.1 Rewrite `SerialDownload` to await `DownloadChunk` for chunk `i` before it creates chunk `i + 1`.
- [x] 2.2 Keep the url choice `RequestInstances[i % count]`.
- [x] 2.3 Update the comment in `UpdatePackage` if it is no longer true for the serial path.

## 3. Finish

- [x] 3.1 CHANGELOG entry (behavior fix: serial now means one connection).
- [x] 3.2 Build with 0 warnings; all tests green, including `SerialDownloadIntegrationTest`.
