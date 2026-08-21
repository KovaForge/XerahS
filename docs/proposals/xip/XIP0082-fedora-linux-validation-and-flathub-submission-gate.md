# XIP0082: Fedora Linux Validation and Flathub Submission Gate

**Status**: Proposed
**Priority**: High
**Area**: Linux | Packaging | Flathub | QA | Provenance
**Related**: XIP0075 (complete), XIP0079 (Linux improvement plan), XIP0080 (direct evdev hotkeys), PR #231 (Flatpak packaging), `docs/linux/flathub-submission-checklist.md`, `docs/linux/flatpak-permissions.md`, `docs/linux/flatpak-vm-validation.md`, `docs/linux/xdg-storage.md`
**Authors**: McoreD, Aoife Brennan
**Created**: 2026-08-01

**Trigger comment** (Discord, project channel):

> It doesn't litter your home directory and follows the XDG Directory spec, as any program on Linux should. I daily drive Linux. I do fear that XerahS might struggle to be accepted onto Flathub due to its agentic coding practices.

**Decision in this thread**: Fedora Workstation (current stable, SELinux enforcing, Flatpak-first distro) is the primary "stubborn + popular" acceptance gate for Linux + Flathub readiness. Ubuntu stays the documentation/dev target; Fedora is the holding-the-line gate. Arch is the tertiary smoke target matching XIP0079's matrix. NixOS is intentionally out of scope (separate packaging workstream).

---

## Summary

XIP0075 closed the Linux + Flathub *readiness* design. XIP0079 shipped the P1–P5 backlog. XIP0080 is rewriting global hotkeys over direct evdev. The supporting runbook, permission rationale, and submission checklist already live in `docs/linux/` with real Fedora 44 ARM64 evidence captured 2026-05-11.

This XIP is the **acceptance gate** that connects that finished work to the actual Flathub submission. It does not redesign packaging, XDG paths, or portal UX. It adds:

1. A scripted Fedora QA harness that automates the existing runbook and adds a SELinux denial scan.
2. Verification of the XIP0080 hotkey path on native Fedora (the Flatpak path stays portal-only).
3. Two regression guards: a FHS-write grep and a Flatpak permission allow-list diff.
4. Closing the remaining Pending rows of `docs/linux/flathub-submission-checklist.md` via a human-led, sign-off-anchored gate.
5. A provenance log so the trigger comment's "agentic coding" concern is answered with evidence, not a paragraph.

---

## Why Fedora (not NixOS, not Ubuntu)

The user asked for the most stubborn environment *and* popular. Fedora wins on both axes for the submission path:

- **Stubborn by default.** SELinux is enforcing out of the box. Any process that writes outside its declared paths, escalates implicitly, or talks to a Unix socket the sandbox does not declare is denied and logged. This is the closest analog to the Flathub review posture we can ship on a popular distro.
- **Flatpak-shaped.** Fedora Workstation ships GNOME with `flatpak` preinstalled and the Flathub remote configured. The first thing a Fedora user does when they want XerahS is open GNOME Software and look for it. That is the surface the Flathub reviewer evaluates against.
- **Upstream close.** Fedora packages `xdg-desktop-portal`, `xdg-desktop-portal-gtk`, `xdg-desktop-portal-kde`, `pipewire`, and the major compositors first. Portal regressions surface here earlier than in Ubuntu LTS.
- **Still popular.** Fedora Workstation is the most-used RPM-family desktop and the second target in our release notes after Ubuntu.

Ubuntu is the **second** validation target and the **primary** install-doc audience (XIP0079 P5). Fedora is where the submission gates live.

NixOS is the most stubborn environment by design; it is intentionally out of scope — NixOS packaging is a separate workstream that does not block Flathub submission. Arch is the third target for manual smoke tests, matching XIP0079's matrix.

---

## Current state (evidence-based)

The leap from XIP0075 readiness to Flathub submission already has substantial groundwork:

| Artifact | Path | Status |
|---|---|---|
| XDG storage spec + smoke test | `docs/linux/xdg-storage.md` | Shipped (XIP0075) |
| Flatpak manifest | `flatpak/com.xerahs.XerahS.yml` | Shipped (PR #231) |
| Flatpak permission review | `docs/linux/flatpak-permissions.md` | Shipped (XIP0075) |
| Flatpak VM validation runbook | `docs/linux/flatpak-vm-validation.md` | Shipped (XIP0075), Fedora-first |
| Flathub submission checklist | `docs/linux/flathub-submission-checklist.md` | Shipped, partial evidence (Fedora 44 ARM64, 2026-05-11) |
| Portal behavior doc | `docs/linux/portal-behavior.md` | Shipped (XIP0075) |
| Evdev global hotkeys (native) | `src/platform/XerahS.Platform.Linux/Input/Evdev/` | In progress (XIP0080) |

What is **not** yet in place:

| Gap | Why it matters |
|---|---|
| Scripted Fedora harness (the runbook is human-readable docs, not a script) | The checklist is currently filled by humans running steps ad hoc. Drift is easy. |
| SELinux denial scan | Fedora's added value over Ubuntu is the SELinux posture; the runbook does not exercise it. |
| FHS-write regression guard | A new contributor can reintroduce `~/.xerahs` writes and weaken XIP0075. |
| Flatpak permission diff guard | A new feature can add a host grant that Flathub review will reject. |
| XIP0080 hotkey path verification on Fedora | The new evdev backend is post-XIP0075 and not covered by the existing matrix. |
| Closing the Pending rows of the checklist | Human maintainer, KDE Plasma Wayland smoke, source-checksum human review, AI disclosure row. |
| Provenance log | The trigger comment's "agentic coding" concern is real and worth a permanent, auditable record. |

---

## Goals

1. Make the existing `docs/linux/` runbook executable by a single script.
2. Add a SELinux denial scan to the harness so Fedora's posture is actually exercised.
3. Verify the XIP0080 hotkey path on a native Fedora build (portal path remains the Flatpak path).
4. Add two regression guards so the success of XIP0075 cannot be silently rolled back.
5. Close the remaining Pending rows of `docs/linux/flathub-submission-checklist.md` via a human-led, sign-off-anchored gate.
6. Maintain a provenance log so the Flathub submission is auditable against the trigger comment's concern.

## Non-Goals

- Do not redesign XDG path handling. XIP0075 is closed.
- Do not redesign the Flatpak manifest. PR #231 is the basis.
- Do not have an AI agent open, comment on, or request review on the Flathub submission PR. `docs/linux/flathub-submission-checklist.md` "Submission PR will not be opened by AI/agent tooling" row is the rule; this XIP enforces it.
- Do not author new docs in `docs/linux/`. The runbook, permissions, checklist, and XDG storage doc already exist.
- Do not add a NixOS or Arch baseline. Those are separate, downstream workstreams.
- Do not change upstream-facing packaging conventions (icon names, `.desktop` IDs, appstream metadata) without a separate XIP.

---

## Proposed Work

### Workstream A — Fedora QA Harness (script)

A single script that runs on a clean Fedora Workstation VM (or KVM/libvirt disposable) and produces a pass/fail report per axis. The script automates the existing `docs/linux/flatpak-vm-validation.md` runbook and the XDG smoke test from `docs/linux/xdg-storage.md`, then adds a SELinux scan.

#### A.1 New script

Add `scripts/linux/fedora-qa.sh` (sibling to the existing `build/linux/` packaging scripts). It must:

- capture `getenforce` output and refuse to proceed if SELinux is `Permissive` or `Disabled` (the gate is meaningless otherwise);
- capture the active portal backend (`/usr/libexec/xdg-desktop-portal` + `xdg-desktop-portal-gtk` or `-kde`);
- record distro / arch / session type / desktop environment to `$XDG_STATE_HOME/xerahs-qa/<timestamp>/report.json`;
- invoke the existing temporary-home XDG smoke test from `docs/linux/xdg-storage.md` §"Temporary-Home Smoke Test" and assert no top-level non-XDG entries;
- run the Flatpak build per `docs/linux/flatpak-vm-validation.md` §6–§9, calling `flatpak-builder-lint` on both manifest and repo;
- run the Flatpak smoke checks (`flatpak-vm-validation.md` §8) including the home-litter find from §8;
- run the SELinux denial scan (A.2).

#### A.2 SELinux denial scan

After the smoke test, run:

```bash
sudo ausearch -m avc,user_avc -ts recent -c xerahs 2>/dev/null
sudo journalctl -k -t audit --since "$HARNESS_START" | grep -E 'avc:.*xerahs' || true
```

Any denial is a blocker. The expected denials (none currently known) are documented in the runbook; denials against XDG `user_home_t` paths or unlabeled files at `$HOME`'s top level are release-blocking.

#### A.3 Hotkey path verification

Two paths, both must be exercised:

1. **Portal path (Flatpak).** Install the Flatpak, run `flatpak run com.xerahs.XerahS`, capture `IHotkeyService.GetDiagnostics()` state from the in-app diagnostics page (added by XIP0079 P1). Expected on GNOME Wayland: `PortalBound` with `UserFacingWarning = null`.
2. **Native evdev path (XIP0080).** Install the native Fedora package (rpm from `build/linux/package-linux.sh`), confirm `xerahs doctor --linux-input` reports a healthy state, run the XIP0080 verification matrix on GNOME Wayland (per its own §Success Criteria). Confirm PrintScreen, region capture, full-screen work while unfocused.

#### A.4 Output

The script produces a JSON report and a Markdown summary. The Markdown summary is what gets checked into `docs/linux/` evidence under `docs/linux/qa-reports/fedora-<distro>-<date>.md`. The script never edits the checklist or provenance log directly — those are human-led appends.

**Deliverable**: `scripts/linux/fedora-qa.sh` + `scripts/linux/fedora-qa-report.md` template. No new doc in `docs/linux/`; the script slots into the existing `docs/linux/` family.

### Workstream B — Regression Guards

Two lightweight, locally-runnable scripts that any developer can run before opening a PR. They are not CI; they are the cheapest possible insurance against the trigger comment becoming true again.

#### B.1 FHS-write regression guard

`scripts/linux/check-fhs-writes.sh`: grep the source tree for hardcoded `~/.xerahs`, `~/XerahS`, `~/.XerahS`, `~/ShareX`, `~/Screenshots`, and similar patterns. Exits non-zero on match. Allow-list file `scripts/linux/check-fhs-writes.allowlist` for known false positives (e.g., the comments in `docs/linux/xdg-storage.md` that name these paths to say they must not appear).

Triage guideline: a hit is a regression unless the allowlist justifies it with a file path and a one-line reason.

#### B.2 Flatpak permission diff guard

`scripts/linux/check-flatpak-permissions.sh`: parses `flatpak/com.xerahs.XerahS.yml`, diffs the `finish-args` against the allow-list documented in `docs/linux/flatpak-permissions.md`. Exits non-zero on a new permission. If a new permission is intentional, the developer writes a `docs/linux/flatpak-permissions.md` entry for it *before* merging the manifest change.

This is the same shape of policy that the audit asked for in the trigger comment: every broad permission is justified.

### Workstream C — Close the Pending Checklist Rows

Five rows remain Pending in `docs/linux/flathub-submission-checklist.md` after the 2026-05-11 Fedora 44 ARM64 run:

- KDE Plasma Wayland smoke test
- Named human maintainer
- Release tarball/source checksum verified (sources for `v0.22.256` are listed; a human reviews and signs)
- PR description written/reviewed by human
- Submission PR will not be opened by AI/agent tooling

Each row is closed by a human action, not by an agent. This XIP does not perform those actions; it makes the path explicit.

#### C.1 KDE Plasma Wayland smoke

Run the Fedora QA harness on Fedora KDE Spin Wayland (or a Fedora GNOME host with a `plasma-wayland` session). The Fedora-Spin path is preferred because it exercises the `xdg-desktop-portal-kde` backend on a real KDE session. Record evidence in the QA report.

If the KDE run is blocked by infra, it is a documented deferral with a follow-up issue linked, not a silent skip. The checklist accepts that with the "Pending" status preserved and a footnote.

#### C.2 Human maintainer row

Add to the script-generated Markdown report a "Maintainer" section. The named human maintainer:

- edits the checklist's "Human reviewer" column with their name and the date;
- runs the harness personally and signs the report's "Maintainer approval" line;
- is the only person authorized to open the Flathub submission PR.

This is the operational form of the existing checklist's "Required Statement Before Submission" block.

#### C.3 Source checksum review

The 2026-05-11 checklist entry already lists the SHA-256 hashes for the manifest, npm sources, and NuGet sources at tag `v0.22.256`. A human maintainer verifies these against the published GitHub release artifacts and signs the row.

#### C.4 PR description and PR ownership

The checklist's "Human PR Draft Notes" section summarizes the PR content. The human maintainer adapts these notes into a PR description, opens the PR manually, and ensures no AI agent is subscribed to its review notifications.

### Workstream D — Provenance Log

The trigger comment specifically called out agentic coding practices. The honest answer is to maintain a record.

#### D.1 New log file

Add `docs/linux/flathub-submission-log.md` (sibling to the existing checklist). For every commit touching the Flathub-facing surface — `flatpak/com.xerahs.XerahS.yml`, `com.xerahs.XerahS.desktop`, `com.xerahs.XerahS.metainfo.xml`, the 512×512 icon, the `--finish-args` allow-list, the `flatpak-vm-validation.md` runbook, the `flathub-submission-checklist.md`, the `flatpak-permissions.md` — the human maintainer appends:

```
- <commit-sha> | <author> | <human-reviewer> | <review-evidence> | <summary>
```

`author` distinguishes "human" from "AI-assisted". `human-reviewer` is a separate person or recorded review (PR comment URL, chat excerpt, or signature). The log is **append-only**; rewrites are forbidden so the audit trail is durable.

#### D.2 AI disclosure policy

If a reviewer asks how XerahS was developed, the human maintainer:

- discloses that AI-assisted contributions exist;
- emphasizes human review, moderation, and authorship on the Flathub-facing surface;
- references this XIP and the log as the audit trail.

The "Submission PR will not be opened by AI/agent tooling" row of the checklist is the policy hook; the log is its evidence.

#### D.3 Known blocker to surface

The manifest still declares `--own-name=org.kde.*` (manifest line 36). `docs/linux/flatpak-permissions.md` already flags this as a Flathub linter blocker (`finish-args-own-name-wildcard-org.kde`). The Stage-4 decision is binary: drop the permission (loses the tray icon on KDE/XFCE) or negotiate an exception. The maintainer records the chosen resolution in the log before opening the submission PR. This XIP does not make the decision; it ensures the decision is recorded, not buried.

---

## Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Fedora-specific SELinux denial causes a regression in capture | Medium | High | A.2 SELinux scan + Fedora-first gate before any Flathub submission |
| Flathub rejection based on agentic-coding provenance | Medium | High | C.2–C.4 human-led runbook + D provenance log |
| New contributor adds a `~/.xerahs` shortcut and undoes XIP0075 | Medium | High | B.1 FHS-write grep guard |
| Flatpak manifest permissions drift after a new feature | Medium | High | B.2 permission diff guard |
| `--own-name=org.kde.*` linter blocker unresolved at submission time | High | High | D.3 explicit decision recorded before opening PR |
| KDE Plasma Wayland smoke test deferred due to infra | Medium | Medium | C.1 documented deferral with linked follow-up, not silent skip |
| Fedora lead time vs next release | Medium | Medium | Gate sits before the next Flathub submission window, not before every release |
| Harness script drift vs runbook | Medium | Medium | A.1 script *automates* the existing runbook; the runbook is the source of truth, the script is checked into it |

---

## Implementation Plan

### Stage 1 — Harness

- Add `scripts/linux/fedora-qa.sh` and `scripts/linux/fedora-qa-report.md`.
- Run on a fresh Fedora Workstation VM matching the existing runbook (Fedora 41+ GNOME Wayland). Record results.

### Stage 2 — Guards

- Add `scripts/linux/check-fhs-writes.sh` and its allow-list.
- Add `scripts/linux/check-flatpak-permissions.sh`.
- Wire into the existing developer-checklist pattern from XIP0079 P5.

### Stage 3 — XIP0080 hotkey verification

- Run the harness's hotkey path on native Fedora (evdev) and Flatpak Fedora (portal).
- Record evidence in the QA report.

### Stage 4 — Resolve `--own-name=org.kde.*`

- Human maintainer picks: drop tray, or carry an upstream Avalonia fix, or negotiate an exception.
- Update the manifest and `docs/linux/flatpak-permissions.md` together.
- Append an entry to `docs/linux/flathub-submission-log.md`.

### Stage 5 — Close the checklist

- Human maintainer signs the Fedora GNOME smoke report.
- KDE Plasma Wayland smoke run (or documented deferral).
- Source checksum review.
- PR description drafted by human from the existing "Human PR Draft Notes".

### Stage 6 — Human submission

- Human maintainer opens the Flathub PR manually.
- PR description references this XIP, the checklist, and the provenance log.
- No AI agent is subscribed to the PR's review notifications.

---

## Suggested Test Commands

```bash
# Stage 1 entrypoint
./scripts/linux/fedora-qa.sh

# XDG no-home-litter (with SELinux context)
TMP_HOME="$(mktemp -d)"
HOME="$TMP_HOME" XDG_CONFIG_HOME="$TMP_HOME/.config" \
  XDG_DATA_HOME="$TMP_HOME/.local/share" \
  XDG_STATE_HOME="$TMP_HOME/.local/state" \
  XDG_CACHE_HOME="$TMP_HOME/.cache" \
  ./scripts/linux/run-xerahs.sh --version
find "$TMP_HOME" -maxdepth 1 -mindepth 1 -print
ls -laZ "$TMP_HOME"

# SELinux denials (harness includes this; standalone shown for inspection)
getenforce
sudo ausearch -m avc -ts recent -c xerahs 2>/dev/null
sudo journalctl -k -t audit --since "1 hour ago" | grep -E 'avc:.*xerahs' || echo "no denials"

# Flatpak linter (already in runbook §6 / §9)
flatpak run --command=flatpak-builder-lint org.flatpak.Builder manifest flatpak/com.xerahs.XerahS.yml
flatpak run --command=flatpak-builder-lint org.flatpak.Builder repo repo

# Regression guards
./scripts/linux/check-fhs-writes.sh
./scripts/linux/check-flatpak-permissions.sh
```

---

## Open Questions

1. Who is the named human maintainer for the Flathub submission? (Required before Stage 5.)
2. Is Fedora KDE Spin coverage release-blocking for Flathub submission, or post-submission work? (Default: post-submission, with explicit deferral in the checklist.)
3. Does the project want a public-facing statement about AI-assisted development on the Flathub listing page itself, or only in the PR description? (Default: in the PR description and `docs/linux/flathub-submission-log.md`; not on the Flathub listing.)
4. Does `scripts/linux/check-fhs-writes.sh` belong in pre-commit, in CI, or only in the developer-checklist? (Default: optional, zero-friction, developer runs manually.)
5. What is the cadence for re-running the Fedora harness: every release, every minor release, or every Flathub-relevant change? (Default: every minor release + before any Flathub PR.)
6. The `--own-name=org.kde.*` decision (Stage 4) is a separate maintainer call. Should this XIP block on it, or close its own gate and let the maintainer resolve the blocker when they open the PR? (Default: the latter — record the decision in the log, do not gate the XIP on it.)

---

## Definition of Done

XIP0082 is complete when:

- `scripts/linux/fedora-qa.sh` runs end-to-end on a clean Fedora 41+ Workstation VM and produces a passing report.
- The XDG no-home-litter smoke test passes with SELinux enforcing and no unlabeled files at `$HOME`'s top level.
- SELinux denial scan is clean (or all denials are documented and non-blocking).
- Flatpak builds and lints on Fedora with no warnings.
- Capture smoke (region, full-screen, cancel) passes on Fedora GNOME Wayland.
- Hotkey smoke passes on Fedora GNOME Wayland for both the portal path (Flatpak) and the XIP0080 native evdev path (`IHotkeyService.GetDiagnostics()` returns a healthy state in both).
- KDE Plasma Wayland smoke passes, or is a documented deferral with a linked follow-up issue.
- `scripts/linux/check-fhs-writes.sh` and `scripts/linux/check-flatpak-permissions.sh` exist and exit non-zero on the regression they guard.
- The Pending rows of `docs/linux/flathub-submission-checklist.md` are closed or explicitly deferred with linked follow-up.
- `docs/linux/flathub-submission-log.md` exists with provenance entries for every manifest-changing commit since XIP0075 closed.
- The `--own-name=org.kde.*` decision is recorded in the log.
- A human maintainer has signed off on the Fedora validation results and the audit trail.
- The first Flathub submission PR (or the next one) is opened by a human maintainer, not by an AI agent.

---

## References

- XIP0075 — Linux XDG + Flathub Readiness (complete): `docs/proposals/xip/XIP0075-linux-xdg-flathub-readiness.md`
- XIP0079 — Linux Improvement Plan (P1–P5 shipped): `docs/proposals/xip/XIP0079-linux-improvement-plan.md`
- XIP0080 — Direct evdev global hotkeys (in progress): `docs/proposals/xip/XIP0080-linux-global-hotkeys-direct-evdev-listener.md`
- Existing runbook, permission review, and checklist: `docs/linux/flatpak-vm-validation.md`, `docs/linux/flatpak-permissions.md`, `docs/linux/flathub-submission-checklist.md`, `docs/linux/xdg-storage.md`
- Flatpak manifest: `flatpak/com.xerahs.XerahS.yml`
- XDG Base Directory Specification: <https://specifications.freedesktop.org/basedir-spec/latest/>
- Flatpak Sandbox Permissions: <https://docs.flatpak.org/en/latest/sandbox-permissions.html>
- Flathub App Requirements: <https://docs.flathub.org/docs/for-app-authors/requirements>
- Flathub Submission Process: <https://docs.flathub.org/docs/for-app-authors/submission>
- Trigger discussion: Discord `#xerahs` thread on Linux reception, 2026-08-01
