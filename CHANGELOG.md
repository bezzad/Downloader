# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and this project adheres to
[Semantic Versioning](https://semver.org/).

## [7.0.2] - 2026-11-14

### 🐛 Fixes

- **`FlushAsync` is a real flush while a download runs (#259).** Before, it waited until the write queue was empty. While chunks write faster than the disk, the queue is never empty, so the flush did not end. Now it waits only for the blocks given before the call, then flushes the file to the disk (`FileStream.Flush(true)`, not only to the OS cache). The flush runs on the writer task, so it never runs at the same time as a write. When the stream is closed before the flush ends, `FlushAsync` throws `ObjectDisposedException` instead of reporting dropped bytes as written. The 20 ms wait after each flush is gone.
- To save a package for resume while it downloads: serialize it first, then `await package.FlushAsync()`, then save the bytes. The saved chunk positions then never count bytes that are not on the disk.

## [7.0.1] - 2026-11-10

### 🐛 Fixes

- **The disk space check measured the wrong disk.** It always asked `/` (on Windows, the drive letter), so a download to another mount was checked against the wrong free space. In a Snap or Flatpak sandbox, `/` is a small private mount, so a 21.7 GB download into `~/Downloads` failed with "not enough space" while `/home` had 145 GB free. Now the check finds the file system that really holds the target folder: it follows symbolic links, uses the nearest folder that exists, and picks the longest matching mount (a separate `/home`, `/media/*` USB drives, NFS, Docker volumes, sandboxes).
- **A resumed download needs only the missing bytes.** The check asked for the whole file size even when most of it was already on disk. Now it asks for `TotalFileSize - ReceivedBytesSize`.
- **A clear message.** The old message showed the free bytes as if they were the needed size. Now it reads: `Not enough disk space for '<file>': needs <N> more bytes, <F> bytes free on '<mount>'.` It is still an `IOException`.

## [7.0.0] - 2026-11-07

Progress events are now raised one call at a time, in order (#256). It has breaking changes (marked BREAKING below), so the major version goes up.

### 🔄 Changed

- **`DownloadProgressChanged` is raised one call at a time, in order (#256).** Before, every chunk thread raised it, so with a parallel download the handler ran on several threads at once, an older total could arrive after a newer one, and Rx observers broke. Now the storage writer reports the bytes it has written, and one deliverer on the thread pool raises the event. The download never waits for the handler: a slow handler gets merged updates (`ProgressedByteSize` is the sum since the last one), and the last update can arrive after `DownloadFileCompleted`. A handler that throws is logged and the download goes on. `ChunkDownloadProgressChanged` does not change: it still runs on each chunk's thread, so use it if you must stop a download at an exact point.
- **BREAKING (behaviour): live streaming uses one chunk.** With `EnableLiveStreaming = true` the download uses one connection, so `ReceivedBytes` comes in file order, and every block reaches the handler. When the waiting blocks pass `MaximumMemoryBufferBytes`, the download waits for the handler (`0` = no limit).
- **BREAKING: `EnableLiveStreaming` can not change while a download runs.** Setting it to a new value then throws an `InvalidOperationException`.
- **BREAKING (API): `ConcurrentStream.Write` and `ConcurrentStream.WriteAsync` are internal now.** Only the download itself writes to its storage; these methods were public only because the class is.

### 🐛 Fixes

- **A download could hang forever when the memory buffer was full.** If the writer emptied the buffer at the same moment a chunk stopped adding to it, nothing started the adding again. This happens most with a small `MaximumMemoryBufferBytes`.

## [6.0.1] - 2026-11-03

### 🐛 Fixes

- **A pause while the download is starting is kept again.** 6.0.0 made `Pause()` and `Resume()` do nothing unless the download was Running or Paused, so a `Pause()` while the server was still asked for the size (status None or Created) was lost and the download ran on. Now only a finished download (Completed, Failed or Stopped) ignores them.

## [6.0.0] - 2026-11-02

A big safety and reliability release. It has two breaking changes (marked BREAKING below), so the major version goes up. Version 5.11.0 was never published; its fixes are part of this release.

### 🔒 Security

- **BREAKING: server certificates are checked strictly.** The library had its own certificate check that accepted expired, self-signed and wrong-host certificates, so anyone on the network could replace the file or read your credentials. Now the normal platform check is used, and a certificate error is not retried. If you must trust a private or self-signed certificate, give your own handler with `CustomHttpMessageHandlerFactory` (see the README).
- **A server file name can no longer write outside your folder.** The name from `Content-Disposition` (for example `../../.bashrc`, `/etc/x` or `C:\x.exe`) is cleaned: only the last part is kept, and characters that are not allowed in a file name become `_`. `filename*=UTF-8''...` names are now read, and `FileName=` is matched without caring about case.
- **Credentials are not sent to another host after a redirect.** The `Authorization` header (from `RequestConfiguration.Authorization` or `Credentials`) is sent only to the scheme, host and port you asked for. After a redirect to a CDN, or from https to http, it is left out.

### 🔄 Changed

- **BREAKING: the system proxy is used when `RequestConfiguration.Proxy` is null.** Before, no proxy was used at all, so downloads failed on networks that need one. Now the OS proxy settings (or `HTTP_PROXY` / `HTTPS_PROXY` / `NO_PROXY`) are used, like `HttpClient` does. To use no proxy, set `Proxy = new WebProxy()`.

### 🐛 Fixes

- **A server that ignores ranges no longer breaks the file.** When a server (or one of the mirror URLs) said it supports ranges but sent the whole file to a chunk request, those bytes were written in the middle of the file and the download reported Completed. Every chunk now checks the answer. On a wrong answer the download starts again once over one connection; a `RangeDownload` fails with the new `InvalidRangeResponseException`.
- **Auto-resume no longer mixes two versions of a file.** The `ETag` and `Last-Modified` headers are saved with the resume state (new `PackageInfo.ETag` / `LastModified`, also on `RemoteFileInfo`). If the server file changed but kept its size, the download starts again instead of mixing old and new bytes.
- **Auto-resume reads its state correctly.** The saved state now starts with its length, so a shorter state written over a longer one is read right (before, the download silently started again from 0). Old `.download` files are still read.
- **Bytes that were not written are not counted.** A write to a closed storage threw nothing and the chunk counted the bytes anyway, which could leave holes after a manual resume. Now the write throws `ObjectDisposedException`, the rented buffer is returned, and the packet is not left in the queue.
- **Your configuration is no longer changed by a download.** For a small file, a server without range support, or a range download, the library wrote `ChunkCount`, `ParallelCount`, `ParallelDownload`, `RangeLow`/`RangeHigh` and `CustomHttpClientFactory` back into your `DownloadConfiguration`, so the next download with the same object was slow. These values are now kept per download. `DownloadConfiguration.Clone()` now also copies `RequestConfiguration` (headers and client certificates are new lists; cookies, proxy and credentials stay shared).
- **Long retry runs are safe.** A chunk retry called itself again, so many failures in a row made a deeper and deeper call chain. It is now a loop. The read timeout that grows on every retry is capped at 60 seconds (or `BlockTimeout` if that is bigger); before, it could overflow to a negative value.
- **`ParallelDownload = false` now really uses one connection.** All chunks were started at once and only waited on a semaphore of `ParallelCount`, so a "serial" download opened up to `ParallelCount` connections. Now chunk `i + 1` starts after chunk `i` is done. Serial downloads can be slower if you got parallel speed by accident before.
- **Range download past the end of the file now ends.** `RangeHigh` is the last byte index, but it was clamped to the file size instead of size − 1, so the download waited for one byte that does not exist and retried for ever. A reversed range (`RangeHigh < RangeLow`) or a `RangeLow` past the end now fails the download with `ArgumentOutOfRangeException` instead of being changed silently.
- **A relative redirect `Location` works.** When the library follows a redirect by itself (for example https → http), a relative `Location` such as `/files/a.zip` threw `UriFormatException`. It is now resolved against the current address.
- **Error responses are disposed.** A 4xx/5xx answer was not disposed, so its connection stayed busy until the GC ran; with many retries connections piled up. Chunk requests also no longer overwrite the headers of the first probe.
- **A finished download keeps its status.** `Pause()`, `Resume()` and `CancelAsync()` on a Completed or Failed download changed the status to Running or Paused, and `IsBusy` stayed true. Now they do nothing. `Clear()` / `Dispose()` right after the start (while the server is still being asked for the size) no longer waits until the whole file is downloaded.
- **Chunks no longer leak on a shared configuration.** Every chunk subscribed to `DownloadConfiguration.PropertyChanged` and never unsubscribed, so reusing one configuration for many downloads kept all old chunks alive.
- **Stop no longer waits for the speed limit.** With `MaximumBytesPerSecond` set, a stop or cancel first waited for the throttle delay (many seconds at a low limit). The delay now ends at once when the download is cancelled.
- **`WithFileName` without a folder works.** `DownloadBuilder.New().WithUrl(url).WithFileName("a.zip")` threw `ArgumentNullException`; it now saves to that name.
- `DownloadBuilder.Build()` without a URL throws `ArgumentNullException` with `ParamName = "url"` and a clear message. `FileExistException` now has a message that names the file.

### 🔧 Under the hood

- `RequestConfiguration.ImpersonationLevel`, `Pipelined` and `UseDefaultCredentials` are now `[Obsolete]`: they never had an effect.
- `DownloadFileExtension` is cleaned in one simple step (spaces removed, dots and commas trimmed, lower case); `" .part "` now gives `.part` instead of `..part`.
- The Docker test setup uses the .NET 10 SDK.

## [5.10.2] - 2026-10-29

A small fix for pause/resume and cancel threading.

### 🐛 Fixes

- **Resume and cancel no longer run work on the wrong thread** ([#251](https://github.com/bezzad/Downloader/issues/251)). `PauseTokenSource` and the download's completion source finished their tasks inline. So `Resume()` could run the paused download work on the caller's thread (for example the UI thread), and code after `await CancelTaskAsync()` could run on the download thread before `DownloadFileCompleted` was raised. Both now use `TaskCreationOptions.RunContinuationsAsynchronously`.

## [5.10.1] - 2026-10-14

A small fix on top of 5.10.0 for downloads that use more than one url.

### 🐛 Fixes

- **Each chunk uses its own url again.** In 5.10.0 a parallel worker picked one url and used it for every chunk it downloaded, so the url that served a chunk depended on which worker took it. Now chunk `i` always uses url `i % count`, the same as before 5.10.0, so the load is spread over your mirrors in a stable way.

## [5.10.0] - 2026-10-14

Safer live settings, correct file size after a server error, and smarter splitting of the last chunks.

### ✨ New

- **Idle connections can help finish the download.** When `MinimumChunkSize` is set (greater than 0), a parallel worker that has finished its own chunk can split the largest unfinished chunk and download its second half, as long as both halves stay at least `MinimumChunkSize`. With the default `0` nothing changes. (#245, thanks @crazysmile-PhD)

### 🐛 Fixes

- **A server error on the first probe could give a wrong, tiny file reported as complete** ([#248](https://github.com/bezzad/Downloader/issues/248)). The headers of an error response (for example a 500 with a 41-byte XML body) were read as the file's headers, so the error body's length became the file size. Now only a success or a redirect fills the response headers, so the download really asks again and fails if it never reaches the file.
- **Range support is kept after a passing server error.** A 500 on the first `Range: 0-0` probe no longer switches the download to one connection; the Range probe is asked again. A transient status on the probe (503, 429, 504, 408) is retried up to 3 times instead of failing the download.
- **Settings you change during a download are thread-safe** ([#247](https://github.com/bezzad/Downloader/issues/247)). `MaximumBytesPerSecond`, `ActiveChunks`, `BufferBlockSize` and `EnableLiveStreaming` use atomic reads and writes, so a change from another thread (like a UI speed limit) is seen by the download threads at once, and `PropertyChanged` fires once per real change.

## [5.9.8] - 2026-09-22

A stop no longer leaves a `NullReferenceException` behind.

### 🐛 Fixes

- **A finished download could be reported as "Failed — Object reference not set to an instance of an object."** Both dispatch loops start every chunk task eagerly and then await them one at a time, so a `CancelAsync()` abandons every chunk the loop had not reached yet. Those chunks are still inside their read loops and still raise progress events, while the download has already run its terminal path — which closes the package storage and sets `Package.Storage` to `null`. Each progress event writes the auto-resume metadata through that storage, unguarded, so an abandoned chunk threw an NRE; when its cancellation token is no longer the one in force, that NRE becomes the download's error and reaches the consumer through `DownloadFileCompleted`. The metadata write now reads the storage once and skips it when the download is over — resume metadata for a finished transfer is worthless anyway.
- **A `Dispose()` racing a completion could swallow the completion event entirely.** `Clear()` nulls the internal `TaskCompletionSource`, and an app that releases its engine as soon as a download ends can run that concurrently with the completion signal. Reporting that a download is over no longer depends on that field still being there.
- **`ActiveChunks` no longer throws** when the semaphore it counts has already been disposed by `Clear()`; a torn-down download reports no active chunks instead.
- **`DownloadPackage.FlushAsync`/`CloseAsync`** read `Storage` once instead of check-then-use, so a flush that races a close cannot dereference `null`.

### 🔧 Under the hood

- `StopRaisesNoNullReferenceTest` covers the race deterministically (it raises 7 NREs against 5.9.7 and none against this release), and an opt-in soak (`RetryAfterStopStressTest`, enabled with `NRE_STRESS_ITERATIONS`) walks stop → dispose → retry across cancel point, chunk count, parallel on/off, server variant and throttled/instant.
- Fixed a lost-update race in two integration tests that recorded peak progress with a non-atomic `Math.Max`.

---

Found from a download manager whose retried-after-stop row was shown as failed even though the file was complete on disk ([Downloader.Desktop](https://github.com/bezzad/Downloader.Desktop)).

## [5.9.7] - 2026-09-21

Adds `RemoteFileInfo.ContentType`: the server's `Content-Type` header (or `null` when the server
sends none), exposed alongside file name/size/range-support from the same no-extra-request probe —
useful for callers that need to classify a file whose name carries no usable extension.

## [5.9.6] - 2026-08-31

A download now always reports how it ended.

### 🐛 Fixes

- **A download could finish without ever raising `DownloadFileCompleted`.** `StartDownload` ended in one of four branches, and the last one — an "unexpected" terminal state — only logged a warning and returned. For anything driven by the events (rather than only awaiting the task) that meant the download never ended: no error, no file, nothing to retry, and a UI left showing it as still in progress. That branch now always sends a completion signal.
- **Pausing exactly as the last bytes arrive no longer throws the download away.** A `Pause()` that lands after every byte has been received leaves the status Paused with nothing left to do — a finished download. It is now reported as **Completed** instead of falling into the unexpected-state branch and being discarded.
- Any other unexpected terminal state is reported as **Failed** with an `IncompleteDownloadException` naming the state and the byte counts, so the cause is visible instead of silent.

### 🔧 Under the hood

- New integration tests (`CompletionSignalTest`) cover all three terminal paths — completed, cancelled, and paused-at-the-finish-line. The last one fails against 5.9.5.

---

Found while tracing a download manager whose row stayed "downloading" forever against a server that refused its requests ([Downloader.Desktop #9](https://github.com/bezzad/Downloader.Desktop/issues/9)).

## [5.9.5] - 2026-07-20
### Fixed
- Rare `IOException: The process cannot access the file … .download` when finalizing a download (issue #239): downloader-owned file deletions now retry with exponential backoff (~3s) to ride out transient external locks (e.g. antivirus real-time scans); a permanent lock now fails with a clear error attributing it to the external process, original exception preserved as `InnerException`.
- `RemoteFileResolver.GetFileInfoAsync` no longer rethrows the internal connect-timeout cancellation as a hard failure on slow/unreachable hosts; it falls back to the URL-derived file name as documented.
### Added
- .NET 11 (preview) target (`net11.0`) for the library, validated by CI on Ubuntu/macOS/Windows.

## [5.9.4] - 2026-07-10
### Fixed
- Resume after a failed attempt no longer restarts from 0%: a transient transport error (timeout, dropped connection, 503/504) no longer discards already-downloaded chunk progress when the single-connection fallback engages. A failed attempt now stays resumable from its last position.
- Silent file corruption when resuming a multi-chunk download against a server that reports no range support: the stale chunk layout is now rebuilt so restarted bytes are written at the correct offsets.
- Off-by-one in the chunk-completion check that treated a chunk left exactly one byte short as complete, which could fail a download near 100% and make its final byte impossible to resume.

## [5.9.1] - 2026-07-10
### Fixed
- Release packaging: stop building/pushing an empty symbol package; keep the embedded-PDB symbol-package fix.

## [5.9.0] - 2026-06-19
### Added
- `RemoteFileResolver` with `GetFileNameAsync(url)` and `GetFileInfoAsync(url)`, returning `RemoteFileInfo { FileName, FileSize, SupportsRange, Address }` from a single header probe — preview a queued download's name and size without starting one.
- `IDownloadService.GetFileInfoAsync(url)` exposes the same lookup on the service.
### Changed
- Filename/size/range resolution unified behind `SocketClient.GetFileInfoAsync`, shared by the download pipeline and `RemoteFileResolver`. No behavior change to existing downloads.

## [5.8.1] - 2026-06-16
### Fixed
- Cookie handling for CDN redirects.

## [5.8.0] - 2026-06-02
### Added
- Fallback to a single connection for downloads on transient transport errors.

## [5.6.0] - 2026-06-01
### Fixed
- Download cancellation and progress reporting bugs.

## [5.4.0] - 2026-04-29
### Added
- AOT compilation support for .NET 8 and later.

## [5.3.0] - 2026-04-25
### Fixed
- URL encoding for square brackets in download paths (#223).

## [5.2.0] - 2026-04-21
### Fixed
- Servers that reject the `Range` header with 403/404/503 now fall back to a normal request (#220).

## [5.1.1] - 2026-04-20
### Fixed
- Maintenance release.

## [5.1.0] - 2026-03-11
### Added
- Custom `HttpClient` support, plus related fixes (#219).

## [5.0.0] - 2026-03-10
### Changed
- Improved resume-download functionality and error handling (#217).

## [4.1.1] - 2026-02-10
### Fixed
- Redirected-URL download fix.

## [4.1.0] - 2026-02-09
- Pre-release.

## [4.0.3] - 2025-08-09
## [4.0.2] - 2025-07-12
## [3.3.4] - 2025-03-10
## [3.3.0] - 2024-11-20
## [3.2.0] - 2024-09-22
### Fixed
- Compiler warnings.

## [3.1.2] - 2024-06-30
## [3.0.6] - 2023-06-06
## [3.0.0-beta] - 2022-10-12
## [2.4.1] - 2022-09-21
## [2.4.0] - 2022-09-16

For releases and full history, see the
[GitHub releases](https://github.com/bezzad/Downloader/releases) and
[tags](https://github.com/bezzad/Downloader/tags).

[5.9.1]: https://github.com/bezzad/Downloader/releases/tag/v5.9.1
[5.9.0]: https://github.com/bezzad/Downloader/releases/tag/v5.9.0
[5.8.1]: https://github.com/bezzad/Downloader/releases/tag/v5.8.1
[5.8.0]: https://github.com/bezzad/Downloader/releases/tag/v5.8.0
[5.6.0]: https://github.com/bezzad/Downloader/releases/tag/v5.6.0
[5.4.0]: https://github.com/bezzad/Downloader/releases/tag/v5.4.0
[5.3.0]: https://github.com/bezzad/Downloader/releases/tag/v5.3.0
[5.2.0]: https://github.com/bezzad/Downloader/releases/tag/v5.2.0
[5.1.1]: https://github.com/bezzad/Downloader/releases/tag/v5.1.1
[5.1.0]: https://github.com/bezzad/Downloader/releases/tag/v5.1.0
[5.0.0]: https://github.com/bezzad/Downloader/releases/tag/v5.0.0
