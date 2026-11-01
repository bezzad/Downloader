## 1. Tests first (must fail on the current code)

- [x] 1.1 Test with a custom `HttpMessageHandler` that returns 503 responses whose content tracks `Dispose`: after `SendRequestAsync` throws, the response was disposed.
- [x] 1.2 Test: after a chunk request with different headers, the probe headers (`Content-Range` of the probe) are unchanged.

## 2. Fix

- [x] 2.1 Wrap `EnsureSuccessStatusCode()`: on failure dispose the response, then rethrow.
- [x] 2.2 Split header caching so only probe calls write `ResponseHeaders`.

## 3. Finish

- [x] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
