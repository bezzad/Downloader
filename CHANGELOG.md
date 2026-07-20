# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/), and this project adheres to
[Semantic Versioning](https://semver.org/).

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
