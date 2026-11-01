## 1. Tests first (must fail on the current code)

- [x] 1.1 Test: file of 1000 bytes, `RangeLow = 900`, `RangeHigh = 5000` → completes with 100 bytes (now it never ends).
- [x] 1.2 Test: `RangeLow = 500`, `RangeHigh = 100` → fails with `ArgumentOutOfRangeException` in `DownloadFileCompleted`.
- [x] 1.3 Test: `RangeLow = 2000` on a 1000-byte file → fails the same way.

## 2. Fix

- [x] 2.1 Clamp to `TotalFileSize - 1`.
- [x] 2.2 Throw for the two invalid cases instead of changing the values.

## 3. Finish

- [x] 3.1 XML docs of `RangeLow` / `RangeHigh`: say both are inclusive byte indexes.
- [x] 3.2 CHANGELOG entry.
- [ ] 3.3 Build with 0 warnings; all tests green.
