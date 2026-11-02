## Why

**Priority: Medium.**

The engine writes into the `DownloadConfiguration` that the caller gave:

- `DownloadService.SetSingleChunkDownload` (line ~452) sets `ChunkCount = 1`, `ParallelCount = 1`,
  `ParallelDownload = false`. It runs for a small file, for a server without range support, and for the
  single-connection fallback.
- `DownloadService.SetRangedSizes` (line ~379) rewrites `RangeLow` / `RangeHigh`, and sets both to 0
  when `RangeDownload` is false.
- `SocketClient.GetHttpClientWithSocketHandler` (line ~117) sets `CustomHttpClientFactory = null` when
  the factory returned null.

So after one small file, or one server without range support, **every later download with the same
config runs on one connection**. Downloader.Desktop had to work around this ("the engine mutates the
DownloadConfiguration you give it"). Also `DownloadConfiguration.Clone()` is shallow, so a "copy" still
shares the same `RequestConfiguration` (headers, cookies, proxy).

## What Changes

- Keep the per-download values (chunk count, parallel count, parallel flag, range low/high) in the
  download service (or the package), not in the caller's config. ChunkHub and ChunkDownloader read them
  from there.
- Live settings stay on the caller's config and keep working while a download runs
  (`MaximumBytesPerSecond`, `BufferBlockSize`, `EnableLiveStreaming`, issue #247).
- `ActiveChunks` stays an output on the config (it is documented that way).
- Do not reset `CustomHttpClientFactory`; keep an "owns the client" flag in `SocketClient` instead.
- Owner decision: should `Clone()` also copy `RequestConfiguration` (deep copy)? It is a separate task.

## Capabilities

### New Capabilities
- `download-config`: the caller's configuration is input, not state.

## Impact

- `src/Downloader/DownloadService.cs`, `AbstractDownloadService.cs`, `ChunkHub.cs`,
  `ChunkDownloader.cs`, `SocketClient.cs`, maybe `DownloadConfiguration.cs`.
- Tests that read `Config.ChunkCount` after a download to see the fallback must read the new place.
