## ADDED Requirements

### Requirement: Options with no effect are marked obsolete
Every public configuration property that the downloader never reads SHALL be marked `[Obsolete]` with
a message saying it has no effect.

#### Scenario: Setting a no-op option
- **WHEN** a caller sets `RequestConfiguration.Pipelined`
- **THEN** the compiler shows an obsolete warning that says the property has no effect
