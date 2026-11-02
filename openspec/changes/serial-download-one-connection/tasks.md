## 1. Tests first (must fail on the current code)

- [x] 1.1 Test: `ParallelDownload = false`, `ChunkCount = 4`, `ParallelCount = 4` → the highest `ActiveChunks` seen in progress events is 1.
- [x] 1.2 Test (stronger): the server counts open requests for the file; the maximum is 1 (`ServerSeesOneOpenRequestInASerialDownload`, with `CountingRangeServer`; it saw 4 on the old code). The parallel case checks the counter sees more than 1.

## 2. Fix

- [x] 2.1 Rewrite `SerialDownload` to await `DownloadChunk` for chunk `i` before it creates chunk `i + 1`.
- [x] 2.2 Keep the url choice `RequestInstances[i % count]`.
- [x] 2.3 Update the comment in `UpdatePackage` if it is no longer true for the serial path.

## 3. Finish

- [x] 3.1 CHANGELOG entry (behavior fix: serial now means one connection).
- [x] 3.2 Build with 0 warnings; all tests green, including `SerialDownloadIntegrationTest`.
