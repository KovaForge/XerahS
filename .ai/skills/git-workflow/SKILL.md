---
name: ShareX Workflow and Versioning
description: Canonical Git, commit/push, and Directory.Build.props versioning workflow for XerahS. Use for any task that changes version numbers or requires commit/push.
---

## Scope

This file is the single source of truth for Git and versioning rules that involve:
- Commit and push workflow
- Commit message format
- Version bump behavior
- `Directory.Build.props` updates

This supersedes the retired `docs/development/RELEASE_PROCESS.md`.

## Version Source Of Truth

1. Treat the root `Directory.Build.props` file as the working XerahS app version source of truth.
2. Before any versioned XerahS commit, compare the root version with the highest existing XerahS git tag.
3. If the root version is not strictly greater than the latest tag, bump the root version first so the branch carries the next unreleased version.
4. Never set version numbers in individual `.csproj` files.
5. When bumping version, update every tracked `Directory.Build.props` in the repository that intentionally carries the XerahS app version so values match.
6. Derived release metadata files, such as `build/windows/chocolatey/xerahs.nuspec`, must be synchronized from the root version during release automation.
7. Tagged releases also generate and smoke-test the Chocolatey `.nupkg`, so release metadata under `build/windows/chocolatey/` must stay automation-friendly.

## Version Bump Policy

1. Bug fix: increment patch only (`0.0.z` rule: keep major/minor, increase `z`).
2. New feature: increment minor and reset patch.
3. Breaking change: increment major and reset minor/patch.

## Required Pre-Commit Checks

Before committing and pushing:

```bash
git pull --recurse-submodules
git submodule update --init --recursive
dotnet build src/desktop/XerahS.sln
```

Only continue when build succeeds with 0 errors.

## Commit And Push Procedure

1. Stage changes:
```bash
git add .
```
2. Commit using:
```bash
git commit -m "[vX.Y.Z] [Type] concise description"
```
3. Push:
```bash
git push
```

## Commit Message Rules

1. Prefix XerahS app commits with the root `Directory.Build.props` `<Version>` as `[vX.Y.Z]`, but only after verifying that version is strictly greater than the highest existing XerahS git tag.
2. Never use a version prefix that is lower than or equal to the latest tag. If the latest tag is already at or above the root version, bump `Directory.Build.props` first and then commit with that bumped version.
3. Include a type token such as `[Fix]`, `[Feature]`, `[Build]`, `[Docs]`, `[Refactor]`.
4. Keep the description concise and specific.
5. Submodule-only commits in shared libraries (e.g. `ShareX.ImageEditor`): omit `[vX.Y.Z]`; use `[Type] description` per `AGENTS.md`.

## Git Hook Expectations

1. Keep `.githooks` active (`git config core.hooksPath .githooks`).
2. Do not bypass hooks with `--no-verify` unless explicitly requested for emergency use.
3. If hooks fail, fix issues and recommit.

## Documentation And Git

1. Commit Markdown documentation changes with related code/config changes.
2. Do not leave generated instruction docs uncommitted when they are part of the requested work.
