## ADDED Requirements

### Requirement: Serial download uses one connection
When `ParallelDownload` is false, the downloader SHALL download one chunk at a time, whatever
`ParallelCount` is.

#### Scenario: Serial with many chunks
- **WHEN** `ParallelDownload = false`, `ChunkCount = 4` and `ParallelCount = 4`
- **THEN** at most one chunk request is open at any time
