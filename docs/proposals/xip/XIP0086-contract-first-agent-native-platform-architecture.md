# XIP0086 Contract-First Agent-Native Platform Architecture

**Status**: Proposed - Full Functional Parity Program
**Created**: 2026-08-30
**Updated**: 2026-08-30
**Version**: v0.28.0
**Area**: Architecture | Agentic Development | Windows | macOS | Linux
**Related**: XIP0013, XIP0014, XIP0019, XIP0052, XIP0063, XIP0064, XIP0068, XIP0075, XIP0078, XIP0079, XIP0082, XIP0084
**Implementation repository**: [BriarForge/XerahS](https://github.com/BriarForge/XerahS)
**Reference baseline**: [KovaForge/XerahS](https://github.com/KovaForge/XerahS) commit `5c7e36dea77ab131fe0f5e2101e5d578ccde0306` (`v0.29.0`)
**Canonical proposal**: [BXIP001](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/README.md)

## 1. Decision requested

Approve a greenfield, contract-first implementation of XerahS in BriarForge/XerahS that:

1. reaches full desktop functional parity with the pinned KovaForge XerahS baseline;
2. implements independent, platform-native applications for Windows, macOS, and Linux;
3. uses a versioned plain-English Product Contract, executable conformance evidence, and hierarchical agent governance as the shared product layer; and
4. continues from architecture qualification through full delivery without requiring a second architecture proposal.

The detailed and implementation-binding specification is BXIP001 in the BriarForge repository. This XIP is the ShareX-side authorization, scope boundary, and durable pointer to that proposal. It intentionally does not duplicate the complete BXIP.

## 2. Repository roles

| Repository | Role | May contain the greenfield implementation? |
|---|---|---:|
| [BriarForge/XerahS](https://github.com/BriarForge/XerahS) | Canonical Product Contract, governance, native source trees, conformance system, fixtures, packaging, and releases | Yes; this is the sole implementation repository |
| [KovaForge/XerahS](https://github.com/KovaForge/XerahS) | Pinned discovery and compatibility baseline for the required product behavior | No; read-only evidence source for this program |
| [ShareX/XerahS](https://github.com/ShareX/XerahS) | Historical XIP record, existing Avalonia implementation, and supporting reference material | No; XIP0086 does not authorize greenfield work in this tree |

The expected local implementation checkout is:

`C:\Users\Public\source\repos\BriarForge\BriarForge\XerahS`

The inspected local reference checkout is:

`C:\Users\Public\source\repos\KovaForge\XerahS`

Neither local reference path nor either legacy editor submodule may be a build-time, packaging-time, or runtime dependency of the BriarForge products.

## 3. Pinned functional baseline

The initial parity target is immutable until a human approves a baseline-advance change:

| Item | Pinned value |
|---|---|
| Repository commit | `5c7e36dea77ab131fe0f5e2101e5d578ccde0306` |
| Branch observed | `develop` |
| Application version | `0.29.0` |
| ShareX.ImageEditor reference | `651b1d8de4bc1f874790870560314670cd038684` |
| ShareX.VideoEditor reference | `0482ee322a3086d5af135aa9b54a37803241cb0d` |
| Snapshot date | 2026-08-30 |

KovaForge is evidence of what must be discovered and made accountable; it is not automatically the product specification. Each observed behavior must be contracted as preserved behavior, a native equivalent, a corrected defect, an explicitly approved retirement, not applicable, or outside the desktop scope. Silent omission and permanent `Not implemented` dispositions are prohibited.

## 4. Meaning of full functional parity

Full parity is behavioral and operational, not structural. The BriarForge applications do not need to reproduce Avalonia views, .NET project boundaries, namespaces, classes, or internal implementation choices.

They must, however, provide the complete contracted outcomes reachable from the pinned desktop baseline, including:

- product shell, tray, hotkeys, menus, notifications, localization, accessibility, and theming;
- configuration, secrets, profiles, import/export, migration, and recovery;
- capture modes, region selection, scrolling capture, OCR, QR, and platform capture behavior;
- capture, upload, and after-task workflows, naming, file handling, effects, and automation;
- screen recording, GIF and media capture, audio, FFmpeg/native backends, and device selection;
- ImageEditor, annotations, effects, undo/redo, re-editable documents, export, and overlay editing;
- VideoEditor and all baseline-reachable media utilities;
- destinations, custom uploaders, accounts, history, indexing, search, and deletion behavior;
- CLI, MCP, assistant, daemon, plugin, protocol, and other supported integration surfaces;
- diagnostics, updates, recovery, security behavior, packaging, signing, and distribution; and
- compatible persisted formats and migrations required by real user data.

Experimental mobile code is inventoried so it cannot disappear accidentally, but is classified outside BXIP001. XIP0086 authorizes the Windows, macOS, and Linux desktop program.

## 5. The shared layer is the contract

The conceptual replacement for the common cross-platform UI assembly is a governed product definition:

```text
Plain-English Product Contract
        +-- schemas, state machines, fixtures, scenarios, test vectors
        +-- capability, setting, workflow, interface, and compatibility ledgers
        +-- Windows native implementation and evidence
        +-- macOS native implementation and evidence
        +-- Linux native implementation and evidence
```

Precise English states intent and user-observable behavior. Machine-readable artifacts remove ambiguity where prose is insufficient. Native code remains free to use the operating system's idioms as long as it satisfies the same contracted outcome or records an approved platform variance.

## 6. Native application and editor ownership

The platform implementations are independent native solutions:

- Windows: C#/.NET with WinUI 3 plus required Win32/WinRT capture, tray, and system integration;
- macOS: Swift and SwiftUI with narrow AppKit integration where required; and
- Linux: C++17 with Qt 6 and explicit desktop-portal, compositor, and session adapters.

ImageEditor is a first-class internal feature module in each native solution. `ShareX.ImageEditor` remains a behavior, fixture, and compatibility reference; it is not retained as a production submodule or embedded Avalonia UI.

VideoEditor and media tools follow the same rule. Each native solution owns its editor workflow and UI. License-compatible engines such as FFmpeg may sit behind platform-native adapters, but the legacy React/.NET VideoEditor checkout is not a production dependency.

Small shared non-UI components are permitted only when they have a documented boundary, preserve native ownership, and do not become a hidden cross-platform application framework.

## 7. Canonical BXIP program

The following BriarForge documents bind implementation:

| Document | Purpose |
|---|---|
| [BXIP001 index](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/README.md) | Status, scope, decision index, and review entry point |
| [Product Contract](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/03-product-contract.md) | Contract structure, normative language, and capability requirements |
| [Governance](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/04-governance.md) | Change protocol, agent responsibilities, traceability, and release coordination |
| [Conformance](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/05-conformance.md) | Evidence layers and all-platform conformance rules |
| [Architecture boundaries](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/06-architecture-boundaries.md) | Product/platform ownership and editor/media boundaries |
| [Implementation strategy](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/07-pilot.md) | Qualification, completion, non-goals, and definition of done |
| [Architecture decisions](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/08-decisions.md) | Binding technology, repository, plugin, release, identity, and editor decisions |
| [Reference baseline](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/11-reference-baseline.md) | Pinned source, exhaustive census, parity ledgers, and baseline-advance protocol |
| [Full-parity delivery](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/12-full-parity-delivery.md) | Capability domains, delivery waves, feature packets, and release gate |

If this summary conflicts with BXIP001, the approved BXIP001 text governs implementation. A later XIP may change ShareX-side policy, but it must explicitly update or supersede the relevant BXIP decision before agents act on it in BriarForge.

## 8. Required parity accounting

Before broad implementation, agents must create and maintain exhaustive, reviewable ledgers for:

1. capabilities and user outcomes;
2. settings, defaults, validation, persistence, and secrets;
3. workflows, states, triggers, ordering, cancellation, and failure behavior;
4. CLI, MCP, plugin, protocol, destination, and daemon interfaces; and
5. configuration, history, annotation, media, and other compatibility formats.

Every ledger row must link baseline evidence, a Product Contract identifier, Windows/macOS/Linux dispositions, conformance evidence, and release status. The required lifecycle is:

`Discovered -> Inventoried -> Contracted -> Implemented -> Conformant -> Release-verified`

A feature is not complete merely because three visually similar controls exist.

## 9. Delivery authorization

Approval authorizes all full-parity delivery waves defined by BXIP001:

1. governance and baseline freeze;
2. exhaustive census and ledgers;
3. the original four-capability all-platform qualification tranche;
4. product shell, configuration, security, and migration;
5. capture, workflows, recording, and automation;
6. native ImageEditor, VideoEditor, and media tools;
7. destinations, custom uploaders, plugins, and external integrations;
8. history, CLI, MCP, assistant, daemon, diagnostics, and recovery;
9. accessibility, localization, performance, packaging, signing, updates, and distribution; and
10. full-parity release-candidate verification.

The qualification tranche tests whether the architecture and process work. It does not narrow the product scope and cannot be used as a reason to defer the remaining baseline functionality.

## 10. Agent governance

BriarForge shall use a linked hierarchy of scoped `AGENTS.md` files. The root establishes constitutional rules; parent files define shared domain policy; child files narrow the context for a platform, feature, contract area, test suite, or packaging surface.

The hierarchy must be mechanically discoverable and validated. Child instructions may strengthen local requirements but may not silently contradict a parent. Each implementation change must resolve its applicable root-to-leaf instruction chain before editing and record durable architectural lessons in the appropriate governed location.

More instruction files improve control only when ownership and precedence remain unambiguous. Duplicating the same rule in many files without links or validation creates drift and is not compliant governance.

## 11. Full-parity release gate

No BriarForge release may claim parity with the pinned KovaForge baseline until:

- the census is reviewed and every in-scope item has a final disposition;
- required ledger rows have zero empty, unknown, unreviewed, or `Not implemented` states;
- every required contract identifier passes conformance on Windows, macOS, and Linux, subject only to approved intrinsic platform limitations;
- real sanitized settings, workflows, uploader data, history, annotation documents, and media fixtures migrate successfully;
- accessibility, localization, security, performance, crash recovery, update, packaging, signing, and distribution gates pass;
- production builds and releases succeed without either reference checkout or legacy editor submodule; and
- product, platform, quality, security, accessibility, and release owners sign a machine-readable parity attestation.

The existing Avalonia application remains available during development and validation. This XIP does not authorize retiring it or collapsing application identities; that requires a later product decision after the full-parity evidence exists.

## 12. Consequence

This proposal accepts greater source-code duplication in exchange for platform-native user experience and smaller platform-local change surfaces. It does not assume that AI makes consistency free. The engineering investment moves into contract quality, exhaustive traceability, evidence, instruction governance, independent verification, and coordinated releases.

The architecture succeeds only if a requested feature or bug fix becomes one governed product change with three explicit native implementation dispositions and shared proof of behavior.

## 13. Approval criteria

Approve XIP0086 only if the organization agrees that:

1. BriarForge/XerahS is the implementation authority;
2. the pinned KovaForge snapshot is the initial full-parity discovery baseline;
3. the Product Contract, not legacy code, is product truth;
4. all three native platforms and their conformance evidence are part of the definition of done;
5. ImageEditor and VideoEditor are native internal features rather than production submodules;
6. the BXIP001 decisions and full delivery program bind agents; and
7. parity must be demonstrated exhaustively before any replacement or retirement decision.

## 14. History

| Date | Change |
|---|---|
| 2026-08-30 | Proposed contract-first, agent-native greenfield architecture |
| 2026-08-30 | Moved canonical detail to split BXIP001 documents in BriarForge/XerahS |
| 2026-08-30 | Pinned KovaForge `v0.29.0` reference commit and editor submodule revisions |
| 2026-08-30 | Converted the pilot into a complete native desktop parity program with exhaustive ledgers and a zero-unresolved-item release gate |
