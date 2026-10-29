## ADDED Requirements

### Requirement: Chunk workers do not stay subscribed to the configuration
After a chunk finishes (success, failure or stop), the downloader SHALL remove every handler it added
to the caller's `DownloadConfiguration`.

#### Scenario: Reused configuration
- **WHEN** one `DownloadConfiguration` is used for three downloads in a row
- **THEN** its `PropertyChanged` event has no more subscribers than before the first download
