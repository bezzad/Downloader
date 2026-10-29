## Why

**Priority: Low.**

- The manual redirect path does `new Uri(UrlHelper.EnsurePathEncoded(redirectedUrl))`
  (`SocketClient.cs`, line ~247). A relative `Location` (allowed by RFC 9110, for example
  `/files/a.zip`) is returned unchanged by `EnsurePathEncoded`, and `new Uri(relative)` throws
  `UriFormatException`, so the download fails.
- `SocketClient.GetRedirectUrl` (line ~310) uses `response.Headers.Location` from a success response
  as is; a relative value would set `request.Address` to a relative `Uri` and the next request fails.

The manual path runs only when the handler did not follow the redirect itself (for example
https → http, or after the handler's own limit), so this is rare.

## What Changes

- Resolve the location against the current address: `new Uri(request.Address, encodedLocation)`.
  This works for absolute and relative values.
- Use the same rule in `GetRedirectUrl`.

## Capabilities

### New Capabilities
- `redirects`: how redirect targets are resolved.

## Impact

- `src/Downloader/SocketClient.cs`.
- Test: a dummy-server endpoint that answers a redirect with a relative `Location`, reached through
  the manual path (for example with `AllowAutoRedirect` on the handler turned off by a custom handler).
