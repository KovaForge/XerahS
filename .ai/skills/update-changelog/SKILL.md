---
name: update-changelog
description: Rules and workflows for updating docs/CHANGELOG.md, including version grouping, consolidation, and commit-entry attribution.
---

## Automation Script (Recommended)

Use the helper script to generate a draft section from commits since the last tag, grouped into changelog categories, **with similar commits consolidated by default** (see notes below).

Script path:

```powershell
.ai/skills/update-changelog/scripts/update-changelog.ps1
```

Preview only (prints generated markdown):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1
```

Generate draft from an explicit tag/version and save to a file:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1 -FromTag v0.18.9 -Version 0.19.0 -OutputPath build/changelog-draft.md
```

Apply directly to `docs/CHANGELOG.md`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1 -FromTag v0.18.9 -Version 0.19.0 -Apply
```

Per-commit lines only (disables automatic similarity merge):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1 -FromTag v0.18.9 -Version 0.19.0 -NoConsolidation
```

Notes:
- `-Version` defaults to root `Directory.Build.props`.
- `-FromTag` defaults to `git describe --tags --abbrev=0`.
- The script upserts `## vX.Y.Z` (replaces existing section for that version or inserts after `## Unreleased`).
- **Default consolidation**: `Get-ConsolidationBucket` in `scripts/update-changelog.ps1` merges commits that match the same similarity bucket (for example: **ShareX.ImageEditor** in the subject, **2026-... blog** draft series, **XIP/IEIP** docs, **Linux** install/capture documentation, **IEIP/XIP proposal `.md`** create/update under Changed, **multipart / S3 multipart**). Extend that function when new repetitive patterns appear.
- Always **manually review** for wording, missed merges, and contributor attribution (`#PR`, `@user`) before publishing.

## Version Grouping Strategy

### Current Unreleased Work
- Use the latest released tag as the default lower bound.
- Consolidate all commits after that tag into one heading for the target version, normally the root `Directory.Build.props` version.
- Do not create multiple patch or prerelease headings for the same unreleased range unless the user explicitly requests a historical reconstruction.

### Historical Stable Release Reconstruction
- When rebuilding old changelog history across multiple stable releases, group entries by stable minor release boundaries.
- Use git tags and `Directory.Build.props` history to identify those boundaries.
- Fold patch and prerelease entries into the next stable minor heading unless a patch release was intentionally standalone.
- Retain original context if useful, for example: `Feature: ... (originally v0.8.1)`.

### Consolidation Rules
- Combine related commits that affect the same component and purpose.
- Keep different components separate unless they are part of one coherent user-facing change.
- Keep commits with external contributor attribution separate when merging would obscure credit.

## Commit Entry Handling

### Specific Commit Assignment
- Respect specific user requests to assign certain commits to specific versions.
- **Example**: "List commit `298457a` under **v0.11.0**."
- Always verify the commit hash and subject before assignment.

### Attribution
- **External Contributors**: Attribute Pull Requests from external contributors by including the PR number and their username.
    - **Format**: `(#PR_NUMBER, @username)`
    - **Example**: `(#77, @Hexeption)`
- **Maintainer Merges**: Exclude merge commits from the main maintainer (e.g., `McoreD`) from having explicit attribution unless they contain significant unique work not covered by other commits. The focus is on crediting other users.

### Categorization
Group changes within each version using standard categories:
- **Features**: New functionality.
- **Fixes**: Bug fixes.
- **Refactor**: Code improvements without external behavior change.
- **Build**: Build system, dependencies, and packaging.
- **Documentation**: User, developer, proposal, and release documentation.
- **Testing**: Test coverage and test infrastructure.
- **Performance**: Performance improvements.
- **Changed**: Fallback for changes that do not map cleanly to the categories above.

The helper script maps infrastructure and chore-style commit types into **Build** unless the commit subject carries a clearer component/category signal.

### Entry Consolidation to Reduce Line Count
**CRITICAL**: Consolidate related commits into single entries to keep the changelog concise and readable.

The automation script does this **by default**; agents should still **edit the draft** for narrative quality and any merges the heuristics miss.

#### Guidelines:
- **Group by Component and Purpose**: Combine multiple commits that affect the same component and serve the same purpose.
- **Preserve All Commit Hashes**: When consolidating, include all relevant commit hashes in a single line.
- **Target Reduction**: Aim for 30-50% line reduction by consolidating related work.

#### Examples:

**Before (verbose)**:
```markdown
- **Media Explorer**: Add `IUploaderExplorer` interface `(9deedf9)`
- **Media Explorer**: Implement S3 file browser `(9deedf9)`
- **Media Explorer**: Implement Imgur album browser `(9deedf9)`
- **Media Explorer**: Add navigation, breadcrumbs, search, filter `(9deedf9)`
- **Media Explorer**: Add bandwidth savings banner `(e374160)`
```

**After (consolidated)**:
```markdown
- **Media Explorer**: Implement provider file browsing with S3 and Imgur support, including navigation, search, filtering, and CDN thumbnail optimization `(9deedf9, e374160)`
```

**Before (mobile features)**:
```markdown
- **Mobile**: Add adaptive mobile theming infrastructure `(4b79ddb)`
- **Mobile**: Refactor mobile views for adaptive native styling `(a7cfb22)`
- **Mobile**: Align mobile heads with native theming defaults `(1e5f9eb)`
- **Mobile**: Complete sprint 5 mobile theming polish and docs `(30bbe98)`
- **Mobile**: Add mobile upload queue and picker `(68d97d9)`
- **Mobile**: Add mobile upload history screens `(52d6ad2)`
```

**After (consolidated)**:
```markdown
- **Mobile**: Add adaptive theming infrastructure with native styling polish `(4b79ddb, a7cfb22, 1e5f9eb, 30bbe98)`
- **Mobile**: Add upload queue, picker, and history screens `(68d97d9, 52d6ad2)`
```

**Before (fixes)**:
```markdown
- **Scrolling Capture**: Always auto-scroll to top `(1fa45f2)`
- **Scrolling Capture**: Apply workflow settings and refresh hotkeys `(971219c)`
- **Scrolling Capture**: Use current scroll position for detection `(8ac2c8b)`
```

**After (consolidated)**:
```markdown
- **Scrolling Capture**: Improve auto-scroll behavior and workflow settings integration `(1fa45f2, 971219c, 8ac2c8b)`
```

#### When NOT to Consolidate:
- Commits from different components (e.g., don't merge "Mobile" with "Linux Capture")
- Commits with external contributor attribution (keep separate for visibility)
- Significant standalone features that deserve their own entry

## Format
Follow the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format with Semantic Versioning.

```markdown
## vX.Y.Z

### Features
- **Component**: Description `(short-hash)`
- **Component**: Description `(short-hash, short-hash)`

### Fixes
- Description `(short-hash)`
```

## Workflow

### Step-by-Step Process

1. **Choose the Changelog Mode**
   - Current unreleased work: use the latest released tag as the lower bound and the root `Directory.Build.props` version as the target heading.
   - Historical stable release reconstruction: use two stable release tags as the range and group output under the newer stable release heading.

2. **Identify the Range**
   - Default helper-script behavior: omit `-FromTag` to use `git describe --tags --abbrev=0`.
   - Explicit current-range example: `-FromTag v0.21.2 -Version 0.22.0`.
   - Historical stable reconstruction example: compare `v0.PREV_STABLE..v0.LATEST_STABLE`.

   Fallback tag listing:
   ```powershell
   git tag -l --sort=-version:refname | Select-Object -First 10
   ```

3. **Check Target Version**
   Read the root `Directory.Build.props` `<Version>` property unless the user explicitly provided a historical target version.

4. **Consolidate Version Headings**
   - Current unreleased work: create or update one `## vX.Y.Z` heading for the target version.
   - Historical reconstruction: preserve stable release boundaries, but fold patch/prerelease fragments into the stable heading unless a patch release was intentionally standalone.

5. **Categorize Commits**
   - Group commits into: Features, Fixes, Refactor, Build, Documentation, Testing, Performance, Changed
   - Within each category, group by component (e.g., Mobile, Linux Capture, Editor)

6. **Consolidate Related Entries**
   - Identify commits affecting the same component with similar purpose
   - Merge them into single, comprehensive entries
   - Preserve all commit hashes
   - Aim for 30-50% reduction in line count

7. **Format and Verify**
   - Ensure proper markdown formatting
   - Verify all commit hashes are present
   - Check that external contributor attributions are preserved
   - Confirm adherence to Keep a Changelog format

8. **Fix Double-Encoding Mojibake**
   After any write to `docs/CHANGELOG.md`, scan for mojibake characters (commonly `ΓÇö`, `Ã─`, etc.) and replace them with their correct Unicode equivalents. This occurs when UTF-8 bytes are double-encoded through a round-trip tool. Apply this as a final step every time:

   ```powershell
   $c = [System.IO.File]::ReadAllText('docs/CHANGELOG.md', [System.Text.Encoding]::UTF8)

   # Fix double-encoded em-dash: C2 A7 is Â§ mojibake of U+2014
   $c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x2014

   # Normalize any remaining C2 A7 → A7 (section sign mojibake)
   $c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x00A7

   # Collapse 3+ blank lines
   $c = $c -replace "\r?\n", "`n"
   $c = $c -replace "`n{3,}", "`n`n"
   $c = $c -replace "`n", "`r`n"

   [System.IO.File]::WriteAllText('docs/CHANGELOG.md', $c, [System.Text.Encoding]::UTF8)
   ```

   Common patterns to watch for:
   - `ΓÇö` → `—` (em-dash, U+2014)
   - `Ã─` → `—` (another em-dash variant)
   - `Â§` → `§` (section sign)
   - `Ã³` → `ó` (accented character)

### Example Command Sequence
```powershell
# Check latest tags with version-aware sorting.
$tags = git tag -l --sort=-version:refname | Select-Object -First 10

# Check current version.
$version = Select-String -Path "Directory.Build.props" -Pattern '<Version>(.*)</Version>' | ForEach-Object { $_.Matches.Groups[1].Value }

# Preview default current-unreleased changelog section.
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1

# Apply an explicit current-unreleased range.
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1 -FromTag v0.21.2 -Version $version -Apply
```

### Encoding-Safe Multi-Line Block Replacement

**Why**: exact-match replacement tools can fail when CHANGELOG.md contains multi-byte UTF-8 sequences that were double-encoded during a tool round trip. Use PowerShell `[System.IO.File]` plus `Regex` instead; it reads raw bytes and avoids exact-literal matching against corrupted text.

**Pattern** (replace all prerelease sections between two stable headings with new consolidated content):

```powershell
$cl = 'docs/CHANGELOG.md'
$c  = [System.IO.File]::ReadAllText($cl, [System.Text.Encoding]::UTF8)

$newSection = @'
## v0.X.Y

### Features
- ...

### Fixes
- ...

'@

# (?s) = dotall (. matches newlines); match from first prerelease heading up to (but not including) the previous stable heading
$c = [System.Text.RegularExpressions.Regex]::Replace(
    $c,
    '(?s)## v0\.FIRST_PRERELEASE.*?(?=## v0\.PREV_STABLE)',
    $newSection
)

# Normalize the double-encoded section-sign artifact (C2 A7 → A7).
$c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x00A7

# Collapse 3+ consecutive blank lines down to 2
$c = $c -replace "\r\n", "`n"
$c = $c -replace "`n{3,}", "`n`n"
$c = $c -replace "`n", "`r`n"   # restore CRLF if the repo uses it

[System.IO.File]::WriteAllText($cl, $c, [System.Text.Encoding]::UTF8)
```

**Key points**:
- `(?s)` makes `.` match newlines so the pattern spans the whole block.
- The lookahead `(?=## v0\.PREV_STABLE)` stops the match at the previous stable heading; it is **not** consumed.
- The mojibake normalization pass (`[char]0x00C2 + [char]0x00A7` to `[char]0x00A7`) should always run after a regex write to guard against double-encoding.
- Blank-line normalization (`\n{3,}` to `\n\n`) prevents the file from accumulating excess whitespace after sections are removed.
