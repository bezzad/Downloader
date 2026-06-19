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
