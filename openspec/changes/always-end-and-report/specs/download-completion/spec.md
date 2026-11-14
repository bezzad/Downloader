## ADDED Requirements

### Requirement: A storage error fails the download at once
A write error of the storage SHALL NOT be retried. The download SHALL end as `Failed` with a
`StorageException`, and a waiting `CancelTaskAsync()` SHALL end.

#### Scenario: Disk full in the middle of a download
- **WHEN** the storage throws an `IOException` after some bytes were written
- **THEN** the download ends as `Failed`, `DownloadFileCompleted` is raised once with a `StorageException`, and the awaited call ends

#### Scenario: Memory buffer full when the writer dies
- **WHEN** `MaximumMemoryBufferBytes` is set, the buffer is full and the storage throws
- **THEN** the waiting chunks wake up and the download ends as `Failed`

### Requirement: The completion event is raised exactly once
For every download that starts, `DownloadFileCompleted` SHALL be raised exactly once, and the status
SHALL match what really happened to the file.

#### Scenario: Close or flush fails at the end
- **WHEN** the last flush or close of the storage throws
- **THEN** the status is `Failed`, the event is raised once with the error, and the storage is closed

#### Scenario: The final rename fails
- **WHEN** the move of `name.download` to the final name throws
- **THEN** the status is `Failed` and the event is raised once with the error

### Requirement: Handler errors do not fail the download
An exception in a `ChunkDownloadProgressChanged` or `DownloadStarted` handler SHALL be logged and SHALL
NOT change the download result.

#### Scenario: A started handler throws
- **WHEN** a `DownloadStarted` handler throws
- **THEN** the download still completes
