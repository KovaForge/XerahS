# Remote Identity Matrix

> XIP0077 U7 — Single source of truth for XerahS remote/identity configuration.

## Remote ↔ Agent Mapping

| Agent | Git Wrapper | Remote Name | SSH Host Alias | Verify Ref After Push | Push Command |
|---|---|---|---|---|---|
| Aoife | `git-aoife` | `aoife` | `github-aoife` | `refs/remotes/aoife/develop` | `git-aoife push` |
| Mikhail | `git-mikhail` | `mikhail` (also `origin`) | `github-mikhail` | `refs/remotes/mikhail/develop` or `refs/remotes/origin/develop` | `git -C <repo> push mikhail develop` |
| Declan | `git-declan` | `declan` | `github-declan` | `refs/remotes/declan/develop` | `git-declan push` |
| Vladislava | `git-vladislava` | `vladislava` (also `origin`) | `github-vladislava` | `refs/remotes/origin/develop` | `git-vladislava push` |

## Verification Protocol

**Rule: Fetch before compare. A stale remote-tracking ref is not proof of a push.**

After every push, run:
```bash
scripts/verify-push.sh <remote> <branch>
```

This script:
1. Fetches the named remote (30 s timeout)
2. Compares `HEAD` to `refs/remotes/<remote>/<branch>`
3. Prints `PUSH_VERIFIED` or `PUSH_NOT_VERIFIED <details>`

### Success Criteria

- `PUSH_VERIFIED` = `HEAD` equals the remote-tracking ref after fetch
- `PUSH_NOT_VERIFIED` = mismatch, with details (commits ahead, fetch failure, etc.)

### Hard Blockers

Classify `Permission denied (publickey)` as a **hard blocker** in all pipelines:
- Do not retry with alternate push syntaxes
- Do not claim success from stale local `refs/remotes/<remote>/develop`
- Report the SSH auth error, local HEAD, remote ref, and dirty/clean status

## Upstream Remote

| Remote | URL | Purpose |
|---|---|---|
| `upstream` | `https://github.com/ShareX/XerahS.git` | ShareX upstream (read-only) |

Upstream is never pushed to; only fetched/merged.

## Submodule (ShareX.ImageEditor)

The submodule has its own remote configuration. Use bare git + `-c` flags
for submodule pushes (the per-agent wrappers are for the XerahS parent repo only):

```bash
git -c user.name="<Agent Name>" -c user.email="<agent>@kovaforge" push origin develop
```
