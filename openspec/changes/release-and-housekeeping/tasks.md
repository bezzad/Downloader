## 1. Tests first (where a test can show it)

- [ ] 1.1 L10: clone a configuration with a listener → the clone has no listener; two services do not share `ActiveChunks`.
- [ ] 1.2 L12: the chunk size check in `AbstractDownloadService` (~line 460) has a test that shows the right behavior.

## 2. Fix

- [ ] 2.1 M15: stable SDK in `release.yml`/`global.json`; drop `net11.0` from the package.
- [ ] 2.2 L10: `Clone()` and `ActiveChunks`.
- [ ] 2.3 L12: small cleanups.
- [ ] 2.4 L13: package validation, CI build flag, AOT props, release tests on three OSes.
- [ ] 2.5 L14: update `src/CLAUDE.md`; sync and archive the 16 changes of 2026-10-29 (own commit).

## 3. Finish

- [ ] 3.1 CHANGELOG entry.
- [ ] 3.2 Build with 0 warnings; all tests green.
