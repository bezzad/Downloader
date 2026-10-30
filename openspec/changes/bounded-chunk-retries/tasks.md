## 1. Decide

- [ ] 1.1 Owner: keep `MaxTryAgainOnFailure = int.MaxValue`, or set a finite default (which value)?

## 2. Tests first (must fail on the current code)

- [ ] 2.1 Unit test: a chunk that fails 50 times in a row with a retryable error keeps a flat call depth (for example assert the stack depth or allocations do not grow; or test the loop directly).
- [ ] 2.2 Unit test: after many retries `Chunk.Timeout` is not above the cap and never negative.
- [ ] 2.3 If 1.1 = finite: integration test that an always-503 server ends with Failed.

## 3. Fix

- [ ] 3.1 Turn the retry recursion into a `while` loop in `ChunkDownloader.Download`.
- [ ] 3.2 Cap the timeout growth.
- [ ] 3.3 Apply the default from 1.1.

## 4. Finish

- [ ] 4.1 README / XML docs for `MaxTryAgainOnFailure` match the result.
- [ ] 4.2 CHANGELOG entry.
- [ ] 4.3 Build with 0 warnings; all tests green.
