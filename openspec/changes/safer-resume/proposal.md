## Why

**Priority: Medium (wrong file content after resume).**

1. **A changed file with the same size is resumed.** `DownloadService.TryResumeFromExistingFile`
   (line ~217) only compares `TotalFileSize`. If the server file was replaced by another file of the
   same size (a new build, an edited video), old and new bytes are mixed and the result is reported
   **Completed**. No `ETag` / `Last-Modified` check is done.
2. **Old metadata bytes can stay at the end.** Resume metadata is written at offset `TotalFileSize`
   without cutting the file (`AbstractDownloadService.UpdatePackage`, line ~505). If the new JSON is
   shorter than the old one (possible after `Chunk.Clear()` resets values), the old tail stays, the next
   resume reads broken JSON and silently starts again from 0.
3. **Bytes that never reached the disk are counted.** `ConcurrentPacketBuffer.TryAddAsync` swallows
   every error and returns false; `ConcurrentStream.WriteAsync` ignores that; `ChunkDownloader` still
   advances `Chunk.Position` (`ChunkDownloader.cs`, lines ~205-210). After a stop, the storage is closed
   while abandoned chunks still read (see the comment in `UpdatePackage`), so the in-memory `Package`
   can count bytes that were never written. A manual resume with that `Package` leaves zero-filled holes.
   The packet that was queued after the drain is also never returned to the `ArrayPool`.

## What Changes

1. Save `ETag` and `Last-Modified` (from the probe) in `PackageInfo`. On resume, if the server sends one
   of them and it differs from the saved value, start fresh. Old metadata without these fields still
   resumes as today.
2. Make the metadata length unambiguous: write a fixed 8-byte length after the JSON and read by it (or
   cut the file to `TotalFileSize + length` after the write). Owner/implementer picks the simpler one in
   task 1.1.
3. `ConcurrentStream.WriteAsync` throws when the packet cannot be queued (for example
   `ObjectDisposedException`), and disposes the packet. Then `Chunk.Position` is not advanced.

## Capabilities

### New Capabilities
- `resume`: when and how a download continues from saved state.

## Impact

- `src/Downloader/DownloadService.cs`, `DownloadPackage.cs` (`PackageInfo`), `SocketClient.cs`
  (expose the two headers in `RemoteFileInfo` or keep them internal), `AbstractDownloadService.cs`,
  `ConcurrentStream.cs`, `ConcurrentPacketBuffer.cs`, the JSON source-gen context (new properties).
- The metadata format changes; the reader must accept the old format.
