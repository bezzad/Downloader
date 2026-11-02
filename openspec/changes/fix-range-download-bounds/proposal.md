## Why

**Priority: Medium.**

`DownloadService.SetRangedSizes` (`src/Downloader/DownloadService.cs`, line ~379):

- `RangeHigh = Math.Min(Package.TotalFileSize, Options.RangeHigh)`. `RangeHigh` is the **last byte
  index** (inclusive): `ChunkHub` uses `size = RangeHigh - RangeLow + 1`. So the limit must be
  `TotalFileSize - 1`. When the caller asks past the end, the size is one byte too big, the last chunk
  can never get that byte, and `IncompleteDownloadException` is retried **forever** with the default
  `MaxTryAgainOnFailure = int.MaxValue`.
- `RangeHigh < RangeLow` silently becomes `RangeLow = RangeHigh - 1` (a 2-byte download).
- `RangeLow >= TotalFileSize` is not checked.

## What Changes

- Clamp `RangeHigh` to `TotalFileSize - 1` when the size is known.
- Fail the download with `ArgumentOutOfRangeException` (through `DownloadFileCompleted`, like other
  errors) when `RangeHigh < RangeLow` or `RangeLow >= TotalFileSize`.
- Do this on the per-download values from `keep-caller-config-unchanged` if that change is done first;
  else it still works on the options.

## Capabilities

### New Capabilities
- `range-download`: rules for `RangeLow` / `RangeHigh`.

## Impact

- `src/Downloader/DownloadService.cs`.
- Tests in the integration test classes that use `RangeDownload`.
