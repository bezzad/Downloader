## Why

**Priority: Medium (memory leak, slower progress over time).**

The `ChunkDownloader` constructor does `_configuration.PropertyChanged += ConfigurationPropertyChanged`
(`src/Downloader/ChunkDownloader.cs`, line 37) and **never removes it**. A new `ChunkDownloader` is made
for every chunk of every download (`DownloadService.DownloadChunk`, line ~558).

Apps that reuse one `DownloadConfiguration` for many downloads (normal for a download manager) keep
every old `ChunkDownloader` alive, with its `Chunk`, storage and client references. Also,
`RaiseProgressChangedEvents` sets `Options.ActiveChunks` on progress (`AbstractDownloadService.cs`,
line ~428), which raises `PropertyChanged` and calls **all** old handlers. Memory grows and progress
events get slower the longer the app runs.

## What Changes

- `ChunkDownloader` removes its handler when its work is done: make it `IDisposable` (unsubscribe in
  `Dispose`) and create it with `using` in `DownloadService.DownloadChunk`.
- No public API change (`ChunkDownloader` is internal).

## Capabilities

### New Capabilities
- `resource-lifetime`: objects made for one download are released after it.

## Impact

- `src/Downloader/ChunkDownloader.cs`, `src/Downloader/DownloadService.cs`.
- Tests: reuse one config for several downloads and check the subscriber count of
  `DownloadConfiguration.PropertyChanged` (read with reflection in the test) does not grow.
