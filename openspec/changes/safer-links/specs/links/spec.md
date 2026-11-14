## ADDED Requirements

### Requirement: No silent HTTPS to HTTP redirect
A redirect from `https` to `http` SHALL be followed only when `AllowInsecureRedirect` is true.

#### Scenario: Downgrade redirect
- **WHEN** an `https` URL redirects to `http` and the option is off
- **THEN** the download fails with a clear error

### Requirement: A changed server file is noticed
Chunk requests SHALL send `If-Range` when an `ETag` or `Last-Modified` is known, and a `Content-Range`
total that differs from the file size SHALL fail the chunk.

#### Scenario: File replaced mid-download
- **WHEN** the server file changes between two chunk requests
- **THEN** the download does not complete with mixed content

### Requirement: An expired redirect target is renewed
On 401, 403, 404 or 410 from a redirected address, the original URL SHALL be asked again once.

#### Scenario: Signed link expires
- **WHEN** a later chunk gets 403 from the redirect target
- **THEN** the original URL is asked again and the chunk goes on

### Requirement: Credentials only when asked
Basic credentials SHALL be sent before a challenge only when `PreAuthenticate` is true.

#### Scenario: No challenge
- **WHEN** `PreAuthenticate` is false and the server does not ask for credentials
- **THEN** no `Authorization` header is sent

### Requirement: URL failover
When the probe or a chunk fails on one URL, the next URL SHALL be tried.

#### Scenario: First mirror down
- **WHEN** the first of two URLs is down
- **THEN** the download completes from the second
