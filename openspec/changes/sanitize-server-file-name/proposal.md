## Why

**Priority: High (security).**

The file name from the server is used as is.

- `SocketClient.GetUrlDispositionFilenameAsync` (`src/Downloader/SocketClient.cs`, around line 577)
  reads the name from the `Content-Disposition` header and does not clean it.
- `AbstractDownloadService.DownloadFileTaskAsync(string[] urls, DirectoryInfo folder)`
  (`src/Downloader/AbstractDownloadService.cs`, around line 255) does
  `Path.Combine(folder.FullName, name)`.

So a server can send `filename="../../.bashrc"`, `filename="/etc/cron.d/x"` or `filename="C:\x.exe"`
and the file is written **outside** the folder the caller chose. `Path.Combine` even returns the
server path alone when it is rooted.

Smaller bugs in the same code:

- The match is case-insensitive but `.Replace("filename=", "")` is case-sensitive, so
  `FileName="a.zip"` gives the name `FileName=a.zip`.
- `filename*=UTF-8''...` (RFC 6266 / RFC 5987, used for non-ASCII names) is ignored.
- Characters that Windows does not allow (`: * ? " < > |`) make the download fail on Windows.
- `RemoteFileInfo.FileName` returns the same raw value to apps, which then may combine it with a path too.

## What Changes

- One small helper cleans a server file name: keep only the last path part (treat both `/` and `\`
  as separators), replace control and invalid characters with `_`, trim dots and spaces at the end,
  and reject an empty name, `.` and `..`.
- Parse `filename*=` first (UTF-8, percent-decoded), then `filename=`, both case-insensitive.
- Use the helper in `SocketClient.SetRequestFileNameAsync`, so the download, `RemoteFileResolver` and
  `GetFileInfoAsync` all get a safe name. When nothing is left, fall back to the URL name, then a GUID
  (the current fallback order).
- Extra check in `DownloadFileTaskAsync(urls, folder)`: the full path must stay inside `folder`.

## Capabilities

### New Capabilities
- `file-naming`: how the downloader picks a safe local file name from the server answer.

## Impact

- `src/Downloader/SocketClient.cs`, `src/Downloader/AbstractDownloadService.cs`,
  `src/Downloader/Extensions/FileHelper.cs` (or a new small helper next to it).
- Tests: new cases in `SocketClientTest` and an integration test with a new dummy-server endpoint.
- A caller that relied on a path inside the header (very unlikely and unsafe) now gets only the name.
