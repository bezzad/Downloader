## 1. Change

- [ ] 1.1 `src/dockerfile`: base image `sdk:10.0`; `dotnet restore`; `dotnet build -c Release -f net10.0`; entrypoint `dotnet test -f net10.0`.
- [ ] 1.2 `src/docker-compose.yml`: image `sdk:10.0`; remove the old Windows comment.

## 2. Check

- [ ] 2.1 Run `docker-compose -p downloader up` from `src/` and see the tests pass.
- [ ] 2.2 Run the `docker build` / `docker run` commands from the README and see them work; fix the README text if needed.
