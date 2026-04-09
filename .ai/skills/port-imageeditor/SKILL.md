---
name: port-imageeditor
description: Use the local ShareX checkout as the source of truth for ShareX.ImageEditor, find the latest upstream commit that touches it, and port or sync the matching changes into the XerahS ShareX.ImageEditor submodule with path-aware diffing and build gates.
metadata:
  keywords:
    - imageeditor
    - porting
    - sync
    - sharex
    - submodule
    - avalonia
    - skia
  last_updated: 2026-04-09
---

# Port ImageEditor: Local ShareX -> XerahS

Use this workflow whenever XerahS needs to catch up with the current `ShareX.ImageEditor`
state from the local ShareX repo.

## Source of truth

Do not clone ShareX again. The local ShareX checkout is the upstream reference:

| Role | Path |
|------|------|
| Upstream ShareX repo | `C:\Users\liveu\source\repos\ShareX Team\ShareX` |
| Upstream source tree | `C:\Users\liveu\source\repos\ShareX Team\ShareX\ShareX.ImageEditor` |
| XerahS root | `C:\Users\liveu\source\repos\ShareX Team\XerahS` |
| XerahS ImageEditor repo | `C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor` |
| XerahS ImageEditor code root | `C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor` |

Hardcoded local paths are intentional here. They make this workflow faster and more reliable.

## Core rules

1. The newest relevant upstream commit must be resolved from the local ShareX repo's git history, not guessed.
2. Diff against the mapped XerahS code root. The upstream source lives at `ShareX\ShareX.ImageEditor\...`; the target code lives at `XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\...`.
3. Preserve XerahS-only repository-level differences such as the submodule's `src/` layout, multi-targeting, and any confirmed host integration changes.
4. Do not overwrite XerahS-specific fixes blindly. If a target file already diverged for Avalonia or host integration, port the upstream intent instead of doing a raw replace.
5. This is not a blind cherry-pick workflow. Review the upstream change set, understand the behavior being introduced or fixed, and then map that behavior into the Avalonia submodule.
6. Build before claiming completion.
7. If verification passes and the user did not ask to pause, commit and push the submodule change and then commit and push the XerahS root pointer update.

## Step 0 - Resolve the upstream commit range

### 0a - Confirm the local ShareX checkout is current

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" status --short
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" branch --show-current
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" rev-parse HEAD
```

The repo is expected to already be pulled locally. Use the checked-out branch as the default upstream branch unless the user requests a different ref.

### 0b - Find the latest ShareX commit that touches `ShareX.ImageEditor`

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" `
  log -1 --format="%H %cs %s" -- ShareX.ImageEditor
```

This is the latest relevant upstream commit. Record it.

### 0c - Find the last recorded sync point in XerahS

Read `C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\PORT_STATUS.md`.

Expected fields:
- `ShareX.ImageEditor commit: <hash>`
- `XerahS submodule last synced to: <hash>`

If the file is missing or stale, derive the baseline from repo history and note the assumption in the final update.

### 0d - List pending upstream commits

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" `
  log --reverse --oneline <last_synced_sharex_hash>..HEAD -- ShareX.ImageEditor
```

Use this list to decide whether the catch-up is:
- Low risk: isolated bug fix in a small file
- Medium risk: touches controllers, view models, or multiple files
- High risk: adds files, changes tooling or rendering, or updates editor interaction behavior

Do not treat this commit list as a queue for blind cherry-picks. Use it as a review list for semantic porting.

## Step 1 - Map source paths to target paths

The ShareX tree and XerahS submodule do not have the same repository layout.

| Upstream path | Target path |
|---------------|-------------|
| `ShareX.ImageEditor\Assets\...` | `ShareX.ImageEditor\src\ShareX.ImageEditor\Assets\...` |
| `ShareX.ImageEditor\Core\...` | `ShareX.ImageEditor\src\ShareX.ImageEditor\Core\...` |
| `ShareX.ImageEditor\Hosting\...` | `ShareX.ImageEditor\src\ShareX.ImageEditor\Hosting\...` |
| `ShareX.ImageEditor\Presentation\...` | `ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\...` |
| `ShareX.ImageEditor\ShareX.ImageEditor.csproj` | `ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj` |

Do not diff the upstream folder against the submodule repo root. Always diff it against
`src\ShareX.ImageEditor`.

## Step 2 - Inspect the exact upstream delta

### 2a - List files changed since the last sync

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" `
  diff --name-only <last_synced_sharex_hash>..HEAD -- ShareX.ImageEditor
```

### 2b - Review each pending commit with stats

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" `
  show --stat --summary --oneline <sharex_commit>
```

Also inspect the actual patch for behavior-critical commits:

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" `
  show <sharex_commit> -- ShareX.ImageEditor
```

### 2c - Compare mapped files, not raw repo roots

For each changed upstream file `ShareX.ImageEditor\<relative_path>` compare it to:
`C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\<relative_path>`.

If the target file does not exist, it is a net-new addition and therefore high risk.

## Step 3 - Port or sync safely

### 3a - When a raw file sync is acceptable

You may replace the target file with the upstream version when all of these are true:
- The file lives under `Core/`, `Presentation/`, `Hosting/`, or `Assets/` and maps cleanly into `src/ShareX.ImageEditor`
- The target file does not contain known XerahS-only adaptation that would be lost
- The upstream change is exactly what XerahS needs and there is no repo-layout-only difference inside the file

### 3b - When manual porting is required

Port the intent instead of copying the whole file when any of these are true:
- The target file contains XerahS-specific Avalonia, SkiaSharp, or host wiring that is not present upstream
- The target file already diverged beyond the pending upstream commit range
- The upstream file assumes repository or project settings that do not match the submodule
- The upstream commit adds a feature partially present in XerahS and a direct replace would regress local behavior

Manual porting usually means:
- keep the XerahS file as the base
- apply the upstream behavior in small, reviewable hunks
- rebuild after behavior-critical controller, view model, rendering, or view changes
- only replace the whole file when the diff is layout-only and no XerahS adaptation would be lost

### 3c - Preserve known XerahS repository-level differences

Keep these unless the user explicitly asks to change them:
- `src/ShareX.ImageEditor` repository layout
- XerahS-specific solution or project structure
- XerahS multi-targeting or packaging differences
- Any host integration already verified in XerahS

### 3d - New-file checklist

For each new upstream file:
1. Create the mapped target directory if needed.
2. Add the file under `src/ShareX.ImageEditor`.
3. Update the target `.csproj` only if the new file requires an explicit item entry.
4. Search for references to the new type or view and port the wiring in the same session.

## Step 4 - Verification gates

### 4a - Targeted ImageEditor build

```powershell
cd "C:\Users\liveu\source\repos\ShareX Team\XerahS"
dotnet build "ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj" -m:1
```

If it stalls, stop it before 5 minutes and clear the lock before retrying.

### 4b - Full solution build

```powershell
cd "C:\Users\liveu\source\repos\ShareX Team\XerahS"
dotnet build "src\desktop\XerahS.sln" -m:1
```

This must finish with 0 errors before any push.

## Step 5 - Update tracking

After the catch-up:
1. Update `C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\PORT_STATUS.md`
2. Record:
   - latest upstream ShareX commit used
   - previous recorded sync point
   - files added or updated
   - risk summary
   - adaptations kept for XerahS

Suggested status block:

```markdown
## Port Activity (2026-04-09)

- Previous recorded ShareX sync: `<old_hash>`
- Latest upstream ShareX commit touching ShareX.ImageEditor: `<new_hash>`
- Result: `Caught up through <new_hash>`
- Notes: `<manual adaptations or intentional skips>`
```

## Step 6 - Commit discipline

The submodule is a shared library repo, so submodule commits do not use the XerahS version prefix.

Use:

```text
[ShareX.ImageEditor] [Port] <description> from ShareX@<hash>
```

Then update the XerahS root repo to point to the new submodule commit.

## Step 7 - Push discipline

After verification succeeds:

1. Commit the `ShareX.ImageEditor` submodule changes.
2. Push the submodule branch.
3. Stage the updated submodule pointer and any root tracking or skill changes in `XerahS`.
4. Commit the XerahS root repo using the next unreleased XerahS version prefix.
5. Push the XerahS root branch.

Do not stop after a local commit unless the user explicitly asks to pause before push.

## Fast path for this repo

For the common "catch up XerahS to the latest local ShareX state" task:

1. Read `PORT_STATUS.md` to get the last synced ShareX hash.
2. Run `git -C <sharex_repo> log -1 --format="%H %cs %s" -- ShareX.ImageEditor`.
3. Run `git -C <sharex_repo> diff --name-only <last_sync>..HEAD -- ShareX.ImageEditor`.
4. Map each changed upstream file into `XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor`.
5. Add missing files first.
6. Port or replace changed files as appropriate, but do not blind cherry-pick or raw-copy diverged Avalonia files.
7. Build the ImageEditor project, then the XerahS solution.
8. Update `PORT_STATUS.md`, then commit and push the submodule and root pointer separately.
