## ADDED Requirements

### Requirement: Credentials are not sent to another host
The downloader SHALL send the configured `Authorization` header (or the Basic header from
`Credentials`) only to the scheme, host and port of the URL the caller gave.

#### Scenario: Redirect to another host
- **WHEN** the download URL redirects to a different host and `Authorization` is set
- **THEN** the requests to the new host have no `Authorization` header

#### Scenario: Redirect from https to http
- **WHEN** an https URL redirects to an http URL
- **THEN** no `Authorization` header is sent over http
