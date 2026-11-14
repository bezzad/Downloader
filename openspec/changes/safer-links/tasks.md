## 1. Tests first (must fail on the current code)

- [ ] 1.1 M5: https→http redirect with the option off → fails (or a warning is logged before the major version).
- [ ] 1.2 M6: server changes the file mid-download → no `Completed` with mixed bytes.
- [ ] 1.3 M7: redirect target returns 403 after the first chunk → the original URL is asked again.
- [ ] 1.4 M12: no challenge, `PreAuthenticate = false` → no `Authorization` header.
- [ ] 1.5 L4: first URL down → completes from the second.

## 2. Fix

- [ ] 2.1 M5: `AllowInsecureRedirect`.
- [ ] 2.2 M6: `If-Range` + `Content-Range` total check.
- [ ] 2.3 M7: re-resolve the original URL once.
- [ ] 2.4 M12: honour `PreAuthenticate`.
- [ ] 2.5 L4: URL failover.

## 3. Finish

- [ ] 3.1 README and CHANGELOG.
- [ ] 3.2 Build with 0 warnings; all tests green.
