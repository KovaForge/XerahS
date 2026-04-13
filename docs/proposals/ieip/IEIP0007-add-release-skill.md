# Port publish-release skill to ShareX.ImageEditor

Port the XerahS `.ai/skills/publish-release` skill into the [ShareX.ImageEditor](file:///home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor) submodule repo so it can be published in exactly the same manner, while removing XerahS-specific concerns (Chocolatey, macOS troubleshooting block, XerahS-specific sln path). Also set the initial version to `0.1.0`.

## Proposed File Names

| File | Purpose |
|---|---|
| [.ai/skills/publish-release/SKILL.md](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/SKILL.md) | Agent instructions for the skill |
| [.ai/skills/publish-release/agents/openai.yaml](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/agents/openai.yaml) | Agent display metadata |
| [.ai/skills/publish-release/scripts/bump-version-commit-tag.sh](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/scripts/bump-version-commit-tag.sh) | Core version bump / commit / push / tag script |
| [.ai/skills/publish-release/scripts/run-release-sequence.sh](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/scripts/run-release-sequence.sh) | Orchestration script (maintenance → changelog → bump → monitor → pre-release) |
| [.ai/skills/run-maintenance/SKILL.md](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/run-maintenance/SKILL.md) | Maintenance stub (referenced by run-release-sequence.sh) |
| [.ai/skills/update-changelog/SKILL.md](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/update-changelog/SKILL.md) | Changelog stub (referenced by run-release-sequence.sh) |

All files live under [XerahS/ShareX.ImageEditor/](file:///home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor) (the submodule root).

## Proposed Changes

### 1. Version — [Directory.Build.props](file:///home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor/Directory.Build.props)

#### [MODIFY] [Directory.Build.props](file:///home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor/Directory.Build.props)

Add `<Version>0.1.0</Version>` to the root [Directory.Build.props](file:///home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor/Directory.Build.props) (the bump script reads and updates this file).

---

### 2. Skill infrastructure — [.ai/skills/](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills)

#### [NEW] [.ai/skills/publish-release/SKILL.md](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/SKILL.md)

Adapted from XerahS version. Key differences:
- Build command: `dotnet build ShareX.ImageEditor.sln` instead of `dotnet build src/desktop/XerahS.sln`
- No Chocolatey nuspec syncing step
- No macOS troubleshooting block in release notes (the step still ensures a changelog link, customized to `https://xerahs.com/changelog.html` or a placeholder if the project has its own site)
- Workflow name: `Release Build` (placeholder — to be updated when CI is added)
- Pre-release default: same (pre-release by default)

#### [NEW] [.ai/skills/publish-release/agents/openai.yaml](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/agents/openai.yaml)

Minimal display name/description adapted for ImageEditor.

#### [NEW] [.ai/skills/publish-release/scripts/bump-version-commit-tag.sh](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/scripts/bump-version-commit-tag.sh)

Adapted from XerahS version:
- Removes Chocolatey nuspec sync (`build/windows/chocolatey/xerahs.nuspec`)
- Otherwise identical logic (collects all [Directory.Build.props](file:///home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor/Directory.Build.props) with `<Version>`, bumps, commits, tags)

#### [NEW] [.ai/skills/publish-release/scripts/run-release-sequence.sh](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/scripts/run-release-sequence.sh)

Adapted from XerahS version:
- Workflow name: `Release Build` (update when real workflow is added)
- [standard_release_notes_block](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/scripts/run-release-sequence.sh#163-180) simplified: just emits a changelog placeholder — no macOS section (since ImageEditor is a library/tool that doesn't have a macOS quarantine concern)
- Path to bump script points to [.ai/skills/publish-release/scripts/bump-version-commit-tag.sh](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/publish-release/scripts/bump-version-commit-tag.sh)

#### [NEW] [.ai/skills/run-maintenance/SKILL.md](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/run-maintenance/SKILL.md)

Stub skill (same instructions as XerahS: `git pull --recurse-submodules` + `git submodule update --init --recursive`).

#### [NEW] [.ai/skills/update-changelog/SKILL.md](file:///home/Public/GitHub/ShareXteam/XerahS/.ai/skills/update-changelog/SKILL.md)

Stub skill pointing at `CHANGELOG.md` update instructions.

---

## Verification Plan

### Script dry-run
```bash
cd /home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor
bash .ai/skills/publish-release/scripts/bump-version-commit-tag.sh --bump z --dry-run --yes
```
Expected: prints `[DRY RUN]` output showing version bump from `0.1.0` → `0.1.1`, no git state changes.

### Version file check
```bash
grep -r '<Version>' /home/Public/GitHub/ShareXteam/XerahS/ShareX.ImageEditor/Directory.Build.props
```
Expected: `<Version>0.1.0</Version>`
