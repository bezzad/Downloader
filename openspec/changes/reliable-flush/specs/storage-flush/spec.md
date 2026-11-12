## ADDED Requirements

### Requirement: Flush covers what was written before it
`ConcurrentStream.FlushAsync` SHALL end once every block given before the call is written and the base
stream is flushed, even while other blocks keep coming.

#### Scenario: Download keeps writing
- **WHEN** chunks keep writing faster than the disk and `FlushAsync` is called
- **THEN** it ends in a short time, and every block given before it is on the base stream

### Requirement: Flush reaches the disk
For a `FileStream`, the flush SHALL call `Flush(true)`.

#### Scenario: File storage
- **WHEN** `FlushAsync` is called on a file storage
- **THEN** `FileStream.Flush(true)` is called

### Requirement: Flush never reports dropped bytes as written
A flush that cannot finish SHALL fail, not hang and not succeed.

#### Scenario: Stream closed with queued blocks
- **WHEN** the stream is closed while blocks given before the flush are still queued
- **THEN** `FlushAsync` ends with `OperationCanceledException` or `ObjectDisposedException`

#### Scenario: Writer failed
- **WHEN** the writer has failed
- **THEN** `FlushAsync` throws the writer's error
