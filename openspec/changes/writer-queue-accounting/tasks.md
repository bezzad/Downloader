## 1. Tests first (must fail on the current code)

- [ ] 1.1 M1: burst add + `WaitToComplete` many times → never returns before all are written.
- [ ] 1.2 M3: 1 MB rented buffers with 16-byte packets and a 64 KB limit → adding stops early.
- [ ] 1.3 L8: memory download larger than `int.MaxValue` → fails at the start.
- [ ] 1.4 L11: sync `Dispose()` with queued blocks → blocks are on the stream.

## 2. Fix

- [ ] 2.1 M1: pending counter; flush waits for zero; remove the 20 ms delay.
- [ ] 2.2 M3: count rented array sizes.
- [ ] 2.3 L8: default limit; size check for memory downloads.
- [ ] 2.4 L11: drain in `Dispose()` (or mark it obsolete).

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
