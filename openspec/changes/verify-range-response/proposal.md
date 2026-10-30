## Why

**Priority: High (silent file corruption).**

`ChunkDownloader.DownloadChunk` (`src/Downloader/ChunkDownloader.cs`, around line 141) sends
`Range: start-end` and writes whatever body comes back at offset `Chunk.Start + Chunk.Position`.
It never checks that the server really answered with `206 Partial Content` and a matching
`Content-Range`.

If a server, a CDN node, or a mirror answers `200 OK` with the whole file, the first bytes of the file
are written in the middle of the output. The download then reports **Completed** with a broken file.
This can happen:

- with mirror URLs: only the first URL is probed (`DownloadService.StartDownload`, line ~52), the
  others are trusted;
- when a CDN node behind the same URL does not support ranges;
- on resume, where a single chunk asks from `Position > 0`.

## What Changes

- When a request had a `Range` header, check the answer:
  - `206` with `Content-Range` start equal to the asked start → read as today;
  - `200` and the asked start is `0` → read as today (the chunk stops at its length);
  - anything else → fail the chunk with a clear error (not retried, the same server will answer the
    same way).
- Owner decision (task 1.1): on that error, fail the download, or fall back once to a single connection
  from byte 0 (the existing single-connection fallback could be reused).

## Capabilities

### New Capabilities
- `chunk-download`: what a chunk accepts from the server.

## Impact

- `src/Downloader/ChunkDownloader.cs`; maybe `DownloadService.ShouldFallbackToSingleConnection`.
- Tests: a dummy-server endpoint that ignores `Range` only for some requests (for example the second
  URL of a mirror list) and returns `200` with the full body.
