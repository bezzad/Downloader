## ADDED Requirements

### Requirement: Relative redirect targets work
The downloader SHALL resolve a relative `Location` header against the address of the request that got
the redirect.

#### Scenario: Relative Location
- **WHEN** the server answers `307` with `Location: /files/a.zip`
- **THEN** the download continues from `/files/a.zip` on the same host
