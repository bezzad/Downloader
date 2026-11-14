## ADDED Requirements

### Requirement: Flush waits for every queued block
`FlushAsync` SHALL return only after every block added before the call is written.

#### Scenario: Block added while the writer is busy
- **WHEN** blocks are added in bursts and flush is called after each burst
- **THEN** every flush returns only after all added blocks are written

### Requirement: The memory limit counts real memory
`MaximumMemoryBufferBytes` SHALL limit the total size of the buffers held in the queue.

#### Scenario: Big buffers, small reads
- **WHEN** 1 MB buffers with 16 bytes each are queued with a 64 KB limit and a blocked disk
- **THEN** adding stops after about 64 KB of buffers

### Requirement: Memory use is bounded by default
The write queue SHALL have a default memory limit, and a memory download larger than a `MemoryStream`
can hold SHALL fail at the start.

#### Scenario: Huge memory download
- **WHEN** a 3 GB file is downloaded to memory
- **THEN** the download fails before the first byte

### Requirement: Dispose does not drop blocks
`ConcurrentStream.Dispose()` SHALL write the queued blocks before it closes the stream.

#### Scenario: Dispose with blocks in the queue
- **WHEN** `Dispose()` is called while blocks wait
- **THEN** they are written to the stream
