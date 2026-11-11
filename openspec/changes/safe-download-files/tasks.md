## 1. Tests first (must fail on the current code)

- [x] 1.1 H2: stop, delete the `.download` file, resume the package → correct file (now zeros at the start).
- [x] 1.2 H2: stop, the server changes the `ETag` and content, resume the package → only new bytes (now mixed).
- [x] 1.3 H3: two services to the same path at the same time → with `Rename` both complete under two names; with `Delete` the second is `Failed` and the first file is right (now no file).
- [ ] 1.4 M10: target exists, the new download fails → old file still there.
- [ ] 1.5 M11: server names `CON`, `NUL`, a 300-byte name → safe names; a server name does not replace an existing file.
- [ ] 1.6 L7: a `.download` file with a tail above 16 MB → not read, fresh download.
- [ ] 1.7 L9: resume uses the current `MaxTryAgainOnFailure`/`BlockTimeout`; a layout with a gap starts fresh.

## 2. Fix

- [x] 2.1 H2: compare the probe with the package before it is saved; check the temp file; clear the chunks when needed.
- [x] 2.2 H3: a temp file is claimed by one download in the process (`TempFileClaims`); a second one fails at once with an `IOException`, and `Rename` skips a claimed name. (`FileShare.Read` was not used: on Linux .NET takes a shared lock for it, so it does not stop a second writer.) If two `Rename` downloads pick the same free name at the same moment, the one that loses the claim asks for the next free name (test `RenamePolicyWorksWhenBothStartAtTheSameMoment`).
- [ ] 2.3 M10: no delete at start; `File.Move(..., overwrite: true)` at the end.
- [ ] 2.4 M11: safe server names; `Rename` for server names unless the caller set a policy.
- [ ] 2.5 L7: cap the metadata tail at 16 MB.
- [ ] 2.6 L9: retry settings from the configuration; layout check.

## 3. Finish

- [ ] 3.1 README (resume section) and CHANGELOG.
- [ ] 3.2 Build with 0 warnings; all tests green.
