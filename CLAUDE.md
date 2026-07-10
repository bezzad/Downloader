# CLAUDE.md

Guidance for working in this repository.

## Workflow & progress tracking

- **Work directly on `develop`.** Do ALL work on the `develop` branch. Never create
  feature branches.
- **Commit frequently** — one commit per logical step, with clear messages — and **push
  to `develop`** so any machine can pull the latest state.
- **Never strand work.** If work is unfinished at the end of a session, commit WIP to
  `develop` anyway with a `wip:` message prefix so nothing is stuck on one machine.
- **Maintain `PLAN.md` at all times** as the source of truth:
  - When given tasks, write them into **Todo** first, then start.
  - Move a task to **Active** and mark it `[~]` when you start it.
  - Mark it `[x]` and move it to **Done** with a one-line note and the commit hash when
    finished.
  - Mark it `[!]` and move it to **Blocked/Failed** with the reason if it fails.
  - Update **"Last updated"** and **"Now working on"** each time.
- **Commit `PLAN.md` together with the code change it describes**, on `develop`.
- **For large backlogs, also keep `TASKS.md` updated** as the full board.
- **At the START of every session, read `PLAN.md` (and `TASKS.md`) and continue from
  there.** Never rely on in-session memory surviving across machines — if it matters, it
  must be in `PLAN.md` and committed.

## Packaging

- **Always pack the NuGet package in `Release` mode, output to `src/Downloader/bin/nupkg/`.**
  This is the project's standard package output location.

  ```bash
  dotnet pack src/Downloader/Downloader.csproj -c Release -o src/Downloader/bin/nupkg/
  ```

  This produces `Downloader.<version>.nupkg` (and the symbols package) there. Use this path
  for any local pack/publish step instead of a temp directory.

## Token-efficient builds & tests (MANDATORY)

- **`dotnet build`**: always run with `-v q --nologo`. Only re-run without `-v q` if you need
  to inspect a specific error in detail.
- **`dotnet test`**: always run with `-v q --nologo`. On failure, re-run ONLY the failing
  test(s) with `--filter FullyQualifiedName~<TestName>` instead of the whole suite.
- **Long-running commands** (`dotnet test`, `dotnet build`, `dotnet pack`, `gh run watch`):
  run them with `run_in_background: true` and wait for the completion notification — never
  poll in a `while … sleep` loop, and never dump their full output into context. After
  completion, read only the tail / failure section of the output.
