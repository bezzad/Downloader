## ADDED Requirements

### Requirement: Stable releases use a stable SDK
A stable package SHALL be built with a stable SDK and SHALL NOT ship a preview target framework.

#### Scenario: Release build
- **WHEN** the release workflow builds the package
- **THEN** it uses a stable SDK and the package has no preview target

### Requirement: A cloned configuration is independent
`DownloadConfiguration.Clone()` SHALL NOT copy event listeners, and one service's live values SHALL NOT
change another service's.

#### Scenario: Two services, one configuration
- **WHEN** two services use clones of one configuration
- **THEN** a change of `ActiveChunks` in one does not show in the other

### Requirement: Docs match the code
`src/CLAUDE.md` SHALL describe the current code, and finished OpenSpec changes SHALL be archived.

#### Scenario: Finished change
- **WHEN** every task of a change is done
- **THEN** the change is synced and archived
