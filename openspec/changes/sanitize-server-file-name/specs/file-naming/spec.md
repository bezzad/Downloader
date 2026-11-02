## ADDED Requirements

### Requirement: The server file name never leaves the chosen folder
When the caller gives a folder and the file name comes from the server (`Content-Disposition` or the
URL), the downloader SHALL save the file inside that folder, using only the last part of the name.

#### Scenario: Path traversal in Content-Disposition
- **WHEN** the server sends `Content-Disposition: attachment; filename="../../evil.txt"`
- **THEN** the file is saved as `evil.txt` inside the chosen folder

#### Scenario: Rooted path in Content-Disposition
- **WHEN** the server sends `filename="/etc/evil"` or `filename="C:\evil.txt"`
- **THEN** the file is saved inside the chosen folder, not at the server path

### Requirement: Content-Disposition is parsed by the standard
The downloader SHALL read `filename*=` (RFC 5987, UTF-8) before `filename=`, and SHALL match both
parameter names without caring about case.

#### Scenario: Encoded non-ASCII name
- **WHEN** the server sends `filename*=UTF-8''%E2%82%AC.txt`
- **THEN** the file name is `€.txt`

#### Scenario: Upper-case parameter name
- **WHEN** the server sends `FileName="a.zip"`
- **THEN** the file name is `a.zip`
