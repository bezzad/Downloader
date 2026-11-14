## Why

From the review of 7.0.1 (2026-11-11): issues **M1, M3, L8, L11**.

- **M1 (Medium). Flush can return before the last block is written.** A block added at the wrong moment
  is still in the queue when `FlushAsync` returns. The close step then stops the writer and drops it.
  Rare, but the file is `Completed` with a hole. A 20 ms `Task.Delay` in
  `DownloadPackage.FlushInternalAsync` hides it most of the time (probe: 928 early returns in 6.5 million
  adds).
- **M3 (Medium). `MaximumMemoryBufferBytes` does not limit memory.** It counts the bytes used in each
  block, but each block holds a whole rented buffer (probe: a 64 KB limit let 200 MB of buffers wait).
- **L8 (Low).** With no memory limit (the default) the write queue grows without end when the disk is
  slower than the network. A memory download above 2 GB fails only after 2 GB arrived.
- **L11 (Low).** The sync `ConcurrentStream.Dispose()` does not wait for the writer, and
  `DownloadPackage.Dispose()` uses it. Blocks still in the queue are dropped.

## What Changes

- M1: count the waiting blocks (add 1 in `Add`, take 1 after the write). Flush waits for zero. On close,
  write what is left. Remove the delay.
- M3: keep a running total of the rented array sizes and limit on that.
- L8: a default limit (for example 64 MB); fail at the start when a memory download is larger than a
  `MemoryStream` can hold.
- L11: flush and wait in `Dispose()` too, or mark it obsolete in favour of `DisposeAsync`.

## Capabilities

### New Capabilities
- `storage-writer`: how blocks are queued and written.

## Impact

- `ConcurrentPacketBuffer.cs`, `ConcurrentStream.cs`, `DownloadPackage.cs`, `DownloadConfiguration.cs`.
- L8 changes a default: next major version.
