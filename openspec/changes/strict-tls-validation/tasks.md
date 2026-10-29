## 1. Decide

- [ ] 1.1 Owner: confirm the breaking change and the version number for it.

## 2. Tests first (must fail on the current code)

- [ ] 2.1 Start a local HTTPS test server with a self-signed certificate (Kestrel in the dummy server, generated at test start).
- [ ] 2.2 Test: a download from it fails with a TLS error by default.
- [ ] 2.3 Test: the same download works when the caller gives `CustomHttpMessageHandlerFactory` with its own validation callback.

## 3. Fix

- [ ] 3.1 Remove the `RemoteCertificateValidationCallback` line in `SocketClient.GetSocketsHttpHandler`.
- [ ] 3.2 Delete `ExceptionHelper.CertificateValidationCallBack` and its tests.

## 4. Finish

- [ ] 4.1 README: add "Private or self-signed certificates" with the handler-factory sample.
- [ ] 4.2 CHANGELOG: security fix, marked breaking.
- [ ] 4.3 Build with 0 warnings; all tests green.
