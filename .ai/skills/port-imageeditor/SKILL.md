---
name: port-imageeditor
description: Workflow for manually porting bug fixes and new features from ShareX.ImageEditor (ShareX repo) into XerahS.ImageEditor (XerahS submodule). Use whenever examining ShareX.ImageEditor code to port into XerahS. Includes robust regression prevention: risk classification, staged verification, structural comparison, dependency analysis, and build+test gates.
metadata:
  keywords:
    - imageeditor
    - porting
    - diff
    - sharex
    - sync
    - regression
    - risk-assessment
  last_updated: 2026-04-08
---

# Port ImageEditor: ShareX → XerahS

Manually mirrors bug fixes and new features from `ShareX.ImageEditor` in the ShareX repo into the `ShareX.ImageEditor` submodule in XerahS.

## Repositories

| Repo | Path |
|------|------|
| ShareX (source of truth) | `C:\Users\liveu\source\repos\ShareX Team\ShareX\ShareX.ImageEditor\` |
| XerahS (target) | `C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\` |

---

## Regression Prevention: Overview

Every port introduces risk. This workflow gates every step with checks that catch regressions before they reach a commit. The core principle:

> **Never copy code you have not diffed. Never commit code that has not built. Never merge code whose dependencies have not been verified.**

The prevention layers, in order:
1. [Structural inventory](#step-0--structural-inventory) — understand the full shape of both directories
2. [Risk classification](#step-1--classify-risk-before-touching-anything) — determine the blast radius before writing anything
3. [Dependency analysis](#step-2--map-dependencies-before-porting) — know what else might break
4. [Diff-then-port](#step-3--diff-then-port) — compare before touching target
5. [XerahS-specific guard](#step-4--guard-xerahs-specific-adaptation) — preserve XerahS adaptations
6. [Staged build gate](#step-5--staged-build-gate) — build at each stage, not just at the end
7. [Pre-commit regression checklist](#step-6--pre-commit-regression-checklist) — mandatory checklist before committing

---

## Step 0 — Structural Inventory

Before touching any file, take stock of both directories to understand the current state of divergence.

### 0a — List both directory trees

```bash
# List all .cs files in ShareX.ImageEditor (source)
cd "C:/Users/liveu/source/repos/ShareX Team/ShareX/ShareX.ImageEditor"
find . -type f -name "*.cs" | sort

# List all .cs files in XerahS.ImageEditor (target)
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"
find . -type f -name "*.cs" | sort
```

Output both lists side by side. Flag any file that exists in ShareX but **not** in XerahS — those need new-file ports, which carry higher risk.

### 0b — Get the last sync point

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"
git log --oneline -5
```

Record the current submodule HEAD commit. All porting diffs are measured **from this commit** in ShareX.

### 0c — Record the baseline

Before making any changes, record the known-good state of XerahS:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS"
dotnet build ShareX.ImageEditor/ShareX.ImageEditor.csproj
# → must succeed with 0 errors before proceeding
```

If it does not build cleanly, fix the existing build errors first — do not begin porting on a broken baseline.

---

## Step 1 — Classify Risk Before Touching Anything

Classify the port based on what is being changed. This determines how many of the remaining steps are mandatory.

| Risk | Criteria | Required Steps |
|------|----------|----------------|
| **Low** | Bug fix in an isolated, self-contained file; no Avalonia/WPF boundary crossing; no other file references the changed members | Steps 0, 1, 3, 5, 6 |
| **Medium** | Changes cross layers (e.g., Core → Presentation), or the file is referenced by other files in XerahS, or some namespace adaptation is needed | All steps except 4g |
| **High** | Whole-file replacement, new file added, changes annotation rendering, toolbar, or effect system, or ShareX uses WPF-only types | All steps including 4g |

If the change affects any of the following, automatically escalate to **High**:
- `EditorView.axaml` / `EditorView.axaml.cs`
- `MainViewModel.cs`
- `AnnotationVisualFactory.cs`
- `EffectBrowserPanel.axaml.cs`
- `EditorCore.cs`
- `EditorToolbarAdapter.cs`

### 0d — Check if this is a new file

If the ShareX file does **not exist** in XerahS, the risk is automatically **High** (new code has no regression guardrail in XerahS). Treat it as a net-new addition: compare against similar existing files in XerahS for patterns, and apply all steps.

---

## Step 2 — Map Dependencies Before Porting

This is the most commonly skipped step and the most common source of silent regressions.

### 2a — Find what ShareX's changed file depends on

In ShareX.ImageEditor, search the changed file for `using` statements and constructor-injected services. For every dependency, determine:
- Is it in `ShareX.ImageEditor.Core.*`?
- Is it in `ShareX.ImageEditor.Presentation.*`?
- Is it a third-party NuGet?

### 2b — Check which XerahS files reference the target file

In XerahS.ImageEditor, find all files that reference (call, instantiate, or inherit from) the file being ported:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"

# Find all files that reference the target class/file
grep -r "EditorCore\|ClassName" --include="*.cs" -l .
```

Every caller is a **regression surface**. If the port changes a public API (method signature, property name), all callers must be updated. List them explicitly.

### 2c — Identify cascading ports

If the changed file depends on another file that is **also newer in ShareX** (detected by comparing file hashes or git log dates), that dependency must be ported **first** or flagged as a prerequisite. Never port a consumer before its dependency.

Create a dependency list:

```
File to port: Core/Editor/EditorCore.cs
Risk level: High
Cascading prerequisites:
  - Core/ImageEffects/ImageEffectBase.cs  (newer in ShareX — port first)
  - Core/ImageEffects/Parameters/EffectParameters.cs  (no change needed)
Known callers that must be verified post-port:
  - Presentation/Views/EditorView.axaml.cs
  - ViewModels/MainViewModel.cs
  - Controllers/EditorInputController.cs
```

---

## Step 3 — Diff, Then Port

### 3a — Full diff of the specific file

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/ShareX/ShareX.ImageEditor"

# Diff a single file against XerahS submodule HEAD
git diff C:/Users/liveu/source/repos/ShareX\ Team/XerahS/ShareX.ImageEditor -- <relative/path/filename.cs>
```

If the file does not exist in XerahS (new file), use:

```bash
# Show the new file content (it is "new" relative to XerahS)
git show HEAD:<relative/path/filename.cs>
```

### 3b — Understand the diff intent

For each hunk in the diff, determine:
- **What** changed (added lines, removed lines, modified lines)
- **Why** it changed — read the commit message in ShareX: `git log -1 --format="%h %s" <commit>`
- **Whether** the reason applies to XerahS — some bug fixes are WPF-specific and do not apply

Discard hunks that are purely WPF-specific and irrelevant to Avalonia, but **document every discarded hunk** in the PORT_STATUS.md entry.

### 3d — Preserve XerahS features that are not in ShareX

**Porting does not mean removing or replacing existing XerahS features unless ShareX explicitly replaces them with a demonstrably better feature.** XerahS.ImageEditor may contain:

- XerahS-specific Avalonia adaptations (SkiaSharp rendering, CommunityToolkit.Mvvm, platform integrations)
- XerahS-specific UI/UX choices (styling, themes, window management)
- XerahS-specific hosting integration (clipboard, desktop wallpaper, hotkey bridges)

**When the ShareX change would remove or overwrite XerahS-specific code:**

| Scenario | Action |
|----------|--------|
| ShareX removes a method/class that XerahS overrides for Avalonia | Do NOT apply the removal — keep the XerahS override |
| ShareX refactors a layer that XerahS adapted differently | Do NOT apply the refactor — keep the XerahS adaptation intact |
| ShareX renames a public API that XerahS uses | Adapt the rename in XerahS, but do not remove XerahS-specific call sites |
| ShareX adds a feature that conflicts with a XerahS-specific feature | Flag as a conflict in PORT_STATUS.md and do not port without explicit review |

**Rule of thumb:** If the diff shows a deletion of XerahS-specific code (e.g., Avalonia-specific handling, XerahS hosting bridges), do not apply that deletion. Only apply additions and modifications.

### 3c — Identify XerahS-specific adaptation points

Mark every line that will need adaptation before porting:

```
Line 42:  using System.Windows.Media;         → SkiaSharp.SKColor (adapt)
Line 87:  var bitmap = new BitmapSource();    → SKBitmap / SKImage (replace)
Line 103: Command.Execute()                   → CommunityToolkit.Mvvm ICommand (adapt)
Line 215: #if WPF                           → #if AVALONIA or remove (assess)
```

---

## Step 4 — Guard XerahS-Specific Adaptation

XerahS.ImageEditor contains adaptations for Avalonia that have no WPF equivalent. These must **never be overwritten** by a ShareX port.

### 4a — Locate XerahS-specific override regions

Search for comments that mark XerahS-specific code:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"
grep -r "XerahS\|AVALONIA\|SkiaSharp\|SKBitmap\|Avalonia" --include="*.cs" -n
```

### 4b — Check for conditional compilation

XerahS may use `#if AVALONIA` or similar guards. Verify the preprocessor symbols defined in the XerahS.ImageEditor.csproj:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"
cat ShareX.ImageEditor.csproj | grep -A5 "DefineConstants"
```

### 4c — Preserve SkiaSharp bridging code

XerahS rendering is SkiaSharp-based. If the ShareX file touches image rendering (filters, annotations, canvas), verify that XerahS has equivalent SkiaSharp helpers in:
- `ShareX.ImageEditor/Core/ImageEffects/Helpers/ImageHelpers.cs`
- `ShareX.ImageEditor/Presentation/Rendering/SkiaSharpConversions.cs`
- `ShareX.ImageEditor/Presentation/Rendering/BitmapConversionHelpers.cs`

Do not overwrite these files with WPF equivalents.

### 4d — Preserve CommunityToolkit.Mvvm wiring

If the port touches ViewModels, ensure `[ObservableProperty]` and `[RelayCommand]` patterns from CommunityToolkit.Mvvm are preserved and not replaced by WPF INotifyPropertyChanged boilerplate.

### 4e — Preserve annotation visual layer

Annotation visuals in XerahS are rendered via SkiaSharp in `AnnotationVisuals/` — they are not WPF shapes. Do not port WPF shape definitions into these files unless explicitly verified.

### 4f — Preserve hosting/integration interfaces

Files under `Hosting/` define how ImageEditor integrates with XerahS' host application. These interfaces (e.g., `IClipboardService`, `IDesktopWallpaperService`) may differ from ShareX. Do not assume ShareX's `Hosting/` files are directly portable.

### 4g — Automated XerahS-specific check (High risk only)

For **High** risk ports, run this check before committing:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"

# Warn if WPF types were accidentally introduced
grep -r "System\.Windows\|Windows\.Media\|Windows\.Controls\|BitmapSource\|DrawingVisual" --include="*.cs" -n .

# Warn if ReactiveUI was introduced (XerahS uses CommunityToolkit.Mvvm)
grep -r "ReactiveObject\|ReactiveCommand\|WhenAnyValue" --include="*.cs" -n .
```

If any of these fire, the port introduced a WPF dependency and must be corrected before committing.

---

## Step 5 — Staged Build Gate

Build **at every stage**, not just at the end.

### Stage A — Baseline build (already done in Step 0c)

Must be clean before starting.

### Stage B — After dependency files are ported (but before the main file)

If cascading ports were needed, build after each prerequisite port:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS"
dotnet build ShareX.ImageEditor/ShareX.ImageEditor.csproj
# → must succeed before continuing
```

### Stage C — After main file is ported

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS"
dotnet build ShareX.ImageEditor/ShareX.ImageEditor.csproj
# → must succeed
```

### Stage D — Full solution build

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS"
dotnet build src/desktop/XerahS.sln
```

If the full solution does not build, the port has introduced a breaking change to the host app. Investigate and fix before proceeding.

---

## Step 6 — Pre-Commit Regression Checklist

Complete this checklist **before** running `git add` or committing.

### Functional checklist

- [ ] **The ShareX bug fix / feature reason is understood and documented** in PORT_STATUS.md
- [ ] **Every diff hunk has been assessed**: kept, adapted, or documented-as-discarded
- [ ] **No WPF types** (`System.Windows.*`, `BitmapSource`, `DrawingVisual`) were introduced (verify with Step 4g)
- [ ] **No ReactiveUI** was introduced (verify with Step 4g)
- [ ] **All callers** identified in Step 2b still compile and have correct behavior
- [ ] **Conditional compilation** guards (`#if`) are correct for XerahS
- [ ] **Namespace** follows XerahS conventions (`ShareX.ImageEditor.<Layer>.*`)
- [ ] **NuGet packages** added to XerahS.ImageEditor.csproj are listed in PORT_STATUS.md

### Build checklist

- [ ] `dotnet build ShareX.ImageEditor/ShareX.ImageEditor.csproj` → 0 errors
- [ ] `dotnet build src/desktop/XerahS.sln` → 0 errors
- [ ] No new compiler warnings introduced by the port

### Port status checklist

- [ ] PORT_STATUS.md updated with: ShareX commit hash, files touched, risk level, adaptation notes, and your name/date

---

## Step 7 — Apply the Port

Only after **all** checklist items in Steps 0–6 are complete.

Apply changes manually in `XERAHS_TARGET`. Common porting actions:

| Action | When to Use |
|--------|-------------|
| **Copy method body** | Bug fix where the algorithm is the fix |
| **Adapt types** | WPF type → SkiaSharp/Avalonia equivalent |
| **Adapt namespaces** | e.g., `ShareX.ImageEffects` → `ShareX.ImageEditor.Core.ImageEffects` |
| **Copy whole file (new file)** | XerahS has no equivalent — create from ShareX source |
| **Skip / document and discard** | WPF-only code that has no Avalonia equivalent |

---

## ONE COMMIT PER CHANGE: Staging and Committing Rules

These rules exist to make every commit independently revertible and to keep the git history auditable for port traceability.

### 7a — One bug fix or one feature per commit

Each ShareX change being ported — regardless of how many files it touches — constitutes **one commit**. If a single ShareX commit modifies `EditorCore.cs`, `EffectParameters.cs`, and `EffectSlider.cs` as a coherent unit, port them as **one commit** with all three files.

If the ShareX commit is large and self-contained (e.g., 50 new image effects), you may split it across multiple commits **only by effect group**, not by file — and each split must be noted in PORT_STATUS.md.

Do **not** combine two unrelated ShareX bug fixes into one commit.

### 7b — Work on one port at a time

If multiple ShareX commits need to be ported, complete and commit **one** before beginning the next:

```
Port 1: ShareX@abc1234 → commit [ShareX.ImageEditor] [Port] Fix X from ShareX@abc1234
Port 2: ShareX@def5678 → commit [ShareX.ImageEditor] [Port] Add Y from ShareX@def5678
```

Do not stage changes from two different ShareX commits into one XerahS commit.

### 7c — Staging procedure

After completing a single port and verifying the build, stage **only the files for that port**:

```bash
cd "C:/Users/liveu/source/repos/ShareX Team/XerahS"

# Stage files for THIS port only
git add ShareX.ImageEditor/Core/Editor/EditorCore.cs

# Verify what is staged — nothing unrelated
git status
```

If `git status` shows files you did not intend to stage, unstage them with `git restore --staged <path>` before committing.

### 7d — Commit each port immediately after verification

Do not accumulate uncommitted ports. After a port passes the build gate (Stage C or D), commit it before moving to the next ShareX commit.

### 7e — Submodule commit rules

Since `ShareX.ImageEditor` is a git submodule of XerahS:

1. Commit the ported changes to the **submodule** first (inside `ShareX.ImageEditor/`):
   ```bash
   cd "C:/Users/liveu/source/repos/ShareX Team/XerahS/ShareX.ImageEditor"
   git add <changed files>
   git commit -m "[ShareX.ImageEditor] [Port] <desc> from ShareX@<hash>"
   ```
2. Then return to the XerahS root and commit the submodule update:
   ```bash
   cd "C:/Users/liveu/source/repos/ShareX Team/XerahS"
   git add ShareX.ImageEditor   # records the new submodule HEAD
   git commit -m "[ShareX.ImageEditor] [Port] <desc> from ShareX@<hash>"
   ```

The XerahS root commit pins the submodule to the new commit. Both commits together form the complete port.

---

## Tracking Port Status

Update `ShareX.ImageEditor/PORT_STATUS.md` in XerahS after each session:

```markdown
## Ported from ShareX (commit <hash>)

| File/Feature | ShareX Commit | XerahS Location | Risk | Status | Notes |
|--------------|---------------|-----------------|------|--------|-------|
| EditorCore.cs | 8a51a9a | Core/Editor/ | High | ✅ Ported | SkiaSharp canvas state fix |
| RemoveBackground filter | abcd123 | Core/ImageEffects/Filters/ | High | 🔄 In progress | WPF types to strip |
```

If `PORT_STATUS.md` does not exist, create it at `ShareX.ImageEditor/PORT_STATUS.md`.

---

## Commit Message for Ported Changes

When committing a port to XerahS, use:

```
[ShareX.ImageEditor] [Port] <description> from ShareX@<commit>
```

Example:
```
[ShareX.ImageEditor] [Port] EditorCore canvas state fix from ShareX@8a51a9a
```

Per `AGENTS.md`, submodule-only commits omit the version prefix. Do NOT add `[vX.Y.Z]` for ImageEditor submodule ports.

---

## Key Differences: ShareX.ImageEditor vs XerahS.ImageEditor

| Aspect | ShareX | XerahS |
|--------|--------|--------|
| UI Framework | WPF | Avalonia (.NET 10) |
| Base namespace | `ShareX.ImageEditor` | `ShareX.ImageEditor` |
| Annotations layer | WPF Shapes | Custom SkiaSharp rendering |
| Effects layer | WPF BitmapEffects | SkiaSharp image effects |
| MVVM | WPF ICommand / ViewModelBase | CommunityToolkit.Mvvm |
| Image rendering | WPF BitmapSource | SkiaSharp `SKBitmap` / `SKImage` |
| Preprocessor guard | `#if WPF` | `#if AVALONIA` |

**Always assume ShareX uses WPF types** unless the file is in `ShareX.ImageEditor\Hosting` or clearly marked as cross-platform.
