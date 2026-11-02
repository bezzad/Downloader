## 1. Decide

- [x] 1.1 Owner: confirm the breaking change and the version number for it. (Owner, 2026-11-02: "do all" = yes, 6.0.0.)

## 2. Tests first (must fail on the current code)

- [x] 2.1 Start a local HTTPS test server with a self-signed certificate (a small `SelfSignedHttpsServer` in the test helpers, generated at test start).
- [x] 2.2 Test: a download from it fails with a TLS error by default.
- [x] 2.3 Test: the same download works when the caller gives `CustomHttpMessageHandlerFactory` with its own validation callback.

## 3. Fix

- [x] 3.1 Remove the `RemoteCertificateValidationCallback` line in `SocketClient.GetSocketsHttpHandler`.
- [x] 3.2 Delete `ExceptionHelper.CertificateValidationCallBack` and its tests.
- [x] 3.3 A TLS error (`HttpRequestError.SecureConnectionError`) is not retried; with the default unlimited retries it would loop for ever.

## 4. Finish

- [x] 4.1 README: add "Private or self-signed certificates" with the handler-factory sample.
- [x] 4.2 CHANGELOG: security fix, marked breaking.
- [x] 4.3 Build with 0 warnings; all tests green.
