---
name: publish-release
description: "Orchestrate XerahS release flow in strict order: run maintenance prep first, update-changelog second (optional only if docs/CHANGELOG.md is intentionally absent), verify build, bump/commit/push/tag while syncing Chocolatey version metadata, monitor GitHub Actions every 2 minutes, ensure standard release notes content, then set pre-release by default (use explicit opt-out for stable). On failures, inspect logs, fix root cause, and retry with the next patch release."
---

# XerahS Release Bump Tag

## Overview

Use this skill to run release steps in strict order:
- Step 1: Execute maintenance prep first (`git pull --recurse-submodules` and `git submodule update --init --recursive`)
- Step 2: Run `.ai/skills/update-changelog/SKILL.md` second (optional only if `docs/CHANGELOG.md` is intentionally absent)
- Step 3: Verify build, then execute bump/commit/push/tag automation
- Step 4: Monitor the tag-triggered release workflow every 2 minutes
- Step 5: If failure occurs, inspect logs, fix issues, and retry with the next patch version
- Step 6: Ensure standard release notes block is present on the GitHub release
- Step 7: Set the successful release as pre-release by default (opt out only when intentionally publishing stable)

Repository target behavior:
- The automation is repository-agnostic. Git pushes use the local `origin` remote.
- GitHub CLI operations (`gh run`, `gh release`) resolve the target from the GitHub `origin` remote by default, for example `ShareX/XerahS` or `KovaForge/XerahS`.
- Use `--repo owner/name` to override the inferred target when needed.

Step 3 performs:
- Pre-check: Run `dotnet build src/desktop/XerahS.sln`; do not proceed if build fails.
- Prompts for `x/y/z` bump type (major/minor/patch) unless specified.
- Updates every tracked `Directory.Build.props` file that defines `<Version>`.
- Syncs `build/windows/chocolatey/xerahs.nuspec` `<version>` with the release version.
- Stages all current repo changes.
- Commits with version-prefixed message.
- Pushes current branch and creates/pushes annotated tag `vX.Y.Z`.

Step 4-5 performs:
- Find tag run for `Release Build (All Platforms)`.
- Poll run status every 120 seconds until completion.
- On failure, inspect failing job logs and identify first blocking error.
- Fix root cause in code/workflow/scripts.
- Re-run local pre-check build.
- Retry release using next patch bump, then monitor again.
- Repeat until workflow succeeds.

Step 6 performs:
- Ensures release notes always include:
  - `Change log:`
  - `https://xerahs.com/changelog.html`
  - `### macOS Troubleshooting ("App is damaged")` section with Gatekeeper `xattr -cr` guidance.
- After the release is published, the tag workflow also builds, smoke-tests, and attaches `xerahs.X.Y.Z.nupkg` to the GitHub release.
- `build/windows/chocolatey/Sync-ChocolateyPackage.ps1 -Version X.Y.Z` remains the manual recovery path for re-syncing checksums or repacking.
- Expected Windows release assets per architecture: `XerahS-X.Y.Z-win-x64.exe`, `XerahS-X.Y.Z-win-x64.msi`, `XerahS-X.Y.Z-win-arm64.exe`, `XerahS-X.Y.Z-win-arm64.msi`.

## Primary Command

From repository root:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh
```

Automated monitor + default pre-release (recommended):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --set-prerelease --bump z --yes
```

Explicit repository target example:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --repo KovaForge/XerahS --assume-changelog-done --monitor --set-prerelease --bump z --yes
```

Stable release opt-out example:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --no-prerelease --bump z --yes
```

Manual monitor (fallback, PowerShell example):

```powershell
gh run list --limit 10 --json databaseId,workflowName,headBranch,status,conclusion,url
Start-Sleep -Seconds 120
gh run view <run-id> --json status,conclusion,jobs,url
```

## Non-Interactive Examples

Patch bump, no prompts:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --bump z --yes
```

Patch bump with built-in 2-minute monitoring:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --monitor-interval 120 --bump z --yes
```

Minor bump with custom commit token/summary:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --bump y --type CI --summary "Prepare release artifacts" --yes
```

Preview only:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --bump z --dry-run --yes
```

## When bash is unavailable (e.g. Windows PowerShell)

On environments where `bash` is not in PATH, execute the sequence manually:

1. Step 1 - Maintenance
   - `git pull --recurse-submodules`
   - `git submodule update --init --recursive`

2. Step 2 - Changelog
   - Run `.ai/skills/update-changelog/SKILL.md`.
   - Skip only if `docs/CHANGELOG.md` is intentionally absent or the user confirms skip.

3. Step 3 - Bump, commit, push, tag
   - Run `dotnet build src/desktop/XerahS.sln`; abort if it fails.
   - Read current version from root `Directory.Build.props`.
   - Compute next version: patch `Z+1`, minor `Y+1.0`, major `X+1.0.0`.
    - Ensure tag `v<new-version>` does not exist locally or on `origin`.
    - PowerShell-safe local check (avoid false positives from `if (git rev-parse <tag>)`):
       - `git show-ref --verify --quiet "refs/tags/v<new-version>"`
       - `if ($LASTEXITCODE -eq 0) { throw "Local tag exists" }`
    - PowerShell-safe remote check:
       - `git ls-remote --exit-code --tags origin "refs/tags/v<new-version>" *> $null`
       - `if ($LASTEXITCODE -eq 0) { throw "Remote tag exists" }`
    - For `--no-bump`: if `v<current-version>` already exists, do not try to recreate it. Use `--no-tag` for commit-only flow, or bump patch for a new tag.
   - Update all tracked `Directory.Build.props` files containing `<Version>`.
   - Update `build/windows/chocolatey/xerahs.nuspec` `<version>` to match.
   - `git add -A` -> `git commit -m "[v<new-version>] [CI] Release v<new-version>"` -> `git push origin <current-branch>` -> `git tag -a v<new-version> -m "v<new-version>"` -> `git push origin v<new-version>`.

4. Step 4 - Monitor every 2 minutes
   - Find run: `gh run list --limit 10 --json databaseId,workflowName,headBranch,status,conclusion,url`
   - Poll: `Start-Sleep -Seconds 120`; then `gh run view <run-id> --json status,conclusion,jobs,url`

5. Step 5 - On failure, fix and retry
   - Fetch failed job logs: `gh run view <run-id> --job <job-id> --log`
   - Fix root cause in repository.
   - Re-run `dotnet build src/desktop/XerahS.sln`.
   - Repeat Step 3 with next patch version.

6. Step 6 - Ensure standard release notes content
   - Read current body: `gh release view v<new-version> --json body`
   - Append the standard changelog + macOS troubleshooting block if missing.
   - Write body: `gh release edit v<new-version> --notes-file <file>`
   - Verify all **8 Windows assets** are attached (`-win-x64.exe`, `-win-x64.msi`, `-win-arm64.exe`, `-win-arm64.msi`) plus macOS and Linux assets.

7. Step 7 - Set pre-release (default behavior)
   - `gh release edit v<new-version> --prerelease`
   - Verify: `gh release view v<new-version> --json isPrerelease,url,assets`
   - Stable opt-out: skip this step only when intentionally publishing stable.

8. Optional post-release Chocolatey maintenance
   - The tag workflow should already have produced and smoke-tested `xerahs.<new-version>.nupkg`.
   - Manual repack/re-sync: `powershell -File build/windows/chocolatey/Sync-ChocolateyPackage.ps1 -Version <new-version> -Pack`
   - Manual smoke test: `powershell -File build/windows/chocolatey/Test-ChocolateyPackage.ps1 -Version <new-version> -SourceDirectory dist\chocolatey`
   - Optionally push after review: `powershell -File build/windows/chocolatey/Sync-ChocolateyPackage.ps1 -Version <new-version> -Pack -Push -ApiKey <key>`

Default bump when unspecified: patch (`z`). Default commit type token: `CI`.

## Behavior

1. Require completion of `run-maintenance` first.
   - Script behavior: executes maintenance commands automatically unless explicitly bypassed with `--skip-maintenance` (or legacy alias `--assume-maintenance-done`).
2. Require completion of `update-changelog` second (skip only if `docs/CHANGELOG.md` is intentionally absent or user confirms).
3. Before bump, run `dotnet build src/desktop/XerahS.sln`; abort on failure.
4. Run `scripts/bump-version-commit-tag.sh` (or PowerShell/manual equivalent when bash unavailable).
5. After tag push, monitor the release workflow every 120 seconds until complete.
6. If failed, inspect logs, fix root cause, and retry with next patch version.
7. Continue retry loop until release workflow is successful.
8. Ensure standard release notes content is present on the successful release.
9. Mark successful release as pre-release by default; only skip when explicitly publishing stable.

## Guardrails

- Do not skip sequence unless user explicitly requests bypass.
- Do not skip maintenance unless user explicitly requests bypass (`--skip-maintenance`).
- Do not commit/push during maintenance/changelog steps.
- Always verify build before bump/tag.
- Always monitor workflow after tag push; do not stop at tag creation.
- Always inspect logs on failure and fix root cause before retry.
- Always ensure the standard release notes block exists on the successful release.
- Always use a new patch version for retries requiring new commits/tags.
- Abort on detached HEAD.
- Abort if version format is not `X.Y.Z`.
- Abort if matching tag already exists locally or remotely.
- In PowerShell manual flow, use `git show-ref --verify --quiet "refs/tags/<tag>"` and `git ls-remote --exit-code --tags` with `$LASTEXITCODE` checks for tag existence.
- Support `--no-push` and `--no-tag` when partial flow is needed.

## Agent usage (Cursor / Codex)

When executing this skill:
1. Run sequence: maintenance -> changelog -> build verify -> bump/commit/push/tag.
2. Use bash scripts if bash exists; otherwise use PowerShell/manual flow.
3. Default bump is patch (`z`) when unspecified.
4. Monitor tag workflow every 120 seconds until completion.
5. On failure, inspect logs, fix issue, and retry with next patch version.
6. Ensure release notes include changelog link + macOS troubleshooting block.
7. If requested, set the final successful release to pre-release.
8. Report final version, commit hash, branch push status, tag push status, run URL, and pre-release status.

Default pre-release policy: unless explicitly instructed otherwise, keep `--set-prerelease` enabled. Use `--no-prerelease` only for intentional stable publishes.

## Notes (lessons learnt)

- Windows/PowerShell: bash may be unavailable; manual fallback must be first-class.
- Windows/PowerShell: avoid `if (git rev-parse <tag>)` for local tag existence checks; use `git show-ref --verify --quiet refs/tags/<tag>` and inspect `$LASTEXITCODE`.
- Build before bump: avoid tagging broken trees.
- Changelog optional: do not block if `docs/CHANGELOG.md` is intentionally absent unless user requires it.
- Version sync: update every tracked `Directory.Build.props` with `<Version>` and sync `build/windows/chocolatey/xerahs.nuspec`.
- **Windows packaging produces 4 assets per release**: `XerahS-X.Y.Z-win-x64.exe`, `XerahS-X.Y.Z-win-x64.msi`, `XerahS-X.Y.Z-win-arm64.exe`, `XerahS-X.Y.Z-win-arm64.msi`. The EXE is built by Inno Setup; the MSI is built by WiX Toolset v4 (`build/windows/XerahS-setup.wxs`). Both are produced by `build/windows/package-windows.ps1` in the same loop iteration.
- **WiX prerequisite (CI & local)**: use a pinned pre-v7 WiX CLI, currently `dotnet tool install --global wix --version 6.0.2` + `wix extension add --global WixToolset.UI.wixext/6.0.2`. The `release-build-all-platforms.yml` workflow installs WiX automatically in the `build-windows` job. For local MSI builds: install WiX first; if not present the script emits a warning and skips MSI.
- **MSI install layout**: per-user, no UAC elevation required. Binaries → `%LocalAppData%\Programs\XerahS\`; Plugins → `%USERPROFILE%\Documents\XerahS\Plugins\`; Start Menu shortcut created automatically.
- **Winget manifest**: both `InstallerType: nullsoft` (EXE) and `InstallerType: wix` (MSI) entries must be included for each architecture when submitting to winget-pkgs. See `build/windows/winget/manifests/0.16.0/ShareX.XerahS.yaml` as the template.
- Chocolatey asset naming: `build/windows/chocolatey/tools/chocolateyInstall.ps1` resolves `XerahS-<version>-win-x64.exe` or `XerahS-<version>-win-arm64.exe` from `ChocolateyPackageVersion`, so release bumps should not hardcode installer filenames there.
- Chocolatey checksums for community publication are post-release data because GitHub release assets do not exist until after the tag workflow completes. The tag workflow now performs that sync automatically for release packaging, and `build/windows/chocolatey/Sync-ChocolateyPackage.ps1` remains the manual fallback.
- Flatpak CI setup must fail loudly when the runtime cannot be installed; use `flatpak remote-add --no-gpg-verify` for unsigned Flathub setup, not `--no-sign-verify`.
- Flatpak manifest source paths are resolved relative to the manifest directory, so staging paths outside `flatpak/` need a `../` prefix.
- Flatpak build commands install into `/app`, not `/usr`; expose launchers through `/app/bin`.
- Flatpak build commands run from the module build directory, not the repository root; add icons, desktop files, metainfo, or other repository assets as explicit manifest sources before installing them.
- Flatpak `finish-args` must use options supported by `flatpak build-finish`; clipboard read/write flags are not valid `finish-args`.
- Flatpak session bus access is expressed as `--socket=session-bus` or narrower `--talk-name=` policies, not `--bus=session`.
- Flatpak CI bundling should export `flatpak-builder` output to an explicit local repo with `--repo=...`; validate files directly or use supported Flatpak commands, not `flatpak build-info`.
- Chocolatey release metadata lookup must use the active GitHub repository (`GITHUB_REPOSITORY`, `origin`, or explicit `-Repository owner/name`), not a hardcoded upstream owner.
- Chocolatey install scripts must also generate download URLs from the active release repository; updating only nuspec metadata is not enough.
- Release reliability loop: tag push is not the end; monitor, fix, and retry until green.
