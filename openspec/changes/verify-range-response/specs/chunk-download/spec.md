## ADDED Requirements

### Requirement: A ranged answer must match the asked range
When a chunk request carries a `Range` header, the downloader SHALL write the body only if the answer
is `206` with a `Content-Range` that starts at the asked byte, or `200` when the asked start is 0.

#### Scenario: Server ignores Range
- **WHEN** a chunk asks for `bytes=1000-1999` and the server answers `200` with the whole file
- **THEN** no bytes are written for that chunk and the download does not report Completed with a broken file

#### Scenario: Normal partial answer
- **WHEN** the server answers `206` with `Content-Range: bytes 1000-1999/5000`
- **THEN** the chunk is downloaded as today
