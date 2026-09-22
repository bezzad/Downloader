# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and this project adheres to
[Semantic Versioning](https://semver.org/).

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
