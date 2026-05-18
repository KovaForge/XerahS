# Contributing to KovaForge XerahS

## Git Identity & Wrappers

All contributors must use the per-agent git wrapper for their identity when working in this repo.

### Available Wrappers

| Wrapper | Path | Identity | Push Remote |
|---------|------|----------|-------------|
| `git-mikhail` | `/Users/mike/.local/bin/git-mikhail` | Mikhail Orlov `<275563267+mikhail-orlov-kf@users.noreply.github.com>` | `mikhail` |
| `git-vladislava` | `/usr/local/bin/git-vladislava` | Vladislava Kova `<274343239+vladislava-kova-kf@users.noreply.github.com>` | `vladislava` |
| `git-aoife` | `/Users/mike/.local/bin/git-aoife` | Aoife Brennan `<276835204+aoife-brennan-bf@users.noreply.github.com>` | `aoife` |
| `git-declan` | `/Users/mike/.local/bin/git-declan` | Declan Murphy `<278305138+declan-murphy-bf@users.noreply.github.com>` | `declan` |

### Usage

```bash
# Check your identity in this repo
git-mikhail whoami

# All git operations via wrapper
git-mikhail status
git-mikhail add ...
git-mikhail commit-kf "fix: describe the change"
git-mikhail push
```

**Rule:** If a wrapper exists for your identity, use it instead of plain `git`. The wrapper ensures correct commit authorship and correct push remote on every operation.

## Commit Format

```
<type>: <short description>

<type>: feature | fix | refactor | docs | chore
```

## Build Before Push

Run `dotnet build --configuration Release` and `dotnet test --configuration Release` before pushing. Zero errors, zero warnings, all tests passing.

## Co-Authors

All commits include:
```
Co-authored-by: Michael D <McoreD@users.noreply.github.com>
Co-authored-by: vladislava-kova-kf <vladislava-kova-kf@users.noreply.github.com>
```