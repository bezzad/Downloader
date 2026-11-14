## ADDED Requirements

### Requirement: A timeout is not a user cancel
A timeout SHALL end the download as `Failed` with a `TimeoutException`, and the file info probe SHALL
retry it. `Stopped` SHALL mean the caller asked for the cancel.

#### Scenario: Slow headers
- **WHEN** the server sends the headers after `HttpClientTimeout`
- **THEN** the status is `Failed` and `Cancelled` is false

### Requirement: Stop from a handler does not block
`IDownload.Stop()` SHALL return without waiting for the download to end; `StopAsync()` SHALL wait.

#### Scenario: Stop inside DownloadStarted
- **WHEN** a `DownloadStarted` handler calls `Stop()`
- **THEN** the download ends as `Stopped`

### Requirement: Recovered errors do not count against the limit
A chunk that gets bytes again after an error SHALL reset its failure count and read timeout.

#### Scenario: Many recovered errors
- **WHEN** a chunk with `MaxTryAgainOnFailure = 2` has 5 errors, each followed by good bytes
- **THEN** the download completes

### Requirement: A no-range download retries from zero
When the server has no range support, a retryable error SHALL restart the single chunk from byte 0,
inside the retry limit.

#### Scenario: Network error without range support
- **WHEN** a no-range download fails once mid-stream
- **THEN** it starts again from 0 and completes

### Requirement: Pause cannot change a finished status
The finished check and the status write SHALL happen under one lock.

#### Scenario: Pause at the end
- **WHEN** `Pause()` runs at the moment the download completes
- **THEN** the status stays `Completed`

### Requirement: Server errors are retried
500 and 502 SHALL be retried, and an `OperationCanceledException` the user did not ask for SHALL be
treated as a timeout.

#### Scenario: One 502
- **WHEN** the server answers 502 once
- **THEN** the chunk retries and the download completes
