## ADDED Requirements

### Requirement: Resume only the same file
When resuming, the downloader SHALL start fresh if the server's `ETag` or `Last-Modified` differs from
the saved value, even when the size is the same.

#### Scenario: Same size, new content
- **WHEN** a stopped download is resumed and the server now has another file of the same size with a new `ETag`
- **THEN** the download starts from 0 and the final file is the new content

### Requirement: Counted bytes are written bytes
A chunk's position SHALL only count bytes that were accepted by the storage.

#### Scenario: Storage closed during a stop
- **WHEN** the storage is closed while a chunk still reads
- **THEN** that chunk's position does not grow for the bytes that were not written
