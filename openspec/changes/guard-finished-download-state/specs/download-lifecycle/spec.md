## ADDED Requirements

### Requirement: A finished download keeps its status
Once a download is Completed or Failed, `Pause()`, `Resume()` and `CancelAsync()` SHALL NOT change its
status.

#### Scenario: Stop after completion
- **WHEN** `CancelAsync()` is called on a Completed download
- **THEN** `Status` stays `Completed` and `IsBusy` is false

### Requirement: Clear stops a download that is still starting
`Clear()` and `Dispose()` SHALL cancel a download whose status is `Created`, and SHALL NOT wait for
the whole file.

#### Scenario: Dispose during the first probe
- **WHEN** the service is disposed while the header probe is still running
- **THEN** dispose returns soon and the download ends as Stopped
