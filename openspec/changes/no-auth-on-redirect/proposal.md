## Why

**Priority: Medium-High (security).**

`Request.GetRequest()` (`src/Downloader/Request.cs`) adds an `Authorization` header to every request:
the `RequestConfiguration.Authorization` value, or a Basic header built from `Credentials`.

After a redirect the library changes `request.Address` to the final URL:
- `SocketClient.EnsureResponseAddressIsSameWithOrigin` (`SocketClient.cs`, around line 294) after the
  handler followed the redirect, and
- the manual redirect path (`SocketClient.cs`, around line 247), used for example on an https → http
  downgrade.

Every chunk request then goes **straight to the redirect target with the `Authorization` header**.
.NET removes that header when it follows a redirect by itself; the library adds it back. Results:

- A token or password is sent to another host (for example a release page that redirects to a CDN).
- After an https → http redirect it is sent in plain text.
- Some signed-URL storage (S3-style) refuses a request that has both a signed query and an
  `Authorization` header, so the download fails with 400.

## What Changes

- `Request` remembers the address the caller gave (the origin).
- `GetRequest()` adds the `Authorization` header only when the current address has the same scheme,
  host and port as the origin. Never on https → http.
- Everything else stays the same (cookies are already handled per host by `CookieContainer`).

## Capabilities

### New Capabilities
- `http-auth`: when credentials are sent.

## Impact

- `src/Downloader/Request.cs` (and maybe `SocketClient.cs` if the origin is set there).
- Tests: a dummy-server endpoint that records whether `Authorization` arrived, plus a redirect to a
  second host name (for example `127.0.0.1` ↔ `localhost`).
- A caller who needs the header on another host can set it in `RequestConfiguration.Headers` on purpose.
