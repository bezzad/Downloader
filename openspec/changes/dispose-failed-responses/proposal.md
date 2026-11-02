## Why

**Priority: Low-Medium.**

- `SocketClient.SendRequestAsync` (`SocketClient.cs`, line ~603) calls `EnsureSuccessStatusCode()` but
  does not dispose the response when that throws. With `HttpCompletionOption.ResponseHeadersRead` the
  connection stays busy until the GC runs. With retries on 503 / 429 (for ever by default) connections
  pile up; `MaxConnectionsPerServer = 1000` hides it until the server or the OS limit is hit.
- The same method clears and refills the shared `ResponseHeaders` dictionary on **every** request,
  including every chunk request from many threads at once. That dictionary is the cache of the first
  probe. Today nothing reads it after the start, but any future read (for example in a retry path) gets
  the headers of a random chunk, or nothing after an error.

## What Changes

- Dispose the response before the exception leaves `SendRequestAsync`.
- Only the probe path (`FetchResponseHeaders`, `FetchRangeHeadersAgain`) fills `ResponseHeaders`.
  Chunk requests do not touch it (for example a private method with a `keepHeaders` flag, or a
  separate method for chunks).

## Capabilities

### New Capabilities
- `http-client`: how requests and responses are handled.

## Impact

- `src/Downloader/SocketClient.cs`, `src/Downloader/ChunkDownloader.cs` (the call).
- `SendRequestAsync` is public; keep its signature.
