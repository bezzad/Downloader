## 1. Tests first (must fail on the current code)

- [ ] 1.1 Dummy-server endpoint: `307` with `Location: /dummyfile/file/{name}?size=...` (relative).
- [ ] 1.2 Test that reaches the manual redirect path (custom handler with `AllowAutoRedirect = false`) and expects a complete file.

## 2. Fix

- [ ] 2.1 Resolve `Location` against `request.Address` in the manual path.
- [ ] 2.2 Same in `GetRedirectUrl`.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
