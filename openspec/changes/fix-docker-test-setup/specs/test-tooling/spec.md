## ADDED Requirements

### Requirement: Docker test commands work
The Docker files in `src/` SHALL use an SDK that can build the test project, and the README commands
SHALL run the test suite.

#### Scenario: Compose
- **WHEN** a contributor runs `docker-compose -p downloader up` in `src/`
- **THEN** the tests build and run
