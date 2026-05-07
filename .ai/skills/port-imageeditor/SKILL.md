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
  last_updated: 2026-05-08
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
3. Scan every relevant upstream commit from the previous sync point through the newest relevant commit, not just the tip commit.
4. Build a holistic understanding of the bugs fixed and features added across that whole commit window before changing XerahS.
5. Before implementing anything, publish a concise implementation manifest that lists every bug fix and enhancement identified from the new ShareX commits.
6. For every manifest item, check whether XerahS already fixed it, implemented it differently, partially implemented it, or has a better local behavior that must be preserved.
7. Do not start implementing an individual bug fix or enhancement until it appears in the manifest with its source commit, affected files, XerahS status, and intended XerahS mapping.
8. Prefer the best combined design over a direct ShareX copy: preserve superior XerahS behavior, import superior ShareX behavior, and write a custom implementation when that is the cleanest integration.
9. Read the affected upstream and XerahS files to clarify intent, control flow, rendering behavior, and wiring before re-implementing anything ambiguous.
10. Preserve XerahS-only repository-level differences such as the submodule's `src/` layout, multi-targeting, and any confirmed host integration changes.
11. Do not overwrite XerahS-specific fixes blindly. If a target file already diverged for Avalonia or host integration, port the upstream intent instead of doing a raw replace.
12. This is not a blind cherry-pick workflow. Review the upstream change set, understand the behavior being introduced or fixed, and then map that behavior into the Avalonia submodule.
13. Re-implement the upstream behavior in the XerahS ImageEditor submodule after understanding it, instead of mechanically transplanting diffs.
14. Build before claiming completion.
15. If verification passes and the user did not ask to pause, commit and push the submodule change and then commit and push the XerahS root pointer update.

## Step 0 - Resolve the upstream commit range

### 0a - Confirm the local ShareX checkout is current

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" status --short
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" branch --show-current
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" rev-parse HEAD
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" fetch --prune
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" status --short --branch
```

Use the checked-out branch as the default upstream branch unless the user requests a different ref.

If the local ShareX checkout is behind its upstream tracking branch, pull it before resolving the ImageEditor range:

```powershell
git -C "C:\Users\liveu\source\repos\ShareX Team\ShareX" pull --ff-only
```

If ShareX has local uncommitted changes, do not overwrite them. Prefer:
- If the changes are unrelated and the checkout is only needed for read-only upstream assessment, use `git pull --rebase --autostash` so the newest source is available locally.
- If the changes conflict with the pull or look relevant to `ShareX.ImageEditor`, stop and report that the local ShareX checkout must be cleaned or reviewed before porting.

After pulling, record the updated ShareX `HEAD` and use that local source for the rest of the assessment. This keeps the upstream code local and fast to inspect without cloning ShareX again.

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
Read the whole queue from oldest to newest so you understand how fixes and features build on each other.

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

For a real catch-up, do this for every commit in the pending range from the last synced hash to the newest relevant hash.
Summarize for yourself:
- which user-visible features were added
- which bugs were fixed
- which commits depend on earlier commits in the range
- which files are the authoritative implementation points

### 2c - Publish the implementation manifest before editing

Before changing files, post a concise manifest in chat. This is a mandatory gate for the port session.

The manifest must include every bug fix and enhancement identified in the pending ShareX commits, grouped by implementation item rather than by raw file. Each item must include:
- Type: `Bug fix`, `Enhancement`, `Refactor with behavior impact`, or `Infrastructure`
- Source commit(s): the ShareX hash and short subject
- Upstream behavior: what changed for users or editor internals
- XerahS status: `missing`, `already fixed`, `implemented differently`, `partially implemented`, or `conflicts with XerahS`
- Target files: the mapped XerahS files expected to change
- Decision: raw sync, manual merge, custom implementation, keep XerahS behavior, or intentional skip with reason
- Rationale: why that decision gives XerahS the best behavior

Use this shape:

```markdown
## ImageEditor Port Manifest

- [ ] Bug fix: <short name>
  Source: `<hash>` <subject>
  Upstream behavior: <one sentence>
  XerahS status: <missing/already fixed/implemented differently/partially implemented/conflicts>
  Target files: `<mapped/file.cs>`, `<mapped/file.axaml>`
  Decision: <raw sync/manual merge/custom implementation/keep XerahS/skip>
  Rationale: <why this is the best XerahS outcome>

- [ ] Enhancement: <short name>
  Source: `<hash>` <subject>
  Upstream behavior: <one sentence>
  XerahS status: <missing/already fixed/implemented differently/partially implemented/conflicts>
  Target files: `<mapped/file.cs>`
  Decision: <raw sync/manual merge/custom implementation/keep XerahS/skip>
  Rationale: <why this is the best XerahS outcome>
```

During implementation, work against this manifest. Announce the item being implemented before editing it, and update the item status as it is completed or intentionally skipped.

Do not use `raw sync` as the default. It is only acceptable after comparing the mapped XerahS file and confirming there is no local fix, no different design, and no host-specific behavior worth preserving.

### 2d - Read code to remove ambiguity

If the commit message or patch alone is not enough, read the upstream implementation files and the current XerahS counterparts before editing.

Typical files to inspect:
- controllers
- view models
- rendering and visual factory code
- annotation model classes
- views and control markup
- the target `.csproj` for new files or assets

### 2e - Compare mapped files, not raw repo roots

For each changed upstream file `ShareX.ImageEditor\<relative_path>` compare it to:
`C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\<relative_path>`.

If the target file does not exist, it is a net-new addition and therefore high risk.

### 2f - Compare behavior, not just text

For every manifest item, answer these questions before editing:

- Is the same bug already fixed in XerahS under a different implementation?
- Is the ShareX fix better, worse, or complementary to the XerahS behavior?
- Does XerahS have host integration, Avalonia-specific behavior, tests, or persistence hooks that ShareX does not have?
- Can the best result be achieved by combining both implementations instead of copying either one wholesale?
- Is a small custom implementation clearer than copying the upstream patch?

If XerahS already has an equal or better implementation, keep it and record the item as `keep XerahS behavior` or `skip`.
If both implementations solve different parts of the problem, create a manual merge or custom implementation and record the rationale in `PORT_STATUS.md`.

## Step 3 - Port or sync safely

### 3a - When a raw file sync is acceptable

Raw file sync is an exception, not the normal path. You may replace the target file with the upstream version only when all of these are true:
- The file lives under `Core/`, `Presentation/`, `Hosting/`, or `Assets/` and maps cleanly into `src/ShareX.ImageEditor`
- The target file was compared and does not contain a relevant XerahS fix, alternate implementation, host integration, persistence hook, test-supported behavior, or Avalonia-specific adaptation that would be lost
- The upstream change is exactly what XerahS needs and there is no repo-layout-only difference inside the file
- The manifest item explicitly marks the decision as `raw sync` with a short rationale

### 3b - When manual porting is required

Port the intent instead of copying the whole file when any of these are true:
- The target file contains XerahS-specific Avalonia, SkiaSharp, or host wiring that is not present upstream
- The target file already diverged beyond the pending upstream commit range
- The upstream file assumes repository or project settings that do not match the submodule
- The upstream commit adds a feature partially present in XerahS and a direct replace would regress local behavior
- XerahS already has a different fix for the same bug and the best result needs parts of both implementations
- The upstream code is correct for ShareX but a custom XerahS implementation would be simpler, safer, or better aligned with current XerahS architecture

Manual porting usually means:
- keep the XerahS file as the base
- apply the upstream behavior in small, reviewable hunks
- re-implement the bug fix or feature after understanding the whole upstream flow
- keep any superior XerahS behavior and merge only the missing upstream behavior
- add or update tests for bugs where the upstream fix differs from the XerahS implementation
- rebuild after behavior-critical controller, view model, rendering, or view changes
- only replace the whole file when the diff is layout-only and no XerahS adaptation would be lost

### 3c - When to write a custom implementation

Write a custom implementation when the upstream patch exposes the desired behavior but the XerahS architecture calls for a different shape.

Common cases:
- XerahS already has a related service, adapter, or persistence hook that should own the behavior.
- ShareX solves the bug in UI code, but XerahS should solve it in `EditorCore` or a shared controller.
- The upstream change duplicates logic already centralized in XerahS.
- The upstream behavior is useful, but its exact code would regress RegionCapture, embedded editor mode, tests, serialization, or host integration.
- Combining both implementations would otherwise create duplicated state or unclear ownership.

Record custom implementations in `PORT_STATUS.md` with the source ShareX commit, the XerahS files changed, and the reason for not copying upstream code.

### 3d - Preserve known XerahS repository-level differences

Keep these unless the user explicitly asks to change them:
- `src/ShareX.ImageEditor` repository layout
- XerahS-specific solution or project structure
- XerahS multi-targeting or packaging differences
- Any host integration already verified in XerahS

### 3e - New-file checklist

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
2. Run `git -C <sharex_repo> fetch --prune` and check `git -C <sharex_repo> status --short --branch`.
3. If the local ShareX checkout is behind, run `git -C <sharex_repo> pull --ff-only`; use `--rebase --autostash` only for unrelated local ShareX changes that must be preserved.
4. Run `git -C <sharex_repo> log -1 --format="%H %cs %s" -- ShareX.ImageEditor`.
5. Run `git -C <sharex_repo> diff --name-only <last_sync>..HEAD -- ShareX.ImageEditor`.
6. Map each changed upstream file into `XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor`.
7. Add missing files first.
8. Review every upstream commit in the range so you understand the complete feature and bug-fix set.
9. For each item, compare the upstream behavior against the current XerahS behavior and decide whether it is missing, already fixed, implemented differently, partially implemented, or conflicting.
10. Post the ImageEditor Port Manifest listing every identified bug fix and enhancement, including XerahS status, decision, and rationale, before editing.
11. Read upstream and XerahS code where needed to confirm how the behavior works.
12. Port, manually merge, keep XerahS behavior, or write a custom implementation as appropriate; do not blind cherry-pick or raw-copy diverged Avalonia files.
13. Build the ImageEditor project, then the XerahS solution.
14. Update `PORT_STATUS.md`, then commit and push the submodule and root pointer separately.
