## 1. Tests first (must fail on the current code)

- [ ] 1.1 Test with a custom `HttpMessageHandler` that returns 503 responses whose content tracks `Dispose`: after `SendRequestAsync` throws, the response was disposed.
- [ ] 1.2 Test: after a chunk request with different headers, the probe headers (`Content-Range` of the probe) are unchanged.

## 2. Fix

- [ ] 2.1 Wrap `EnsureSuccessStatusCode()`: on failure dispose the response, then rethrow.
- [ ] 2.2 Split header caching so only probe calls write `ResponseHeaders`.

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
