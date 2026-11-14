## Why

From the review of 7.0.1 (2026-11-11): issues **M15, L10, L12, L13, L14**.

- **M15 (Medium).** The stable NuGet package is built with a preview SDK and ships a `net11.0` target
  made against preview libraries (`release.yml`, `Downloader.csproj`, `global.json`).
- **L10 (Low).** `DownloadConfiguration.Clone()` copies the `PropertyChanged` listeners, and
  `ActiveChunks` lives in the shared configuration, so two services on one configuration change each
  other's speed share.
- **L12 (Low).** Small code smells: `lock (this)`; a `LongRunning` task for an async method;
  `Thread.Sleep` up to 3 seconds in `DeleteFile` on an async path; `KeepAlivePingTimeout` set with no
  ping delay (no effect); `TransferEncoding` sent on a GET; `ContentType` added to `Accept`; log
  messages built with `$"..."`; a size check that compares a chunk position with the whole file size
  (never true, `AbstractDownloadService` line ~460).
- **L13 (Low).** Package hygiene: no nullable annotations on the public API, no package validation
  against the last version, no deterministic CI build flag, and `PublishAot`/`PublishTrimmed` in a
  library project. The release job tests only on Ubuntu.
- **L14 (Low).** Docs are behind the code. `src/CLAUDE.md` still lists the 16 old issues as "not fixed
  yet" and says handlers run on the chunk thread. The 16 OpenSpec changes of 2026-10-29 are finished but
  not archived, and `openspec/specs` is empty.

## What Changes

- M15: build releases with the stable SDK; drop `net11.0` until .NET 11 is final (the `net10.0` build
  runs on it).
- L10: clear the event in the copy; keep `ActiveChunks` in the download plan.
- L12: clean up one by one when the file is touched.
- L13: `EnablePackageValidation` with a baseline version, `ContinuousIntegrationBuild`; keep only
  `IsAotCompatible`; test the release on all three OSes.
- L14: update `src/CLAUDE.md`, then sync and archive the 16 old changes (a separate step, not mixed with
  the new changes).

## Capabilities

### New Capabilities
- `housekeeping`: build, package and docs rules.

## Impact

- `.github/workflows/release.yml`, `src/global.json`, `Downloader.csproj`, `DownloadConfiguration.cs`,
  several small files, `src/CLAUDE.md`, `openspec/`.
