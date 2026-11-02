## 1. Tests first

- [x] 1.1 Test: `DownloadFileExtension` setter gives the same results as today for `"download"`, `".Download"`, `" .part "` and `"a b"`.

## 2. Change

- [x] 2.1 Add `[Obsolete]` to `ImpersonationLevel`, `Pipelined`, `UseDefaultCredentials`; stop setting them in the constructor (or keep, with a pragma) so the build stays at 0 warnings.
- [x] 2.2 Remove `ExceptionHelper.HasTypeOf` and `HasSource` and any tests of them.
- [x] 2.3 Simplify the `DownloadFileExtension` setter.

## 3. Finish

- [x] 3.1 CHANGELOG entry (deprecations).
- [x] 3.2 Build with 0 warnings; all tests green.
