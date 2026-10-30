## 1. Tests first (must fail on the current code)

- [ ] 1.1 Add a dummy-server endpoint that redirects to the same file on another host name, and one that reports if an `Authorization` header came in.
- [ ] 1.2 Test: with `Authorization` set, the chunk requests to the other host carry no `Authorization` header, and the file is still correct.
- [ ] 1.3 Test: with `Credentials` (NetworkCredential) set, the same is true.
- [ ] 1.4 Test: a same-host redirect still sends the header.

## 2. Fix

- [ ] 2.1 Store the origin `Uri` in `Request` when it is built.
- [ ] 2.2 In `GetRequest()`, add the auth header only when scheme, host and port equal the origin.

## 3. Finish

- [ ] 3.1 XML doc of `Authorization` and `Credentials`: say they are sent to the original host only.
- [ ] 3.2 CHANGELOG entry (security fix).
- [ ] 3.3 Build with 0 warnings; all tests green.
