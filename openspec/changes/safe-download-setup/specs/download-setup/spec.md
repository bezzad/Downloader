## ADDED Requirements

### Requirement: A setup error leaves the service usable
If building the HTTP client or the requests throws, the service SHALL NOT be left in `Created`, and
`Dispose()`, `Clear()` and a new download on the same service SHALL NOT wait for ever.

#### Scenario: Bad request header
- **WHEN** `RequestConfiguration.Headers` holds `Content-Type` and a download is started
- **THEN** the call throws, then `Dispose()` ends at once and a new download call ends

### Requirement: Only absolute http and https URLs are accepted
`Request` SHALL throw `ArgumentException` for a URL that is not an absolute `http` or `https` URL.

#### Scenario: URL without a scheme
- **WHEN** a download is started with `example.com/file.zip`
- **THEN** an `ArgumentException` is thrown and no request is sent

### Requirement: No silent cancel of a running download
`DownloadFileTaskAsync` SHALL throw `InvalidOperationException` when the service already runs a download.

#### Scenario: Second call while busy
- **WHEN** a second download call is made on a busy service
- **THEN** it throws and the first download goes on
