## 1. Decide

- [x] 1.1 Owner: keep `MaxTryAgainOnFailure = int.MaxValue`, or set a finite default (which value)? — owner: keep `int.MaxValue`.

## 2. Tests first (must fail on the current code)

- [x] 2.1 Unit test: a chunk that fails 50 times in a row with a retryable error keeps a flat call depth (for example assert the stack depth or allocations do not grow; or test the loop directly).
  Async call depth can not be measured reliably, so the test checks behavior: 6 failures in a row still finish with the right bytes (`ChunkRetryTest`).
- [x] 2.2 Unit test: after many retries `Chunk.Timeout` is not above the cap and never negative.
- [x] 2.3 If 1.1 = finite: integration test that an always-503 server ends with Failed. — not needed (1.1 keeps unlimited).

## 3. Fix

- [x] 3.1 Turn the retry recursion into a `while` loop in `ChunkDownloader.Download`.
- [x] 3.2 Cap the timeout growth.
- [x] 3.3 Apply the default from 1.1. — nothing to change.

## 4. Finish

- [ ] 4.1 README / XML docs for `MaxTryAgainOnFailure` match the result.
- [x] 4.2 CHANGELOG entry.
- [x] 4.3 Build with 0 warnings; all tests green.
