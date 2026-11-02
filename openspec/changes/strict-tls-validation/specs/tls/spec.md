## ADDED Requirements

### Requirement: Server certificates are checked by the platform
The downloader SHALL use the platform's default server-certificate validation and SHALL NOT accept
expired, self-signed or wrong-host certificates on its own.

#### Scenario: Self-signed certificate
- **WHEN** a download URL is served over HTTPS with a self-signed certificate
- **THEN** the download fails with a TLS error

#### Scenario: Caller trusts a private certificate
- **WHEN** the caller sets `CustomHttpMessageHandlerFactory` to a handler whose callback accepts that certificate
- **THEN** the download works
