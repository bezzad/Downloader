## 1. Tests first

- [ ] 1.1 Test: `DownloadFileExtension` setter gives the same results as today for `"download"`, `".Download"`, `" .part "` and `"a b"`.

## 2. Change

- [ ] 2.1 Add `[Obsolete]` to `ImpersonationLevel`, `Pipelined`, `UseDefaultCredentials`; stop setting them in the constructor (or keep, with a pragma) so the build stays at 0 warnings.
- [ ] 2.2 Remove `ExceptionHelper.HasTypeOf` and `HasSource` and any tests of them.
- [ ] 2.3 Simplify the `DownloadFileExtension` setter.

## 3. Finish

- [ ] 3.1 CHANGELOG entry (deprecations).
- [ ] 3.2 Build with 0 warnings; all tests green.
