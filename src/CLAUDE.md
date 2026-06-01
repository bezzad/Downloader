# Downloader — Codebase Guide

## What this project is

**Downloader** (`bezzad/Downloader`) is a .NET NuGet library (v5.5.0) for fast, cross-platform multipart file downloads. It supports parallel chunked downloads, pause/resume, bandwidth throttling, progress events, and AOT compilation. Targets .NET 8, 9, and 10.

---

## Repository layout

```
src/
  Downloader/                  # Main library (NuGet package)
  Downloader.Test/             # All tests (xUnit)
    UnitTests/                 # Component-level tests
    IntegrationTests/          # End-to-end tests using DummyHttpServer
    HelperTests/               # Utility/helper tests
  Downloader.DummyHttpServer/  # ASP.NET test server used by integration tests
```

---

## Key source files

| File | Role |
|---|---|
| `AbstractDownloadService.cs` | Base class: lifecycle, events, pause/resume/cancel, progress tracking |
| `DownloadService.cs` | Concrete impl: `StartDownload()`, parallel/serial dispatch, auto-resume |
| `DownloadConfiguration.cs` | All settings (implements `INotifyPropertyChanged`, `ICloneable`) |
| `DownloadPackage.cs` | Live state snapshot: chunks, file size, status, storage handle |
| `Chunk.cs` | Single file segment: start/end/position, retry counter, timeout |
| `ChunkHub.cs` | Splits file into N equal chunks respecting `MinimumChunkSize` |
| `ChunkDownloader.cs` | Downloads one chunk: retry loop, pause support, ArrayPool reads |
| `ConcurrentStream.cs` | Thread-safe write-only stream with background Watcher task |
| `ConcurrentPacketBuffer.cs` | Bounded async queue for `Packet` objects |
| `SocketClient.cs` | `HttpClient` wrapper: range-support detection, redirect handling, file-size extraction |
| `ThrottledStream.cs` | Wraps a stream to enforce per-chunk bandwidth limits |
| `Bandwidth.cs` | Tracks instantaneous and average download speed |
| `PauseTokenSource.cs` / `PauseToken.cs` | Custom pause primitive (wraps `SemaphoreSlim`) |
| `DownloadBuilder.cs` | Fluent API: `DownloadBuilder.New().WithUrl(...).Build().StartAsync()` |
| `Request.cs` / `RequestConfiguration.cs` | HTTP request model + per-request headers/proxy/auth |
| `DownloadProgressChangedEventArgs.cs` | Progress event payload (speed, bytes, percentage, live data) |

---

## Architecture overview

### Download flow

```
DownloadFileTaskAsync(url, path)
  └── InitialDownloader()          // creates SocketClient, Request, ChunkHub, semaphores
  └── StartDownload()
        ├── GetFileSizeAsync()     // HEAD/GET with Range:0-0 to probe server
        ├── IsSupportDownloadInRange()
        ├── ProvideDownloadOnFile()
        │     └── TryResumeFromExistingFile()  // reads metadata from end of .download file
        ├── Package.BuildStorage() // creates ConcurrentStream (file or memory)
        ├── ChunkHub.SetFileChunks()
        ├── ParallelDownload() or SerialDownload()
        │     └── DownloadChunk() × N
        │           └── ChunkDownloader.Download()
        │                 └── ReadStream() → ConcurrentStream.WriteAsync()
        └── SendDownloadCompletionSignal()
              └── Package.TrySetCompleteState()  // truncate metadata, rename file
```

### Producer-consumer for writes

`ChunkDownloader` pushes `Packet` objects (with rented `ArrayPool` buffers + position) to
`ConcurrentPacketBuffer`. A single background `Watcher` task in `ConcurrentStream` dequeues
packets and writes them to the underlying `FileStream`/`MemoryStream` in order.

### Auto-resume (`.download` files)

When `EnableAutoResumeDownload = true`:
- During download: every ~1 second, `Package` state is serialized (JSON→binary) and written
  **appended after** `TotalFileSize` in the `.download` temp file.
- On resume: `stream.Length - TotalFileSize` bytes at end = metadata. Deserialize, verify
  `TotalFileSize` matches server, restore `Chunks[]`.
- On completion: `SetLength(TotalFileSize)` strips metadata, then `File.Move` to final name.

---

## Build and test

```bash
# Run all tests (uses docker-compose to spin up DummyHttpServer)
docker-compose -p downloader up

# Run tests directly (DummyHttpServer auto-starts in tests via HttpServer.cs)
dotnet test src/Downloader.Test/

# Build AOT native
dotnet publish -r linux-x64 -f net8.0 -c Release src/Downloader/
```

The `DummyHttpServer` project is an ASP.NET app referenced by integration tests. `HttpServer.cs`
manages a singleton instance so tests share one server process.

---

## Configuration quick-reference

```csharp
new DownloadConfiguration {
    ChunkCount = 8,               // parallel segments; auto-reduced if file too small
    ParallelDownload = true,
    ParallelCount = 4,            // concurrent active chunks (default = ChunkCount)
    MaximumBytesPerSecond = 2*MB, // 0 = unlimited
    BufferBlockSize = 8192,       // read buffer per stream call (max 1MB)
    BlockTimeout = 1000,          // ms timeout per ReadAsync call
    HttpClientTimeout = 100_000,  // ms per HttpClient request
    MaxTryAgainOnFailure = 5,
    MinimumSizeOfChunking = 512,  // bytes; below this = single chunk
    MinimumChunkSize = 0,         // 0 = no constraint
    MaximumMemoryBufferBytes = 0, // 0 = unlimited (ConcurrentPacketBuffer limit)
    EnableAutoResumeDownload = false,
    DownloadFileExtension = ".download",
    FileExistPolicy = FileExistPolicy.Delete,
    EnableLiveStreaming = false,
    RangeDownload = false,        // download a byte slice only
    CheckDiskSizeBeforeDownload = true,
    CustomHttpClientFactory = null,         // bypass all internal HttpClient setup
    CustomHttpMessageHandlerFactory = null, // replace handler, keep header config
}
```

---

## Status enum

`DownloadStatus`: `None → Created → Running → Paused → Completed / Stopped / Failed`

`DownloadPackage.TrySetCompleteState()` (at `DownloadPackage.cs:179`) is idempotent — once
`Failed` or `Completed`, state cannot change. This prevents race conditions between parallel
chunk errors.

### Stopped vs Failed (terminal state rule)

In `DownloadService.cs:129`: status becomes **`Stopped`** when `IsCancelled == true` OR the
caught exception is `OperationCanceledException`/`TaskCanceledException` (including exceptions
thrown *during* an in-flight cancellation, fixed in commit `e4ed715`). Any other exception
yields **`Failed`**. When fixing cancellation bugs, always check the cancellation flag, not
just the exception type.

### FileExistPolicy values

`IgnoreDownload=0` (skip if exists) · `Delete=1` (overwrite, default) · `Exception=2` (throw
`FileExistException`) · `Rename=3` (append numeric suffix).

---

## Exceptions (`Downloader/Exceptions/`)

| Exception | When |
|---|---|
| `IncompleteDownloadException` | Chunk closed early — `Position < Length` after read loop. Raised in `ChunkDownloader.cs` via `IsChunkIncomplete()`; triggers retry while `MaxTryAgainOnFailure > 0`. Only checked for known-length chunks (Length>0). See issue #231. |
| `FileExistException` | `FileExistPolicy.Exception` and the target file exists. Extends `IOException`; `.Name` holds the path. |

---

## Events (raised by `AbstractDownloadService`)

- `DownloadStarted` — once at start, after file size + storage are ready
- `ChunkDownloadProgressChanged` — per chunk read, raw chunk bytes/speed
- `DownloadProgressChanged` — aggregated across all chunks, raised *after* the chunk event
- `DownloadFileCompleted` — terminal; `Error`/`Cancelled` flags reflect Failed/Stopped status

Handlers are invoked **synchronously on the chunk download thread** — slow handlers will stall
downloads. `ActiveChunks` on the progress args is read from `ParallelSemaphore.CurrentCount`
(`AbstractDownloadService.cs:402`).

### Unknown-size downloads (Content-Length absent)

`TotalFileSize` starts at 0. `RaiseProgressChangedEvents()` (`AbstractDownloadService.cs:399`)
must **not** report 100% prematurely — it grows `TotalFileSize` only when received exceeds
advertised, and `ProgressPercentage` stays 0 until completion finalizes the real size in
`DownloadService.cs:106` (`TotalFileSize = ReceivedBytesSize`). Recently fixed in `e4e50c9`.

---

## Storage abstraction

`Package.BuildStorage()` (`DownloadPackage.cs:155`) decides:
- `DownloadingFileName` null/whitespace → `MemoryStream`
- otherwise → `FileStream` preallocated to `TotalFileSize`

`ConcurrentStream` lazily creates the underlying stream on first write (double-checked lock at
`ConcurrentStream.cs:180`). The background `Watcher` task is `LongRunning`-scheduled and
returns `ArrayPool` buffers after each ordered write.

**Hazard:** `ConcurrentStream.Dispose()` does **not** await `_watcherTask` or drain buffered
packets — prefer `DisposeAsync()` to flush cleanly. The `_disposed` flag is volatile and
checked under lock to prevent re-init after disposal.

---

## Test infrastructure

`DummyHttpServer.HttpServer` is a singleton guarded by `SemaphoreSlim StartLock(1,1)`
(`HttpServer.cs:18`). `DummyFileHelper`'s static ctor (`DummyFileHelper.cs:22`) calls
`HttpServer.Run(0)` so tests auto-start the server on first reference; port is dynamic via
`SetPort()`.

`DummyFileController` endpoints used by integration tests:

| Route pattern | Purpose |
|---|---|
| `/dummyfile/file/size/{size}` | Plain file, no Content-Disposition |
| `/dummyfile/file/{name}?size=...` | With filename header |
| `/dummyfile/file/{name}/size/{size}/norange` | Advertises **no** range support |
| `/dummyfile/file/{name}/redirect?size=...` | 308 redirect |
| `/dummyfile/file/size/{size}/failure/{offset}` | Throws mid-stream |
| `/dummyfile/file/size/{size}/timeout/{offset}` | Delays mid-stream |
| `/dummyfile/file/{name}/size/{size}/truncate/{actualSize}` | Advertises full size, delivers partial (issue #231 regression) |
| `/dummyfile/file/{name}/check-useragent?size=...` | Returns 428 on invalid/AOT User-Agent |

When writing integration tests that depend on event timing, remember chunk events fire
concurrently across N parallel chunks — use thread-safe progress collection and a
cancellation-triggered wait rather than relying on event ordering (see fix in `3ddb9fd`).

---

## Recurring bug patterns (lessons from recent commits)

- **Early stream close looks like success** — server-side disconnects don't throw; always
  validate `Chunk.Position == Chunk.Length` after the read loop. (#231)
- **Unknown Content-Length** — never compute progress percentage off `TotalFileSize` until it's
  been finalized; guard against the 0 / 0 case. (#230)
- **Cancellation during cancellation** — exceptions raised while a cancel is in flight must
  still map to `Stopped`, not `Failed`. Check `IsCancelled` first. (#225)

---

## Serializer

`AbstractDownloadService.Serializer` is a public `IBinarySerializer` (default:
`JsonBinarySerializer`). You can replace it if needed. Used to read/write auto-resume metadata.

---

## Patterns to be aware of

- **`ArrayPool<byte>.Shared`** — buffers rented in `ChunkDownloader.ReadStream()` and
  `DownloadService.TryResumeFromExistingFile()`; ownership transferred to `Packet` on write,
  returned on `Packet.Dispose()`.
- **`INotifyPropertyChanged` on `DownloadConfiguration`** — `ChunkDownloader` listens for
  `MaximumBytesPerSecond` changes to update the live throttle on active streams.
- **Single-instance semaphore** — `SingleInstanceSemaphore` in `AbstractDownloadService`
  prevents concurrent `StartDownload` calls on the same service instance.
- **AOT compatibility** — the library is `IsAotCompatible = true`, uses `System.Text.Json`
  source gen, no reflection-based serialization.
