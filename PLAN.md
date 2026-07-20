# PLAN — Living Plan

This is the **living plan** for the repo. It is the single source of truth for what is
being worked on, kept current at all times and committed on the `develop` branch so any
machine can pull the latest state and continue. Always update this file together with the
code change it describes.

**Status markers:** `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/failed

---

- **Last updated:** 2026-07-19
- **Branch:** develop
- **Now working on:** [~] .NET 11 (preview) support across all projects + CI.

---

## Active

- [~] **.NET 11 support.** Added `net11.0` (SDK 11.0.100-preview.5) to all four projects:
  library (`net8.0;net9.0;net10.0;net11.0`), DummyHttpServer (`net8.0;net10.0;net11.0`),
  Test + Sample (`net10.0;net11.0`). CI workflows (ubuntu/macos/windows/release) install the
  11 preview SDK via a separate `actions/setup-dotnet` step with `dotnet-quality: preview`
  (separate step so GA 8/9/10 don't get preview quality). Running full test suite on both
  test TFMs before pushing.
  - [x] **CI fix**: Windows leg crashed with a fatal access violation (0xC0000005 in
    `Mono.Cecil.Pdb.NativePdbWriter`) while Fody wove the `net11.0` test assembly. The test
    project forced `DebugType=full` (native Windows PDBs); switched it to `portable` so Fody's
    Mono.Cecil writes managed cross-platform PDBs and avoids the crashing native COM writer. (6500987)
    macOS leg failure was an unrelated flaky 503 in `Issue231Test` (transient dummy-server transport
    error), not a regression.

## Todo

_(queued tasks — marked `[ ]`)_

_(no queued tasks)_

## Done

- [x] **Fix stale/flaky `RemoteFileResolverTest.GetFileInfoOnUnreachableHostFallsBackToUrlNameTest`**
  — found while re-verifying the issue #239 fix. `RemoteFileResolver.GetFileInfoAsync` had
  `catch (OperationCanceledException) { throw; }` unconditionally before its best-effort fallback
  catch. `TaskCanceledException` (a subtype of `OperationCanceledException`) is also what
  `HttpClient`/`SocketsHttpHandler` throws internally on a `ConnectTimeout` — even though the
  caller's own `cancelToken` was never signaled — so a slow/black-holed unreachable host
  propagated that timeout as an exception instead of falling back to the URL-derived file name,
  breaking the method's documented "resilient: on a network/server error it falls back"
  contract. Fixed by gating the rethrow on `cancelToken.IsCancellationRequested` (same
  cancellation-flag-not-just-type rule as issue #225). Full suite 529/529 passing after the fix.

- [x] **Fix issue #239** (rare `IOException: file in use` on the `.download` temp file) —
  `DownloadService.ProvideDownloadOnFile`, `DownloadPackage.TrySetCompleteState`, and
  `FileHelper.CheckFileExistPolicy` all called `File.Delete` directly, which threw an unhandled
  `IOException` when the file was momentarily locked by another process (e.g. an antivirus
  real-time scan of a freshly-written `.exe`) instead of the OS releasing the handle in time.
  Added `FileHelper.DeleteFile` — retries on `IOException` before giving up — and used it at
  every `File.Delete` call site touching downloader-owned files. Tests (`FileHelperTest`)
  verify the retry succeeds once the lock clears and still throws when it never does; gated to
  Windows since POSIX allows unlinking open files (the sharing-violation premise doesn't apply
  on Linux/macOS). (86ae834) Follow-ups: widened the retry budget from ~300ms (too short for
  AV scans of large files — the report's file was a 771MB exe) to ~3.1s exponential backoff
  (6 attempts, 100→1600ms) (db1bc23); wrapped the final give-up `IOException` with a message
  explaining the lock is held by an external process (AV/other program) and how to resolve it,
  original exception preserved as `InnerException` (8d9d1d5). Framing agreed with Behzad: the
  lock itself is an OS/environment condition, not a library defect — the library's job is to
  tolerate transient locks and clearly attribute permanent ones. Explanatory comment posted:
  https://github.com/bezzad/Downloader/issues/239#issuecomment-5017226153

- [x] **Test-coverage increase** — 13 deterministic unit tests (`DownloadBuilderTest`,
  `RequestTest`, `SocketClientTest`, `RemoteFileResolverTest`); line coverage 88.73% → 90.21%,
  no production code touched, no bugs found. (035716f)

- [x] **Released v5.9.4** (tag `v5.9.4`, commit `7dc1d79`) — resume-after-failure bug fixes
  (Downloader.Desktop report: failed resume restarts from 0%). Four root causes fixed:
  1. `DownloadService.ShouldFallbackToSingleConnection` — gated the single-connection fallback
     with `Package.ReceivedBytesSize == 0` so a transient failure never wipes resumable progress.
  2. `Chunk.IsDownloadCompleted` — off-by-one (`Start+Position >= End` → `Position >= Length`);
     a chunk one byte short was skipped as complete while the incomplete-guard kept failing it,
     so downloads failed near 100% and could never resume.
  3. `ChunkDownloader.SetRequestRange` — `startOffset < End` → `<= End`; a chunk resuming with
     exactly one byte left did a full GET from offset 0 and corrupted its last byte.
  4. `ChunkHub.SetFileChunks` — rebuild chunks when `!IsSupportDownloadInRange` (stale multi-chunk
     layout against a no-range server corrupted the file).
  Tests: `IntegrationTests/ResumeAfterFailureTest.cs` (+ `UnitTests/ChunkTest.cs` boundary tests);
  each fix verified to fail pre-fix and pass post-fix. Full suite 515/515; all 3 OS CI legs green
  on develop and master before tagging. Followed the "green on develop → merge master → green →
  tag" flow. Note: earlier attempts bumped 5.9.2 and 5.9.3 (CI-failed, nothing published) —
  dangling `v5.9.2`/`v5.9.3` tags remain on the remote.

- [x] **Released v5.9.1** (issue #236 gzip-truncation fix) to **nuget.org**
  (https://www.nuget.org/packages/Downloader/5.9.1), **GitHub Packages**, and a **GitHub Release**
  with curated notes. Ran `scripts/release.sh 5.9.1` — the git mechanics (bump/merge/tag), XML-escaped
  notes, tag-run SHA matching, test gate, and "create release before pushing" ordering all worked
  live. **One live failure surfaced a real bug the audit missed:** with `DebugType=embedded` the
  `.snupkg` symbol package contains no `.pdb`, so nuget.org's symbol server rejects it with HTTP 400,
  which aborted the publish before the GitHub Packages push. Fixed by `IncludeSymbols=false` +
  `--no-symbols` on both pushes + dropping the `.snupkg` release asset (commit `9400dd6`, on develop
  and master). Completed the GitHub Packages publish via `workflow_dispatch` on master
  (nuget.org `--skip-duplicate`) and set the release notes. The main 5.9.1 package had already
  reached nuget.org before the failure, so no version was burned.
- [x] Hardened `scripts/release.sh` + `.github/workflows/release.yml` (audit fixes): `--no-symbols`
  on both feed pushes, XML-escaped release notes, GitHub Release created before the feed pushes,
  commit-SHA run matching in release.sh, and a CI test gate before publish.

- [x] Fix issue #236 (file truncated when a caller's HttpClient auto-decompresses gzip content):
  `SocketClient.GetFileSizeAsync`/`IsSupportDownloadInRange` now treat a non-identity
  `Content-Encoding` on the probe response as unknown size + no range support, routing such
  downloads through the existing single-connection/unknown-size path (issue #230) instead of
  chunking against the compressed byte count and truncating. Added `Issue236Test` +
  `DummyFileController.GetGzipCompressedFile` gzip test endpoint. Verified: new test passes.
  Branch `fix/issue-236-gzip-content-truncation` pushed, explanatory comment posted on the issue
  (https://github.com/bezzad/Downloader/issues/236#issuecomment-4929028719), merged into develop
  (94da811).
- [x] Added `scripts/release.sh` + `.github/workflows/release.yml` for the NuGet release process.
  Publishing (nuget.org + GitHub Packages) happens in CI via the tag-push-triggered workflow,
  since `NUGET_API_KEY`/`PACKAGE_TOKEN` live in GitHub Actions secrets, not a local shell —
  release.sh only does the version bump/merge/tag mechanics and then waits on the workflow +
  sets curated release notes. Modeled on `../Downloader.Desktop/scripts/release.sh` +
  `release.yml`. Not run yet — no version has been supplied to release. (fdabdba)

- [x] Set up cross-machine task tracking (PLAN.md, TASKS.md, CLAUDE.md workflow section) — committed on develop (e7e73aa)
- [x] Expose public file-metadata resolver — added `RemoteFileResolver` + `RemoteFileInfo` so consumers can fetch a remote file's name/size (and range support) without starting a download; wraps `SocketClient.SetRequestFileNameAsync`/`GetFileSizeAsync`. Tests in `RemoteFileResolverTest`. (4ac4d39)
- [x] Use the metadata concept internally (no duplication) — added canonical `SocketClient.GetFileInfoAsync` (name+size+range in one probe); `DownloadService.StartDownload` now consumes it instead of separate `GetFileSizeAsync`/`IsSupportDownloadInRange` calls; `RemoteFileResolver` delegates to it; exposed `IDownloadService.GetFileInfoAsync(url)`. 165 regression tests + new tests pass. (9d740df)
- [x] Document `RemoteFileResolver` in README (new "get file name and size without downloading" section + Key Features bullet) and left a self-prompt in the **Downloader.Desktop** repo (its PLAN.md/TASKS.md) to migrate the downloads grid to it — note: blocked there until a package release >5.8.1 ships the API. (71fd5b1)
- [x] Released **v5.9.0**: bumped version + PackageReleaseNotes (e205c1e), merged develop→master (3ed452f), tagged `v5.9.0`, created the GitHub Release with notes + assets, and published the NuGet package to **GitHub Packages** and **nuget.org** (https://www.nuget.org/packages/Downloader/5.9.0).
- [x] README: added the **Downloader Desktop** promo + screenshot at the top (2b25e07) and made the screenshot theme-aware via `<picture>` — `home-dark.png` in dark mode, `home-light.png` in light mode, light fallback (c18c3ae).

## Blocked/Failed

_(none)_

- [x] ~~Publish Downloader **5.9.0 to NuGet.org**~~ — RESOLVED: 5.9.0 is live on nuget.org
  (https://www.nuget.org/packages/Downloader/5.9.0). The earlier "no API key in this session" block
  is stale — going forward the CI `release.yml` publishes to nuget.org + GitHub Packages using the
  `NUGET_API_KEY`/`PACKAGE_TOKEN` Actions secrets, so the next `scripts/release.sh` run covers both
  feeds automatically.
