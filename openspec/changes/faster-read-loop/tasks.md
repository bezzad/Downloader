## 1. Tests first (must fail on the current code)

- [ ] 1.1 M2: `ThrottledStream` with 64 KB buffer and 16 KB / 1 KB reads → real speed near the limit.
- [ ] 1.2 M14: a chunk read loop does not make a cancel source per read (unit test on the timeout source).

## 2. Fix

- [ ] 2.1 M2: throttle after the read with the real count.
- [ ] 2.2 M14: default buffer 64 KB; one cancel source per chunk; `IsEnabled` checks.

## 3. Finish

- [ ] 3.1 README defaults and CHANGELOG.
- [ ] 3.2 Build with 0 warnings; all tests green.
