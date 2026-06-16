---
name: code-navigator
description: >-
  Token-efficient map of the Downloader (bezzad/Downloader) .NET library plus its enforced C#
  code-style rules. Use this BEFORE grepping or reading broadly when working in this repo: locating
  the file that owns a behavior (downloads, chunks, redirects, resume, throttling, events, HTTP),
  or writing/editing C# so it matches house style. Read the named file directly instead of scanning.
---

# Downloader — code navigator & style guide

Goal: spend the fewest tokens to land in the right file and produce code that matches the repo.
For deep architecture, read `src/CLAUDE.md` (don't re-derive it). This skill is the fast index +
the non-obvious style rules `.editorconfig` enforces.

## 1. Jump straight to the owning file

Don't grep the tree first — map the concept, open the file, read only the relevant region.

| I'm touching… | Open |
|---|---|
| Download lifecycle, events, pause/resume/cancel, progress aggregation | `Downloader/AbstractDownloadService.cs` |
| `StartDownload`, parallel/serial dispatch, auto-resume, Stopped-vs-Failed | `Downloader/DownloadService.cs` |
| Settings / defaults / clone / `INotifyPropertyChanged` | `Downloader/DownloadConfiguration.cs` |
| HTTP headers, request/proxy/auth/cookies/redirect/user-agent | `Downloader/RequestConfiguration.cs`, `Downloader/Request.cs` |
| Range probe, file-size/filename extraction, **redirects** | `Downloader/SocketClient.cs` |
| Retry classification (`IsMomentumError`), redirect status, cert callback | `Downloader/Extensions/ExceptionHelper.cs` |
| One chunk: retry/backoff, pause, ArrayPool reads, throttle wiring | `Downloader/ChunkDownloader.cs` |
| Split file into chunks | `Downloader/ChunkHub.cs` |
| Live state, status transitions, `TrySetCompleteState`, storage choice | `Downloader/DownloadPackage.cs` |
| Thread-safe writes + background watcher | `Downloader/ConcurrentStream.cs`, `Downloader/ConcurrentPacketBuffer.cs` |
| Bandwidth limit / speed | `Downloader/ThrottledStream.cs`, `Downloader/Bandwidth.cs` |
| Fluent API | `Downloader/DownloadBuilder.cs` |
| URL normalization before `new Uri()` | `Downloader/Extensions/UrlHelper.cs` |
| Test HTTP endpoints (failure/timeout/redirect/truncate/cookie-challenge/useragent) | `Downloader.DummyHttpServer/Controllers/DummyFileController.cs` |
| Test URL builders | `Downloader.DummyHttpServer/DummyFileHelper.cs` |

Cross-cutting concepts and their home: **cancellation→Stopped** rule and retry loop →
`DownloadService.cs`; **redirect/cookie-challenge** → `SocketClient.FetchResponseHeaders`;
**unknown Content-Length progress** → `AbstractDownloadService.RaiseProgressChangedEvents`.

## 2. Cheap recon, in order

1. This skill + `src/CLAUDE.md` first (already summarize structure, hazards, issue history).
2. Need a symbol? `grep -rn "MethodName" src/Downloader` scoped to the library, not the whole repo.
3. Read **targeted line ranges**, not whole files, once the table above points you at one.
4. Tests: there is almost always an existing test mirroring what you're changing — find it by name
   (`grep -rn "Redirect\|Resume\|Truncate" src/Downloader.Test`) and copy its arrange/act/assert shape.
5. Don't re-read a file you just edited to "verify" — Edit/Write fail loudly on mismatch.

## 3. House C# style (enforced by `src/.editorconfig`)

Match these exactly; they differ from common defaults:

- **No `var`.** Always use explicit types — `HttpResponseMessage response = ...`, not `var`.
  (`csharp_style_var_* = false`.)
- **File-scoped namespaces:** `namespace Downloader;`. Usings **outside** the namespace,
  System groups **not** sorted first, groups not separated.
- **4-space indent, CRLF, no final newline.**
- **Allman braces** for types/methods/properties/accessors/control blocks; **always brace** even
  one-line `if`. Object/collection initializers stay K&R: `SocketsHttpHandler handler = new() {`.
- Expression-bodied **properties/accessors/indexers/lambdas = yes**; **methods/constructors = no**.
- Prefer **pattern matching & switch expressions**, **null-propagation/coalescing**,
  **target-typed `new()`**, collection expressions `[]`, range/index operators, `is null` checks.
- `readonly` fields where possible; predefined types (`string`/`int`) over BCL names.
- Modifier order: `public private protected internal static ... readonly ... async`.
- Discard unused: `_ = ...`. Static local functions where applicable.
- Primary constructors are used (e.g. `class ChunkDownloader(...)`, controllers) — keep that idiom.

## 4. AOT & correctness rules that bite (don't regress)

- **Reflection-free / AOT-safe.** Classify exceptions by **type/status only — never
  `Exception.Source`** (empty under AOT, issue #226). System.Text.Json source-gen only.
- **Don't trust a clean EOF** — verify `Chunk.Position == Chunk.Length`; a short stream is a retry,
  not success (issue #231).
- **Cancellation → `Stopped`** even when the thrown exception isn't a cancel type; check
  `IsCancelled` first (issue #225).
- **Unknown `Content-Length`:** never report 100%/percentage until the size is finalized (issue #230).
- **Redirects:** clear `ResponseHeaders` before re-probing a redirect target; same-URL `307`
  challenges resolve only with a `CookieContainer`. No HTTP client defeats a JS challenge or an
  expired signed link — inspect the live response (`curl -I`, `-r 0-0`) before assuming a code bug.

## 5. Verifying changes

- Tests target **net10.0**: `dotnet test src/Downloader.Test/ --filter "FullyQualifiedName~<Name>"`.
- The DummyHttpServer auto-starts in-process (dynamic port) on first `DummyFileHelper` reference —
  no manual server start needed.
- Add new server behavior as a `DummyFileController` endpoint + a `DummyFileHelper.Get*Url` builder,
  then an integration test mirroring an existing one.
