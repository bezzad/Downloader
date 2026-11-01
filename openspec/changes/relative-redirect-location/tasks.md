## 1. Tests first (must fail on the current code)

- [x] 1.1 (Reused the existing `file/{name}/redirect` endpoint: its `Location` is already relative.) Dummy-server endpoint: `307` with `Location: /dummyfile/file/{name}?size=...` (relative).
- [x] 1.2 Test that reaches the manual redirect path (custom handler with `AllowAutoRedirect = false`) and expects a complete file.

## 2. Fix

- [x] 2.1 Resolve `Location` against `request.Address` in the manual path.
- [x] 2.2 Same in `GetRedirectUrl`.

## 3. Finish

- [x] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
