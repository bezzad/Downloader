## ADDED Requirements

### Requirement: Retries do not grow memory or overflow the timeout
A chunk SHALL retry in a loop, and its per-read timeout SHALL stay within a fixed maximum, however many
times it retries.

#### Scenario: Long outage
- **WHEN** a chunk fails with a retryable error many times in a row
- **THEN** memory does not grow per retry and the read timeout stays at or below the cap
