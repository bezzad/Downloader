## 1. Decide

- [ ] 1.1 Owner: on a wrong range answer, fail the download, or fall back once to one connection?

## 2. Tests first (must fail on the current code)

- [ ] 2.1 Add a dummy-server endpoint that answers `200` with the full body even when `Range` is sent (but still advertises range support on the `Range: 0-0` probe).
- [ ] 2.2 Test: a 4-chunk parallel download from it does not report Completed with wrong bytes.
- [ ] 2.3 Test: two mirror URLs where the second one ignores `Range` does not give a corrupted file.

## 3. Fix

- [ ] 3.1 In `ChunkDownloader.DownloadChunk`, after the response, check status code and `Content-Range` start when a `Range` header was sent.
- [ ] 3.2 Throw a clear exception that `IsMomentumError` does not retry.
- [ ] 3.3 Apply the decision from 1.1.

## 4. Finish

- [ ] 4.1 README "When does the Downloader fail to download in multiple chunks?": add this case.
- [ ] 4.2 CHANGELOG entry.
- [ ] 4.3 Build with 0 warnings; all tests green.
