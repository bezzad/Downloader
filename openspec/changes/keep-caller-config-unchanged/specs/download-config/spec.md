## ADDED Requirements

### Requirement: The caller's configuration is not changed by a download
The downloader SHALL NOT change `ChunkCount`, `ParallelCount`, `ParallelDownload`, `RangeLow`,
`RangeHigh` or `CustomHttpClientFactory` on the caller's `DownloadConfiguration`.

#### Scenario: Small file then large file
- **WHEN** a config with `ChunkCount = 8` downloads a small file and then a large file
- **THEN** the large file is downloaded with 8 chunks

#### Scenario: Server without range support
- **WHEN** a server does not support ranges
- **THEN** that download uses one connection and the config still has its original values
