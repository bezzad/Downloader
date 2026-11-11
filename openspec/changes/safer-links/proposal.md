## Why

From the review of 7.0.1 (2026-11-11): issues **M5, M6, M7, M12, L4**.

- **M5 (Medium).** A redirect from HTTPS to HTTP is followed. .NET refuses this on purpose; the library
  follows it by hand (`SocketClient.cs` lines ~236-261), so the file then comes over a link anyone on the
  network can change.
- **M6 (Medium).** A file that changes on the server during the download is not noticed. Chunks of the
  old and the new file are mixed. The other URLs of a multi-URL download are never checked to be the
  same file.
- **M7 (Medium).** The address after a redirect is kept for the whole download. Signed links (GitHub
  releases, S3) expire, and later chunks or retries then fail with 403.
- **M12 (Medium).** Credentials are always sent as a Basic header on the first request, without a
  challenge and also over plain HTTP. `PreAuthenticate` is ignored.
- **L4 (Low).** No failover between URLs. Only the first URL is asked for the file info, so the download
  fails when it is down, even if the others work.

## What Changes

- M5: add `AllowInsecureRedirect`. Default `false` in the next major version; until then log a warning.
- M6: send `If-Range` with the `ETag` (or `Last-Modified`) on chunk requests; check that the total in
  `Content-Range` is the known file size.
- M7: on 401, 403, 404 or 410 from a redirected address, ask the original URL again once.
- M12: send Basic first only when `PreAuthenticate` is true; otherwise let the handler answer the
  server's challenge.
- L4: try the next URL when the probe or a chunk fails.

## Capabilities

### New Capabilities
- `links`: how URLs, redirects and credentials are used.

## Impact

- `SocketClient.cs`, `Request.cs`, `RequestConfiguration.cs`, `ChunkDownloader.cs`, `DownloadService.cs`.
- M5 and M12 change default behavior: next major version, or an option.
