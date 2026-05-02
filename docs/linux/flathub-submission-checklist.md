# Flathub Submission Checklist

Flathub submission must be human-led. Do not use an AI tool or agent to open the Flathub pull request, request review, post reviewer replies, or automate the submission process.

| Gate | Status | Evidence / Notes | Human reviewer |
|------|--------|------------------|----------------|
| Named human maintainer responsible | Pending |  |  |
| Manifest reviewed by human | Pending | `flatpak/com.getsharex.XerahS.yml` |  |
| Permissions reviewed by human | Pending | See `docs/linux/flatpak-permissions.md` |  |
| `flatpak-builder-lint manifest` output captured | Pending |  |  |
| `flatpak-builder-lint repo` output captured | Pending |  |  |
| Local Flatpak build tested | Pending |  |  |
| GNOME Wayland smoke test passed | Pending |  |  |
| KDE Plasma Wayland smoke test passed | Pending |  |  |
| No `$HOME` litter smoke test passed | Pending | See `docs/linux/xdg-storage.md` |  |
| Release tarball/source checksum verified | Pending |  |  |
| PR description written/reviewed by human | Pending |  |  |
| Submission PR will not be opened by AI/agent tooling | Pending |  |  |

## Required Statement Before Submission

Before opening the Flathub PR, a human maintainer should fill this in:

```text
I reviewed the Flatpak manifest, permissions, linter output, local build result,
GNOME Wayland smoke test, KDE Plasma Wayland smoke test, XDG home-litter smoke
test, and source provenance. I will open and manage the Flathub submission PR
manually without AI/agent automation.

Maintainer:
Date:
```

