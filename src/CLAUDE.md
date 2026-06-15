# Downloader — Codebase Guide

> Read this first. It is the high-signal map of the repo. For day-to-day code-style rules and
> token-efficient navigation, also see the **`code-navigator`** skill in `.claude/skills/`.

## What this project is

**Downloader** (`bezzad/Downloader`) is a .NET NuGet library (currently **v5.8.0**) for fast,
cross-platform multipart file downloads. It supports parallel chunked downloads, pause/resume,
bandwidth throttling, progress events, custom `HttpClient`/handler injection, and Native AOT.
Targets **.NET 8, 9, and 10**. The library has **no external runtime dependencies** and is
`IsAotCompatible = true` (System.Text.Json source-gen, no reflection serialization).

---

## Repository layout

```
src/
  Downloader/                  # Main library (the NuGet package)
    Exceptions/                # IncompleteDownloadException, FileExistException
    Extensions/                # ExceptionHelper, FileHelper, UrlHelper
    Helpers/                   # internal helpers
  Downloader.Test/             # All tests (xUnit)
    UnitTests/                 # Component-level tests
    IntegrationTests/          # End-to-end tests against DummyHttpServer
    HelperTests/               # Utility/server-endpoint tests
  Downloader.DummyHttpServer/  # ASP.NET test server used by integration tests
  Samples/Downloader.Sample/   # Console sample app (download.json drives URLs)
  .editorconfig                # Authoritative code-style rules (see skill)
```
`.editorconfig` lives in `src/`, not the repo root.

---

## Key source files (`src/Downloader/`)

| File | Role |
|---|---|
| `AbstractDownloadService.cs` | Base class: lifecycle, events, pause/resume/cancel, progress aggregation, `Serializer` |
| `DownloadService.cs` | Concrete impl: `StartDownload()`, parallel/serial dispatch, auto-resume, terminal-state rule |
| `DownloadConfiguration.cs` | All settings; `INotifyPropertyChanged`, `ICloneable` |
| `RequestConfiguration.cs` | Per-request HTTP knobs: headers, proxy, auth, cookies, redirect, user-agent |
| `Request.cs` | HTTP request model; URL→filename extraction |
| `SocketClient.cs` | `HttpClient`/`SocketsHttpHandler` wrapper: header fetch, range-support probe, **redirect handling**, file-size + filename extraction |
| `DownloadPackage.cs` | Live state snapshot: chunks, file size, status, storage handle; `TrySetCompleteState()` |
| `Chunk.cs` | One file segment: start/end/position, retry counter, timeout |
| `ChunkHub.cs` | Splits the file into N chunks honoring `MinimumChunkSize` |
| `ChunkDownloader.cs` | Downloads one chunk: retry/backoff loop, pause support, `ArrayPool` reads, throttle wiring |
| `ConcurrentStream.cs` | Thread-safe write stream with a background `Watcher` task |
| `ConcurrentPacketBuffer.cs` | Bounded async queue of `Packet`s (back-pressure for memory cap) |
| `ThrottledStream.cs` | Enforces per-chunk bandwidth limit |
| `Bandwidth.cs` | Tracks instantaneous + average speed |
| `PauseTokenSource.cs` / `PauseToken.cs` | Pause primitive over `SemaphoreSlim` |
| `DownloadBuilder.cs` | Fluent API: `DownloadBuilder.New().WithUrl(...).Build().StartAsync()` |
| `Extensions/ExceptionHelper.cs` | `IsMomentumError` (retry classification), `IsRedirectError`, cert validation |
| `Extensions/UrlHelper.cs` | `EnsurePathEncoded` — normalizes attacker-influenceable URLs before `new Uri()` |

---

## Architecture overview

### Download flow

```
DownloadFileTaskAsync(url, path)
  └── InitialDownloader()            // SocketClient, Request, ChunkHub, semaphores
  └── StartDownload()
        ├── GetFileSizeAsync()       // probe via SocketClient (Range: 0-0)
        ├── IsSupportDownloadInRange()
        ├── ProvideDownloadOnFile()
        │     └── TryResumeFromExistingFile()   // read metadata appended to .download file
        ├── Package.BuildStorage()   // ConcurrentStream over FileStream or MemoryStream
        ├── ChunkHub.SetFileChunks()
        ├── ParallelDownload() | SerialDownload()
        │     └── DownloadChunk() × N → ChunkDownloader.Download()
        │           └── ReadStream() → ConcurrentStream.WriteAsync()
        └── SendDownloadCompletionSignal()
              └── Package.TrySetCompleteState()  // truncate metadata, rename .download → final
```

### Producer–consumer for writes

`ChunkDownloader` rents buffers from `ArrayPool<byte>.Shared`, wraps them in `Packet`s (buffer +
position) and pushes to `ConcurrentPacketBuffer`. A single `LongRunning` `Watcher` task in
`ConcurrentStream` dequeues packets, writes them to the underlying stream in order, and returns the
buffers. `MaximumMemoryBufferBytes` bounds the queue (back-pressure).

### Auto-resume (`.download` files)

The library **always** downloads to a temp file with `DownloadFileExtension` (default `.download`),
then renames on success — independent of `EnableAutoResumeDownload`.

When `EnableAutoResumeDownload = true`:
- During download (~1 s cadence) `Package` state is serialized (JSON→binary) and **appended after**
  `TotalFileSize` inside the `.download` file.
- On resume: bytes past `TotalFileSize` = metadata → deserialize, verify `TotalFileSize` matches the
  server, restore `Chunks[]`. Any mismatch → discard and download fresh.
- On completion: `SetLength(TotalFileSize)` strips metadata, then `File.Move` to the final name.

---

## HTTP layer & redirects (`SocketClient.cs`)

`SocketClient` builds the `HttpClient` (or uses `CustomHttpClientFactory` / a custom
`HttpMessageHandler`), probes range support, and extracts file size + filename.

`FetchResponseHeaders()` is the single entry point that populates `ResponseHeaders` (cached — it
early-returns when already populated). It first tries with `Range: 0-0`, then falls back:
- **Range rejected** (`416`, or a non-redirect error while `addRange`) → retry **without** Range
  (issue #220: some servers 403/404/503 on Range).
- **Redirect error** (`3xx` surfaced as `HttpRequestException` because `SocketsHttpHandler`
  declined to auto-follow — e.g. an **https→http downgrade**, or a challenge) → read `Location`,
  normalize via `UrlHelper.EnsurePathEncoded`, **clear `ResponseHeaders`**, and re-probe the new
  address, bounded by `MaximumAutomaticRedirections`.

### Redirect / cookie-challenge handling (important)

- `AllowAutoRedirect = true` and `MaximumAutomaticRedirections = 50` by default;
  `SocketsHttpHandler` auto-follows ordinary `301/302/303/307/308`.
- **Same-URL "307 → self" challenges** (ArvanCloud / Cloudflare-style cookie walls) are now
  followed too. The previous guard refused redirects whose `Location` equals the current URL; that
  has been replaced by a bounded attempt counter (`_redirectAttempts < MaximumAutomaticRedirections`).
- The manual fallback **clears `ResponseHeaders` before recursing** — otherwise the
  "already populated" guard short-circuits and the redirect target is never actually fetched.
- `RequestConfiguration.CookieContainer` now **defaults to a new `CookieContainer`** so a
  `Set-Cookie` issued mid-redirect is captured and replayed on the retry (set it to `null` to
  disable cookies). This is what lets a JS-free cookie wall resolve.
- **Limits:** none of this defeats a *JavaScript* challenge (the cookie value must be computed by a
  browser) or an **expired** signed link — those return a small HTML "expired/blocked" page with
  `200`, which the library would store as the file. When diagnosing "won't download" URLs, always
  inspect the real response headers/body first (`curl -I`, `-r 0-0`).

`ExceptionHelper.IsRedirectStatus` recognizes `Moved(301)`, `Redirect(302)`, `RedirectMethod(303)`,
`TemporaryRedirect(307)`, `PermanentRedirect(308)`.

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
    MaximumMemoryBufferBytes = 0, // 0 = unlimited (ConcurrentPacketBuffer cap)
    EnableAutoResumeDownload = false,
    DownloadFileExtension = ".download",
    FileExistPolicy = FileExistPolicy.Delete,
    EnableLiveStreaming = false,
    RangeDownload = false,        // download a byte slice only
    CheckDiskSizeBeforeDownload = true,
    CustomHttpClientFactory = null,         // bypass ALL internal HttpClient setup
    CustomHttpMessageHandlerFactory = null, // replace handler, keep header config
    RequestConfiguration = {
        AllowAutoRedirect = true,           // follows 3xx including 307/308
        MaximumAutomaticRedirections = 50,
        CookieContainer = new CookieContainer(), // default; null disables cookies
        // Accept, Headers, UserAgent, Proxy, Authorization, KeepAlive, ...
    }
}
```
`CustomHttpClientFactory` wins over `CustomHttpMessageHandlerFactory` when both are set.

---

## Status & terminal-state rules

`DownloadStatus`: `None → Created → Running → Paused → Completed / Stopped / Failed`.

`DownloadPackage.TrySetCompleteState()` is idempotent — once `Failed`/`Completed`, state cannot
change. This serializes races between parallel chunk errors.

**Stopped vs Failed:** status is **`Stopped`** when `IsCancelled == true` OR the exception is
`OperationCanceledException`/`TaskCanceledException` (including exceptions thrown *during* an
in-flight cancellation — commit `e4ed715`). Any other exception → **`Failed`**. When fixing
cancellation bugs, check the cancellation flag, not just the exception type. (issue #225)

**Retry classification** lives in `ExceptionHelper.IsMomentumError` — retry on transport faults
(`SocketException`, `IOException`/`HttpIOException`, timeouts, `ObjectDisposedException`) and on
transient/overload/redirect status codes (`408/428/429/503/504`, `300/301/302/303/307/308`).
Classify by **type/status only — never `Exception.Source`** (empty under AOT/trimming — issue #226).

### FileExistPolicy

`IgnoreDownload=0` (skip) · `Delete=1` (overwrite, default) · `Exception=2` (throw
`FileExistException`) · `Rename=3` (numeric suffix).

---

## Exceptions (`Downloader/Exceptions/`)

| Exception | When |
|---|---|
| `IncompleteDownloadException` | Chunk closed early — `Position < Length` after the read loop. Triggers retry while `MaxTryAgainOnFailure > 0`; only checked for known-length chunks. (issue #231) |
| `FileExistException` | `FileExistPolicy.Exception` and target exists. Extends `IOException`; `.Name` = path. |

---

## Events (raised by `AbstractDownloadService`)

- `DownloadStarted` — once, after file size + storage are ready
- `ChunkDownloadProgressChanged` — per chunk read (raw chunk bytes/speed)
- `DownloadProgressChanged` — aggregated across chunks, raised *after* the chunk event
- `DownloadFileCompleted` — terminal; `Error`/`Cancelled` reflect `Failed`/`Stopped`

Handlers run **synchronously on the chunk download thread** — slow handlers stall the download.
`DownloadFileTaskAsync`/`StartAsync` **do not throw** on failure/cancel; the outcome arrives via
`DownloadFileCompleted` (and `Package.Status`). `ActiveChunks` comes from
`ParallelSemaphore.CurrentCount`.

### Unknown-size downloads (no `Content-Length`)

`TotalFileSize` starts at 0. `RaiseProgressChangedEvents()` must not report 100% prematurely — it
grows `TotalFileSize` only when received exceeds advertised, and `ProgressPercentage` stays 0 until
completion finalizes the real size (`TotalFileSize = ReceivedBytesSize`). (issue #230, commit `e4e50c9`)

---

## Storage abstraction

`Package.BuildStorage()`:
- `DownloadingFileName` null/whitespace → `MemoryStream`
- otherwise → `FileStream` preallocated to `TotalFileSize`

`ConcurrentStream` lazily creates the underlying stream on first write (double-checked lock). The
`Watcher` task is `LongRunning` and returns `ArrayPool` buffers after each ordered write.

**Hazard:** `ConcurrentStream.Dispose()` does **not** await the watcher or drain buffered packets —
prefer `DisposeAsync()` to flush cleanly. The `_disposed` flag is volatile, checked under lock.

---

## Test infrastructure

`DummyHttpServer.HttpServer` is a singleton guarded by `SemaphoreSlim StartLock(1,1)`.
`DummyFileHelper`'s static ctor calls `HttpServer.Run(0)`, so referencing it auto-starts the server
on a dynamic port. The test project targets **net10.0** only; the library multi-targets net8/9/10.

`DummyFileController` endpoints used by integration tests:

| Route pattern | Purpose |
|---|---|
| `/dummyfile/file/size/{size}` | Plain file, no Content-Disposition |
| `/dummyfile/file/{name}?size=...` | With filename header |
| `/dummyfile/file/{name}/size/{size}/norange` | Advertises **no** range support |
| `/dummyfile/file/{name}/redirect?size=...` | 308 redirect (same host) |
| `/dummyfile/file/{name}/cookie-challenge?size=...` | **307 → self + Set-Cookie**; serves file only on the cookie-bearing retry |
| `/dummyfile/file/size/{size}/failure/{offset}` | Throws mid-stream |
| `/dummyfile/file/size/{size}/timeout/{offset}` | Delays mid-stream |
| `/dummyfile/file/{name}/size/{size}/truncate/{actualSize}` | Advertises full size, delivers partial (issue #231) |
| `/dummyfile/file/{name}/size/{size}/failrange` | 503 on every Range req; full file on no-Range fallback (issue #231) |
| `/dummyfile/file/{name}/check-useragent?size=...` | 428 on invalid/AOT User-Agent (issue #226) |

Build a URL via the matching `DummyFileHelper.Get*Url(...)` helper rather than hand-writing it.
Chunk events fire concurrently across N parallel chunks — collect progress thread-safely and wait on
a cancellation trigger rather than relying on event ordering (commit `3ddb9fd`).

### Build & test

```bash
dotnet test src/Downloader.Test/                 # auto-starts DummyHttpServer in-process
docker-compose -p downloader up                  # run tests via compose
dotnet publish -r linux-x64 -f net8.0 -c Release src/Downloader/   # AOT native build
```

---

## Recurring bug patterns (lessons from history)

- **Early stream close looks like success** — server disconnects don't throw; validate
  `Chunk.Position == Chunk.Length` after the read loop. (issue #231)
- **Unknown Content-Length** — never compute percentage off `TotalFileSize` until it's finalized;
  guard the 0/0 case. (issue #230)
- **Cancellation during cancellation** — exceptions during an in-flight cancel must map to
  `Stopped`, not `Failed`; check `IsCancelled` first. (issue #225)
- **AOT vs JIT divergence** — never branch on `Exception.Source` (empty under AOT). (issue #226)
- **Redirect target silently lost** — clear `ResponseHeaders` before re-probing a redirect, or the
  "already populated" guard skips the fetch. Same-URL `307` challenges need cookies to resolve.

---

## Serializer

`AbstractDownloadService.Serializer` is a public `IBinarySerializer` (default
`JsonBinarySerializer`) — swappable. Used to read/write auto-resume metadata.

---

## Patterns to be aware of

- **`ArrayPool<byte>.Shared`** — rented in `ChunkDownloader.ReadStream()` and
  `DownloadService.TryResumeFromExistingFile()`; ownership transfers to `Packet`, returned on
  `Packet.Dispose()`.
- **`INotifyPropertyChanged` on `DownloadConfiguration`** — `ChunkDownloader` listens for
  `MaximumBytesPerSecond` changes to retune the live throttle.
- **`SingleInstanceSemaphore`** in `AbstractDownloadService` blocks concurrent `StartDownload` on
  one service instance.
- **AOT** — keep it reflection-free; classify exceptions by type/status, not metadata strings.
