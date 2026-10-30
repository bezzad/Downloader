## ADDED Requirements

### Requirement: Failed responses are released
When a request gets a non-success status, the downloader SHALL dispose that response before it reports
or retries the error.

#### Scenario: 503 answer
- **WHEN** the server answers 503 to a chunk request
- **THEN** the response is disposed and its connection is free again

### Requirement: Chunk requests do not change the probe headers
Only the file-info probe SHALL write the cached response headers.

#### Scenario: Parallel chunk requests
- **WHEN** chunk requests run after the probe
- **THEN** the cached probe headers stay as the probe left them
