## 1. Decide

- [ ] 1.1 Pick the metadata length method: 8-byte length trailer, or cut the file after each write.

## 2. Tests first (must fail on the current code)

- [ ] 2.1 Dummy-server endpoint that serves different content with the same size and a different `ETag` on the second call.
- [ ] 2.2 Test: stop mid-download, switch content, resume with `EnableAutoResumeDownload` → final file equals the new content (now it is mixed).
- [ ] 2.3 Test: write long metadata then shorter metadata into a `.download` file → the resume reads the shorter one and continues (now it restarts from 0).
- [ ] 2.4 Unit test: `ConcurrentStream.WriteAsync` after `Dispose` throws and does not leak the rented buffer.

## 3. Fix

- [ ] 3.1 Read `ETag` / `Last-Modified` in the probe; store them in `PackageInfo`; compare on resume.
- [ ] 3.2 Apply the metadata length method from 1.1, keeping the old format readable.
- [ ] 3.3 Throw from `WriteAsync` when the add fails; dispose the packet.

## 4. Finish

- [ ] 4.1 README "How to automatically resume downloads": list the new check.
- [ ] 4.2 CHANGELOG entry.
- [ ] 4.3 Build with 0 warnings (also AOT: new properties in the source-gen context); all tests green.
