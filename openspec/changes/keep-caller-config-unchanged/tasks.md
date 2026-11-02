## 1. Decide

- [x] 1.1 Owner: should `DownloadConfiguration.Clone()` deep-copy `RequestConfiguration`? (yes / no) — owner: yes, deep copy.

## 2. Tests first (must fail on the current code)

- [x] 2.1 Test: download a 100-byte file (below `MinimumSizeOfChunking`) with a config of `ChunkCount = 8, ParallelDownload = true`; afterwards the config still says 8 and true.
- [x] 2.2 Test: same config, then a large file → it uses 8 chunks.
- [x] 2.3 Test: a no-range server does not change the config.
- [x] 2.4 Test: `RangeLow` / `RangeHigh` keep the caller's values after a download (with and without `RangeDownload`).
  The values of one download live in the internal `DownloadPlan`.
- [x] 2.5 Test: a `CustomHttpClientFactory` that returns null is still set after the download.

## 3. Fix

- [x] 3.1 Add per-download fields for the effective chunk count, parallel count, parallel flag and range.
- [x] 3.2 Make `SetSingleChunkDownload` and `SetRangedSizes` write those fields only.
- [x] 3.3 Pass the effective values to `ChunkHub` and `ChunkDownloader` (range offset in `ReadStream`, `SetRequestRange`).
- [x] 3.4 `SocketClient`: keep a private "client is ours" flag; use it in `Dispose`; do not touch the config.
- [x] 3.5 If 1.1 = yes: deep-copy `RequestConfiguration` in `Clone()` and test it.

## 4. Finish

- [x] 4.1 CHANGELOG entry (behavior fix).
- [x] 4.2 Build with 0 warnings; all tests green.
