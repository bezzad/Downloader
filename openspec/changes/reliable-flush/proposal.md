## Why

**Issue #259 (reported by ktxiaok).** A caller saves the `DownloadPackage` while the download runs, to
resume it later. The chunk positions can be ahead of the bytes on the disk, because
`ChunkDownloader` moves `Chunk.Position` as soon as a block is queued, not when it is written. If the
process ends before the queue is written, the saved package says bytes are there that are not.

The safe order is: take a snapshot of the positions, flush, then save the snapshot. But
`ConcurrentStream.FlushAsync` cannot do that today:

1. **It waits for an empty queue.** `ConcurrentPacketBuffer.WaitToComplete` is released only when the
   queue is empty. While chunks keep writing faster than the disk, it is never empty, so the flush does
   not end.
2. **It does not reach the disk.** `Stream.FlushAsync()` on a `FileStream` calls `Flush(false)`: the
   bytes stay in the OS cache.
3. **It says "done" for dropped bytes.** When the stream is closed while blocks are still queued, the
   flush returns with no error, but the blocks were dropped.
4. `Stream.FlushAsync()` runs on the caller's thread while the writer task may write to the same stream.

Related: M1 in `writer-queue-accounting` (PR #258): the flush can also return before the last block is
written. A flush marker fixes both.

## What Changes

- `FlushAsync` puts a small flush marker (with a `TaskCompletionSource`) into the same FIFO queue and
  waits only for that marker. The single writer task writes every block before it, then flushes the base
  stream (`FileStream.Flush(true)`, else `Flush()`) and completes the marker.
- A marker is not data: no progress, not counted in the buffer size.
- When the writer fails, or the stream is closed, every waiting marker fails (exception / canceled), so a
  flush never hangs and never reports dropped bytes as written.
- Remove the 20 ms delay in `DownloadPackage.FlushInternalAsync` if the new flush makes it unneeded.
- XML docs: snapshot (`CreateResumeMetadataSnapshot`) -> `await FlushAsync` -> save.

## Capabilities

### New Capabilities
- `storage-flush`: what a flush of the storage promises.

## Impact

- `ConcurrentStream.cs`, `ConcurrentPacketBuffer.cs`, `Packet.cs` (or a small marker type),
  `DownloadPackage.cs`. No public API change.
