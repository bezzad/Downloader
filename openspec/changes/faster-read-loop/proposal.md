## Why

From the review of 7.0.1 (2026-11-11): issues **M2, M14**.

- **M2 (Medium). The speed limit is far too low with a big buffer.** `ThrottledStream` counts the buffer
  size, not the bytes really read. HTTPS gives at most 16 KB per read, so any `BufferBlockSize` above
  16 KB slows the download below the limit (probe: a 256 KB/s limit gave 75 KB/s with 16 KB reads and
  4.7 KB/s with 1 KB reads).
- **M14 (Medium). Slow read loop.** The default buffer is 1 KB. Every read makes a new cancel source
  with a timer, two registrations, a packet, an event object and three log calls. On a fast link this
  costs a lot of CPU.

## What Changes

- M2: throttle AFTER the read, with the real byte count; read into `buffer.AsMemory(0, BufferBlockSize)`.
- M14: default `BufferBlockSize` 64 KB (after M2); one cancel source per chunk, set again with
  `CancelAfter`; check `IsEnabled` before the debug logs.

## Capabilities

### New Capabilities
- `read-loop`: how a chunk reads and throttles.

## Impact

- `ThrottledStream.cs`, `ChunkDownloader.cs`, `DownloadConfiguration.cs` (default buffer size).
