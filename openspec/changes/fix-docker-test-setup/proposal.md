## Why

**Priority: Low (tooling).**

`src/dockerfile` and `src/docker-compose.yml` still use the **.NET 6 SDK** and build with `-f net6.0`.
No project targets `net6.0` any more (library: net8/9/10/11, tests: net10/11), so the README commands
`docker-compose -p downloader up` and `docker build -f ./dockerfile ...` fail. The compose file also has
a stale comment about a Windows service that does not exist.

(The third README command, `docker run ... sdk:10.0 ... dotnet test`, was fixed in the docs PR.)

## What Changes

- Use `mcr.microsoft.com/dotnet/sdk:10.0` in both files and run the tests with `-f net10.0`.
- Remove the stale comment.
- Check the README docker section once more after the change.

## Capabilities

### New Capabilities
- `test-tooling`: running the tests in Docker.

## Impact

- `src/dockerfile`, `src/docker-compose.yml`, README "Instructions for Contributing".
- Must be checked on a machine with Docker (this review box has none).
