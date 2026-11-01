## 1. Change

- [x] 1.1 `src/dockerfile`: base image `sdk:10.0`; `dotnet restore`; `dotnet build -c Release -f net10.0`; entrypoint `dotnet test -f net10.0`.
- [x] 1.2 `src/docker-compose.yml`: image `sdk:10.0`; remove the old Windows comment.

## 2. Check

- [ ] 2.1 (Not run: no Docker in the cloud session. Note: the SDK 10 image can not restore net11.0, so the commands pass `-p:TargetFrameworks=net10.0`.) Run `docker-compose -p downloader up` from `src/` and see the tests pass.
- [ ] 2.2 Run the `docker build` / `docker run` commands from the README and see them work; fix the README text if needed.
