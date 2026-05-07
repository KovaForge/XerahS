# XIP0075 Linux XDG + Flathub Readiness

**Status**: Draft  
**Area**: Linux | Packaging | XDG | Flatpak | Flathub | Wayland | Trust & Provenance  
**Related**: XIP0044, XIP0046, XIP0051, XIP0058, XIP0059, XIP0061, XIP0063, PR #231  
**CEO Mission**: Discord thread — `#xerahs-linux` / "XerahS Linux"  
**Trigger comment**:

> It doesn't litter your home directory and follows the XDG Directory spec, as any program on Linux should. I daily drive Linux. I do fear that XerahS might struggle to be accepted onto Flathub due to its agentic coding practices.

---

## Summary

XerahS has a credible Linux path, but Linux users and Flathub reviewers will judge it on different axes than Windows users:

1. **Filesystem hygiene** — no random dotfolders or generated files in `$HOME`; use the XDG Base Directory Specification consistently.
2. **Portal-first desktop integration** — use XDG Desktop Portals for screenshots, screencasts, file access, global shortcuts, notifications, and permissioned host interactions wherever possible.
3. **Narrow Flatpak permissions** — avoid broad filesystem, D-Bus, device, session bus, or host access; justify every static permission.
4. **Wayland/KDE/GNOME variance** — document portal backend differences so expected desktop-specific UI does not look like an XerahS bug.
5. **Flathub provenance** — ensure the Flathub submission is human-authored, human-reviewed, auditable, and not presented as agent-generated or automatically opened by AI tooling.

This XIP defines the Linux + Flathub readiness work required before XerahS should be submitted to Flathub or promoted as a polished Linux desktop application.

---

## Research Findings

### 1. Direct Public XerahS Linux Signal Is Still Sparse

Searches for public Linux user commentary outside the XerahS repository produced limited independent discussion. The strongest evidence currently comes from:

- XerahS GitHub issues/XIPs around Linux, Wayland, KDE Plasma, portals, and hotkeys.
- General Linux ecosystem norms around XDG compliance and home-directory cleanliness.
- Flatpak/Flathub documentation and policy.
- The trigger comment from a Linux daily-driver, which matches broader Linux desktop expectations.

**Implication**: Treat this as an early readiness window. XerahS can still shape Linux perception before a larger audience forms a fixed opinion.

### 2. XDG Directory Compliance Is Table Stakes

The XDG Base Directory Specification defines where user-specific application data belongs:

| Purpose | Environment variable | Default |
|---------|----------------------|---------|
| User data | `$XDG_DATA_HOME` | `$HOME/.local/share` |
| Configuration | `$XDG_CONFIG_HOME` | `$HOME/.config` |
| State | `$XDG_STATE_HOME` | `$HOME/.local/state` |
| Cache | `$XDG_CACHE_HOME` | `$HOME/.cache` |
| Runtime files | `$XDG_RUNTIME_DIR` | session-managed runtime dir |

Source: <https://specifications.freedesktop.org/basedir-spec/latest/>

Linux users frequently treat home-directory litter as a quality smell. For XerahS, this means:

- no `~/XerahS`, `~/.XerahS`, `~/ShareX`, `~/Screenshots` auto-creation unless explicitly user-selected;
- config under `$XDG_CONFIG_HOME/xerahs` or a stable app-id-specific path;
- logs/state under `$XDG_STATE_HOME/xerahs`;
- cache/thumbnails/temp derived artifacts under `$XDG_CACHE_HOME/xerahs`;
- runtime sockets/locks under `$XDG_RUNTIME_DIR`;
- exports/captures only to user-selected or documented default folders.

### 3. Linux Portal Behavior Is Correct But Visibly Desktop-Specific

Issue #64 documents an Arch/KDE/Wayland case where the XDG Screenshot portal worked but looked different from the developer's expected UI.

Key finding:

> XerahS correctly uses the XDG Portal API. The visual appearance of the portal is determined by the user's desktop environment and portal backend, which is outside XerahS's control.

Source: <https://github.com/ShareX/XerahS/issues/64>

**Implication**: XerahS should document that portal UI differs across KDE, GNOME, GTK, and wlroots backends. This is expected Linux behavior, not automatically a XerahS defect.

### 4. KDE/Wayland Is The Current Stress Case

XIP0061 / issue #209 shows real KDE Plasma / Nobara Wayland stress cases:

- portal selector routing differences;
- `ConfigureShortcuts` missing on KDE portal;
- rapid hotkey debounce issues;
- D-Bus `ObjectDisposedException` guards;
- portal version dependencies, especially `xdg-desktop-portal-kde >= 6.4.2`.

Source: <https://github.com/ShareX/XerahS/issues/209>

**Implication**: Linux readiness cannot be declared from one GNOME or X11 test. Minimum coverage must include KDE Plasma Wayland and GNOME Wayland.

### 5. Flatpak Sandboxing Rewards Portal-First Design

Flatpak defaults are deliberately restrictive:

- no access to host files except the runtime, app files, `~/.var/app/$FLATPAK_ID`, and `$XDG_RUNTIME_DIR/app/$FLATPAK_ID`;
- no network by default;
- no arbitrary device nodes;
- limited D-Bus;
- no host services like X11, system D-Bus, or PulseAudio unless granted.

Flatpak documentation recommends portals over blanket permissions and says static filesystem access should be limited as much as possible.

Source: <https://docs.flatpak.org/en/latest/sandbox-permissions.html>

**Implication**: XerahS must be designed so the Flatpak build works with narrow, reviewable permissions. Any broad permission is a Flathub review risk.

### 6. Flathub AI / Agentic Provenance Policy Is A Real Risk

Flathub requirements currently state:

- submission PRs must not be generated, opened, or automated using AI tools or agents;
- submitters should not request review from AI tools in the submission PR;
- submissions or changes where most code is written by or using AI without meaningful human input, review, justification, or moderation are not allowed;
- low-quality AI-generated or AI-assisted code is not allowed;
- such submissions can be rejected without further review.

Source: <https://docs.flathub.org/docs/for-app-authors/requirements>

**Important distinction**: XerahS is not automatically disqualified because agents helped develop parts of it. The risk is a submission or codebase that appears unreviewed, low-quality, automated, or lacking meaningful human moderation.

**Implication**: The Flathub submission must be intentionally human-led, with clear authorship, review evidence, conventional packaging, and no agent-opened PR.

---

## Problem Statement

XerahS wants to be accepted as a serious Linux desktop capture tool. To get there, it must satisfy two audiences:

1. **Linux users**, who expect native behavior, clean filesystem usage, Wayland correctness, portal support, and no home-directory litter.
2. **Flathub reviewers**, who expect a high-quality sandboxed graphical desktop application with narrow permissions, clear maintainability, and trustworthy provenance.

Current Linux work has solved important portal and KDE issues, but there is not yet a single readiness checklist covering XDG paths, Flatpak permissions, Flathub policy, portal UX documentation, and agentic-development provenance.

---

## Goals

- Make XerahS XDG-compliant on Linux by default.
- Make Flatpak behavior match native Linux expectations without broad host access.
- Reduce Flathub review risk before submission.
- Preserve agent-assisted development internally while ensuring Flathub-facing work is human-reviewed and policy-compliant.
- Produce testable Linux readiness gates for CI/manual release validation.
- Document expected portal differences across desktop environments.

## Non-Goals

- Do not bypass Flatpak sandboxing to mimic unrestricted native behavior.
- Do not request broad `$HOME`, host, session bus, or system bus access unless a human-reviewed justification exists.
- Do not submit to Flathub automatically.
- Do not use an AI agent to open or automate the Flathub submission PR.
- Do not hide agent assistance if reviewers explicitly ask about development process; instead, be accurate and emphasize human review/moderation.

---

## Proposed Work

### Workstream A — XDG Filesystem Hygiene Audit

Audit all Linux file writes and classify them:

| Category | Correct destination |
|----------|---------------------|
| User config | `$XDG_CONFIG_HOME/xerahs` or app-id equivalent |
| App data | `$XDG_DATA_HOME/xerahs` or app-id equivalent |
| State/logs/history | `$XDG_STATE_HOME/xerahs` |
| Cache/thumbnails/temp derived data | `$XDG_CACHE_HOME/xerahs` |
| Runtime sockets/locks | `$XDG_RUNTIME_DIR/xerahs` |
| Captures/exports | user-selected directory, portal-selected directory, or documented XDG user directory |

#### Requirements

- Centralize Linux path resolution in one service or platform abstraction.
- Respect environment overrides when set.
- Use spec defaults when variables are unset or empty.
- Ignore relative XDG environment variable values as invalid, per spec.
- Add tests for unset, empty, absolute, and relative XDG values.
- Add a Linux smoke test that runs XerahS with a temporary `$HOME` and verifies no unexpected top-level files/folders are created.

#### Acceptance Criteria

- Running a basic capture/upload/config flow creates no unexpected files directly under `$HOME`.
- Unit tests cover XDG path resolution.
- Documentation lists where XerahS stores config, cache, logs, state, and captures on Linux.

---

### Workstream B — Flatpak Manifest Permission Review

Review the Flatpak manifest introduced in PR #231 and ensure every permission is minimal and justified.

#### Permission Rules

- Prefer portals over static permissions.
- Avoid `--filesystem=home`.
- Avoid `--filesystem=host`.
- Avoid `--socket=session-bus` and `--socket=system-bus`.
- Use specific D-Bus talk/own names only where required.
- Use `--socket=wayland` plus `--socket=fallback-x11` for display where possible.
- Use network permission only if upload/update/integration features require it, and document why.
- Keep file access user-mediated through portals wherever feasible.

#### Deliverable

Create a `docs/linux/flatpak-permissions.md` document containing:

- each requested permission;
- why it is required;
- whether a portal alternative exists;
- impact if removed;
- Flathub review risk level.

#### Acceptance Criteria

- `flatpak-builder-lint manifest flatpak/com.getsharex.XerahS.yml` passes or has documented exceptions.
- `flatpak-builder-lint repo repo` passes or has documented exceptions.
- Every static permission has a written justification.

---

### Workstream C — Portal-First Capture + Shortcut Behavior

Build on XIP0044, XIP0046, XIP0051, XIP0058, XIP0059, and XIP0061.

#### Requirements

- Screenshot and screencast paths use XDG Desktop Portals where appropriate.
- Global shortcuts use the GlobalShortcuts portal on Wayland where available.
- KDE `ConfigureShortcuts` absence remains a graceful fallback, not an error.
- Portal cancellation is treated as user cancellation, not a crash.
- Portal UI differences are documented in user-facing Linux docs.

#### Documentation Additions

Create or update Linux docs with:

- GNOME Wayland behavior;
- KDE Plasma Wayland behavior;
- wlroots/Sway/Hyprland notes where known;
- why portal dialogs may look different;
- minimum recommended portal package versions;
- troubleshooting commands for `xdg-desktop-portal` backends.

#### Acceptance Criteria

- KDE Wayland capture works on a current Fedora/Nobara/Arch-class portal stack.
- GNOME Wayland capture works on a current Fedora/Ubuntu-class portal stack.
- User cancellation does not produce scary stack traces.
- Docs clearly state that portal UI is desktop-controlled.

---

### Workstream D — Flathub Provenance + Human Review Protocol

Flathub's AI policy makes the submission process itself part of the product risk.

#### Rules For Flathub Submission

- The Flathub submission PR must be opened manually by a human maintainer.
- The PR description must be written/reviewed by a human maintainer.
- Do not use AI tools to request review in the submission PR.
- Do not use AI-generated PR spam, automated review replies, or agent-authored reviewer responses.
- Keep the manifest conventional, minimal, and easy to audit.
- Keep a local human-review checklist before submission.

#### Required Evidence Before Submission

Create `docs/linux/flathub-submission-checklist.md` with:

- human maintainer responsible;
- manifest reviewed by human;
- permissions reviewed by human;
- linter output captured;
- local Flatpak build tested;
- GNOME Wayland smoke test result;
- KDE Wayland smoke test result;
- no `$HOME` litter smoke test result;
- release tarball/source checksum verification;
- statement that the PR will not be opened by an AI agent.

#### Acceptance Criteria

- Checklist exists and is complete before any Flathub submission.
- Human maintainer signs off in the checklist.
- No automation opens the Flathub submission PR.

---

### Workstream E — Linux Perception Polish

The trigger comment is valuable because it identifies perception risks early.

#### User-Facing Promises To Make True

- "XerahS follows XDG on Linux."
- "XerahS does not litter your home directory."
- "XerahS uses desktop portals where Linux expects portals."
- "XerahS works on modern Wayland desktops, with documented desktop-specific limitations."
- "XerahS Flatpak permissions are narrow and explained."

#### Deliverables

- Linux README section.
- XDG storage locations doc.
- Flatpak permissions doc.
- Portal behavior troubleshooting doc.
- Release notes callout for Linux users.

---

## Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Home-directory litter damages Linux credibility | Medium | High | XDG path audit + smoke test |
| Flathub rejects broad permissions | Medium | High | Portal-first design + permission justification |
| Flathub rejects agentic-looking submission | Medium | High | Human-authored PR + review checklist |
| KDE portal UX perceived as broken | High | Medium | Docs + desktop-specific behavior notes |
| Wayland global shortcuts inconsistent | Medium | High | Portal support + graceful fallbacks |
| Flatpak cannot support core upload/capture workflows cleanly | Medium | High | Separate native vs Flatpak capability matrix |
| Reviewers consider XerahS host-dependent | Low-Medium | High | Minimize host assumptions; document portal/runtime dependencies |

---

## Implementation Plan

### Stage 1 — Audit

- Inventory Linux filesystem writes.
- Inventory Flatpak manifest permissions.
- Inventory Linux portal usage paths.
- Produce initial capability matrix: native Linux vs Flatpak.

### Stage 2 — Fix

- Centralize XDG path resolution.
- Remove or migrate non-XDG writes.
- Tighten Flatpak permissions.
- Add graceful portal fallback handling where missing.

### Stage 3 — Test

- Unit-test XDG path resolution.
- Smoke-test temporary `$HOME` for no top-level litter.
- Build and lint Flatpak locally.
- Run GNOME Wayland smoke test.
- Run KDE Wayland smoke test.

### Stage 4 — Document

- Add Linux storage locations doc.
- Add Flatpak permissions doc.
- Add portal troubleshooting doc.
- Add Flathub submission checklist.

### Stage 5 — Human Flathub Submission Prep

- Human maintainer reviews manifest and docs.
- Human maintainer verifies linter/build/test results.
- Human maintainer opens Flathub PR manually.

---

## Suggested Test Commands

```bash
# Native temporary-home smoke test
TMP_HOME="$(mktemp -d)"
XDG_CONFIG_HOME="$TMP_HOME/.config" \
XDG_DATA_HOME="$TMP_HOME/.local/share" \
XDG_STATE_HOME="$TMP_HOME/.local/state" \
XDG_CACHE_HOME="$TMP_HOME/.cache" \
HOME="$TMP_HOME" \
./xerahs --version
find "$TMP_HOME" -maxdepth 1 -mindepth 1 -print
```

```bash
# Flatpak linter
flatpak run --command=flatpak-builder-lint org.flatpak.Builder manifest flatpak/com.getsharex.XerahS.yml
flatpak run --command=flatpak-builder-lint org.flatpak.Builder repo repo
```

```bash
# Portal backend inspection
busctl --user list | grep -E 'portal|xdg-desktop'
echo "$XDG_CURRENT_DESKTOP"
```

---

## Open Questions

1. What final Flatpak app ID should XerahS use: `com.getsharex.XerahS`, `io.github.ShareX.XerahS`, or another verified-domain ID?
2. Should upload functionality be enabled by default in Flatpak, given network permission implications?
3. Should Flatpak expose capture output folders only through portals, or provide a narrow default filesystem grant for common screenshot directories?
4. Which Linux desktops are release-blocking for v1 Linux support: GNOME, KDE, wlroots, Cinnamon, XFCE?
5. Who is the named human maintainer for the eventual Flathub submission?

---

## Definition of Done

XIP0075 is complete when:

- XerahS passes an XDG no-home-litter smoke test.
- Linux path resolution has unit coverage.
- Flatpak manifest permissions are linted and documented.
- GNOME Wayland and KDE Wayland smoke tests are documented.
- Portal UI differences are documented for users.
- Flathub submission checklist exists and is complete.
- A human maintainer has reviewed and signed off on the Flathub packaging path.
- No AI agent opens or automates the Flathub submission PR.

---

## References

- XDG Base Directory Specification: <https://specifications.freedesktop.org/basedir-spec/latest/>
- Flatpak Sandbox Permissions: <https://docs.flatpak.org/en/latest/sandbox-permissions.html>
- Flathub App Requirements: <https://docs.flathub.org/docs/for-app-authors/requirements>
- Flathub Submission Process: <https://docs.flathub.org/docs/for-app-authors/submission>
- Flathub Safety Model: <https://docs.flathub.org/blog/app-safety-layered-approach-source-to-user>
- XerahS issue #64 — XDG Portal UI differences: <https://github.com/ShareX/XerahS/issues/64>
- XerahS issue #209 — KDE Plasma / Nobara Linux portal issues: <https://github.com/ShareX/XerahS/issues/209>
- XerahS PR #231 — Flatpak packaging/release integration: <https://github.com/ShareX/XerahS/pull/231>
