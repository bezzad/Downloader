## Why

**Priority: Low.** Small bugs, each a few lines.

1. **File name without folder crashes.** `DownloadBuilder.New().WithUrl(url).WithFileName("a.zip")`
   then `StartAsync()` calls `Path.Combine(Folder, Filename)` with `Folder == null`
   (`src/Downloader/Download.cs`, `StartAsync`) → `ArgumentNullException`.
2. **Wrong exception argument.** `DownloadBuilder.Build()` does
   `new ArgumentNullException($"{nameof(_url)} has not been declared.")`; the text goes into
   `ParamName`, and the message is generic.
3. **`FileExistException` has no message.** It shows "I/O error occurred." The path is only in `Name`.
4. **Stop waits for the speed limit.** `ThrottledStream.Throttle` uses `Task.Delay(time)` without the
   cancellation token, so a stop (or a pause) waits for the throttle delay first. With a low limit this
   can be seconds.
5. **System proxy is never used.** `SocketClient` sets `UseProxy = config.Proxy != null`
   (`SocketClient.cs`, line 55). When `Proxy` is null, `HTTP_PROXY` / `HTTPS_PROXY` and the OS proxy are
   ignored, unlike a normal `HttpClient`. Users behind a company proxy must set it by hand. Changing this
   is a behavior change, so it needs an owner decision.

## What Changes

1. Use `Filename` as the path when `Folder` is empty.
2. `throw new ArgumentNullException(nameof(_url) ... )` → `throw new InvalidOperationException("The URL is not set. Call WithUrl() first.")` (or `ArgumentNullException("url", "...")`).
3. Give `FileExistException` a message that names the file.
4. Pass the read's cancellation token to the throttle delay.
5. Only if the owner agrees: use the default system proxy when `Proxy` is null
   (`UseProxy = true`, `Proxy = config.Proxy` when set).

## Capabilities

### New Capabilities
- `public-api`: small correctness rules of the public API.

## Impact

- `Download.cs`, `DownloadBuilder.cs`, `Exceptions/FileExistException.cs`, `ThrottledStream.cs`,
  `SocketClient.cs`. Each item is independent and can be its own commit.
