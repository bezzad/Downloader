## ADDED Requirements

### Requirement: Resume from a package checks the file
When a download is resumed from a `DownloadPackage`, the downloader SHALL start from zero if the temp
file is missing or too short, or if the server's size, `ETag` or `Last-Modified` changed.

#### Scenario: Temp file deleted
- **WHEN** a stopped download's `.download` file is deleted and the package is resumed
- **THEN** the download starts from zero and the final file is correct

#### Scenario: Server file changed
- **WHEN** the server now sends another `ETag` and the package is resumed
- **THEN** the download starts from zero and the final file holds only the new content

### Requirement: One writer per temp file
Only one download SHALL write to a `.download` file at a time. A second download to the same file SHALL
fail at once with a clear error and SHALL NOT touch the first one's file.

#### Scenario: Same link added twice
- **WHEN** two services download to the same path at the same time
- **THEN** one completes with a correct file and the other fails with an error

### Requirement: The old file is kept until the new one is ready
With `FileExistPolicy.Delete` the old file SHALL be replaced only when the new download completes.

#### Scenario: New download fails
- **WHEN** the target exists and the new download fails
- **THEN** the old file is still there

### Requirement: Server file names are safe
A file name chosen by the server SHALL NOT be a Windows device name, SHALL be at most 255 bytes, and
SHALL NOT replace an existing file unless the caller set a policy.

#### Scenario: Server sends `CON`
- **WHEN** the server names the file `CON`
- **THEN** a safe name is used instead

### Requirement: Resume metadata is bounded and checked
The resume metadata tail SHALL be read only when it is at most 16 MB, its chunks SHALL cover the file
exactly, and retry settings SHALL come from the current configuration.

#### Scenario: Huge stale tail
- **WHEN** the `.download` file has a 1 GB tail
- **THEN** it is not read into memory and the download starts fresh
