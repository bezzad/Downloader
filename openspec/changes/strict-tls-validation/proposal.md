## Why

**Priority: High (security). BREAKING.**

`SocketClient` sets `handler.SslOptions.RemoteCertificateValidationCallback =
ExceptionHelper.CertificateValidationCallBack` (`src/Downloader/SocketClient.cs`, line 70). That
callback (`src/Downloader/Extensions/ExceptionHelper.cs`, end of file) accepts certificates that the
platform rejects:

- Any chain with `NotTimeValid` (expired or not yet valid) returns `true` at once, even when the same
  chain has other errors.
- Any self-signed certificate (`Subject == Issuer` with `UntrustedRoot`) is accepted.
- The chain branch runs whenever `RemoteCertificateChainErrors` is set, so a host-name mismatch in the
  same flags is ignored.

So anyone on the network (public Wi-Fi, a bad proxy) can show a self-signed certificate for **any**
host, and the download goes on. The file can be replaced, and an `Authorization` token or password is
sent to the attacker. This is on by default for every user of the library. The code comment is copied
from an old Exchange-server sample.

## What Changes

- Remove the custom callback. The default platform validation is used.
- Callers who really need to trust a private or self-signed certificate already have a way:
  `CustomHttpMessageHandlerFactory` with their own `SocketsHttpHandler` and callback. Document it with
  a short README sample. No new option (YAGNI).
- **BREAKING:** downloads from servers with an expired, self-signed or wrong-host certificate now fail.

## Capabilities

### New Capabilities
- `tls`: how server certificates are checked.

## Impact

- `src/Downloader/SocketClient.cs`, `src/Downloader/Extensions/ExceptionHelper.cs`,
  `src/Downloader.Test/HelperTests/ExceptionHelperTest.cs`, README.
- Needs a version decision by the owner (breaking behavior; maybe a minor version with a clear note,
  or 6.0).
