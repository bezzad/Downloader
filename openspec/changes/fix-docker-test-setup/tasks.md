## 1. Change

- [x] 1.1 `src/dockerfile`: base image `sdk:10.0`; `dotnet restore`; `dotnet build -c Release -f net10.0`; entrypoint `dotnet test -f net10.0`.
- [x] 1.2 `src/docker-compose.yml`: image `sdk:10.0`; remove the old Windows comment.

## 2. Check

- [x] 2.1 (Owner ran it on 2026-11-02: Passed. New Docker uses `docker compose` with a space; the README now says so. Note: the SDK 10 image can not restore net11.0, so the commands pass `-p:TargetFrameworks=net10.0`.) Run `docker-compose -p downloader up` from `src/` and see the tests pass.
- [x] 2.2 (Owner ran it on 2026-11-02: Passed.) Run the `docker build` / `docker run` commands from the README and see them work; fix the README text if needed.
