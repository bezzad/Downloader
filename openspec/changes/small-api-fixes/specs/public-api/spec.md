## ADDED Requirements

### Requirement: Builder works with a file name alone
`DownloadBuilder` with a URL and only `WithFileName` SHALL download to that file name.

#### Scenario: No directory given
- **WHEN** `WithUrl(url).WithFileName("a.zip").Build().StartAsync()` runs
- **THEN** the file `a.zip` is downloaded and no `ArgumentNullException` is thrown

### Requirement: Stop is not delayed by the speed limit
Cancelling a download SHALL also cancel any wait made to keep the speed limit.

#### Scenario: Low speed limit
- **WHEN** a download runs with a low `MaximumBytesPerSecond` and the caller cancels
- **THEN** the download stops without waiting for the throttle delay
