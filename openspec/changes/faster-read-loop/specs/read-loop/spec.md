## ADDED Requirements

### Requirement: The throttle counts real bytes
The speed limit SHALL be applied on the bytes really read, not on the buffer size.

#### Scenario: Big buffer, small reads
- **WHEN** the buffer is 64 KB, each read returns 16 KB and the limit is 256 KB/s
- **THEN** the real speed is close to 256 KB/s

### Requirement: The read loop is cheap
A chunk SHALL reuse one cancel source for its read timeouts, and SHALL not build debug log messages
when debug logging is off.

#### Scenario: Many reads
- **WHEN** a chunk makes many reads
- **THEN** no new cancel source is made per read
