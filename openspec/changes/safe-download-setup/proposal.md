## Why

From the review of 7.0.1 (2026-11-11): issues **H1, L1, L2**.

- **H1 (High). A setup error makes `Dispose()` and the next download hang for ever.**
  `InitialDownloader` sets the status to `Created` and makes the completion task, and only then builds
  the HTTP client and the requests. If that step throws, the exception goes to the caller, the status
  stays `Created`, and the completion task is never finished. After that `Dispose()`, `Clear()` and any
  new `DownloadFileTaskAsync` on the same service wait for ever, because `Clear()` waits for that task.
  Easy ways to hit it: a header like `Content-Type` in `RequestConfiguration.Headers`, a bad
  `UserAgent` or `Referer`, `ConnectTimeout = 0`, or a `CustomHttpClientFactory` that throws. A
  `using var service` then freezes the app.
- **L1 (Low).** A URL without a scheme is turned into `http://localhost/...` and requested
  (`example.com/file.zip` became `http://localhost/example.com/file.zip`).
- **L2 (Low).** A second `DownloadFileTaskAsync` on a busy service cancels the first one without a word.

## What Changes

- H1: a setup error sets the status `Failed` and ends the completion task. It still goes to the caller
  (as today), but the service stays reusable: `Dispose()` and the next download do not hang.
- L1: `Request` throws `ArgumentException` for a URL that is not absolute `http`/`https`.
- L2: `DownloadFileTaskAsync` throws `InvalidOperationException` while a download runs.

## Capabilities

### New Capabilities
- `download-setup`: what happens before the first request is sent.

## Impact

- `AbstractDownloadService.cs` (`InitialDownloader`, `Clear`), `Request.cs`.
- L1 and L2 change behavior (an exception instead of a silent wrong action): note them in the CHANGELOG.
