# XIP0086 Contract-First Agent-Native Platform Architecture

**Status**: Proposed
**Created**: 2026-08-30
**Updated**: 2026-08-30
**Version**: v0.28.0
**Area**: Architecture | Agentic Development | Windows | macOS | Linux
**Related**: XIP0013, XIP0014, XIP0019, XIP0052, XIP0063, XIP0064, XIP0068, XIP0075, XIP0078, XIP0079, XIP0082, XIP0084
**Implementation repository**: [BriarForge/XerahS](https://github.com/BriarForge/XerahS)
**Decision requested**: Approve a greenfield, contract-first XerahS implementation in which a versioned, plain-English Product Contract becomes the source of product behavior; independent native applications implement that contract on Windows, macOS, and Linux; and the architecture decisions in section 14 bind the pilot unless a later XIP supersedes them.

**Canonical split:** the working copy of this proposal is [BXIP001](https://github.com/BriarForge/XerahS/blob/main/docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/README.md) in the independent greenfield repository [BriarForge/XerahS](https://github.com/BriarForge/XerahS) (not a fork of this tree). Prefer that directory for review and edits. This file is the ShareX-side snapshot.

---

## 1. Executive Summary

Cross-platform UI frameworks historically reduced the cost of implementing and maintaining the same application on multiple operating systems. Agentic development changes that cost model: AI agents can implement the same feature independently in the native framework of each operating system with much less manual authoring effort than before.

This does not make cross-platform consistency automatic. It moves the primary engineering problem from code reuse to product governance, specification quality, conformance testing, and coordinated release management.

This XIP proposes that XerahS treat a version-controlled **Product Contract** as the conceptual replacement for the common cross-platform DLL. The Product Contract is written primarily in precise plain English so that humans and AI agents can understand it. It is strengthened by executable scenarios, schemas, state machines, fixtures, and test vectors wherever prose alone would be ambiguous.

Implementation will occur as a greenfield project in the [BriarForge/XerahS](https://github.com/BriarForge/XerahS) repository. The existing ShareX Team XerahS repository remains a behavioral reference and source of lessons, fixtures, compatibility requirements, and migration evidence; it is not the codebase from which the new native applications must inherit their structure.

Under the proposed target architecture:

1. The Product Contract defines what XerahS does.
2. Windows, macOS, and Linux implementations define how each operating system delivers that behavior natively.
3. A shared conformance system proves that the three implementations remain one product.
4. Platform-specific differences are explicit, justified, reviewable, and time-bound where appropriate.
5. A hierarchy of scoped `AGENTS.md` files communicates the applicable governance to each agent at the point of implementation.
6. The existing Avalonia application remains supported while the greenfield implementation is developed and validated. This XIP does not authorize removal of the existing application.
7. Section 14 records the pilot-binding defaults for ownership, contract format, platform baselines, repository shape, sharing, plugins, release, review, instruction hierarchy, golden-image tolerances, and the rendering-kernel evidence bar.

The proposal is therefore not "rewrite XerahS three times and trust AI." It is "specify XerahS once, implement it natively three times, and verify it continuously."

## 2. Motivation

### 2.1 Agentic development changes the reuse calculation

Shared UI code is valuable because it avoids repeating implementation work. AI agents substantially reduce the marginal cost of that repetition. A well-governed agent can read a feature contract, inspect a platform implementation, implement the feature using native APIs and conventions, add tests, and report deviations.

The potential benefits for XerahS are significant:

- Native permission, capture, recording, windowing, shortcut, notification, menu, and accessibility behavior.
- Native user experience instead of a lowest-common-denominator interaction model.
- Direct use of new operating-system capabilities without waiting for cross-platform framework support.
- Smaller platform-specific context for agents and maintainers.
- Independent platform evolution when an operating system requires a different design.

### 2.2 XerahS is unusually platform-sensitive

Many of XerahS's defining capabilities are already operating-system integrations rather than portable UI concerns:

- Screen, display, window, and region capture
- Screen recording and audio capture
- Global hotkeys and input hooks
- Clipboard and drag-and-drop behavior
- Tray, menu, notification, and startup integration
- Permissions and security prompts
- GPU, HDR, DPI, and color handling
- Wayland, X11, desktop portal, AppKit, and Win32 behavior

Avalonia can provide a consistent presentation layer, but it cannot make these capabilities identical. Existing XIPs for macOS, Linux, platform abstraction, and Windows capture parity demonstrate that the hardest work already lives at platform boundaries.

### 2.3 The current code cannot be the only specification

When one platform's implementation is treated as the product definition, other platforms become permanent ports of accidental behavior. This creates several problems:

- Windows behavior can become authoritative merely because it existed first.
- Bugs can be copied as if they were requirements.
- Agents must reverse-engineer intent from implementation details.
- Platform ports can look complete while differing in edge cases, error handling, persistence, or ordering.
- Refactoring one implementation can silently redefine the product.

The source of product truth should be independent of any UI framework, programming language, or operating system.

## 3. Proposal

### 3.1 Establish three sources of truth

XerahS SHALL distinguish three forms of truth:

| Layer | Source of truth | Responsibility |
|---|---|---|
| Product truth | Product Contract | Defines user-visible behavior and product invariants |
| Platform truth | Native implementation | Defines how an operating system realizes the contract |
| Conformance truth | Tests and evidence | Demonstrates that an implementation satisfies the contract |

No platform implementation SHALL become the product specification merely by being first or most complete.

### 3.2 Replace the conceptual common DLL with a Product Contract

The Product Contract is not a runtime binary. It is a versioned repository artifact that humans and agents use at development time.

Plain English is its primary interface because it communicates intent, context, and expected user experience across implementation languages. English SHALL be made precise with normative terms:

- **MUST** and **MUST NOT** define required behavior.
- **SHOULD** and **SHOULD NOT** define preferred behavior that may have a documented exception.
- **MAY** defines optional behavior.

The contract SHALL include machine-readable artifacts wherever exact output or state is important. Prose alone is not sufficient for serialization, naming, migration, security, ordering, concurrency, image processing, or other behavior that can be expressed deterministically.

### 3.3 Implement the contract through native platform applications

The greenfield repository will contain three independently buildable platform applications:

| Platform | Native direction | Notes |
|---|---|---|
| Windows | WinUI 3 with Windows App SDK for application chrome; Win32 and WinRT for capture overlay, DXGI, hotkeys, tray, and startup | Language: C#. OS baseline: Windows 11 23H2 and later. Decision D-WIN-001 |
| macOS | SwiftUI for chrome; AppKit for overlay, status item, and capture surfaces SwiftUI cannot host | Language: Swift. OS baseline: macOS 14 Sonoma and later. Decision D-MAC-001 |
| Linux | Qt 6 (Widgets for overlay, tray, and input; Qt Quick optional for settings chrome) plus xdg-desktop-portal | Language: C++17. Wayland first, X11 fallback. Decision D-LIN-001 |

The Product Contract SHALL remain independent of these choices. Replacing a platform framework must not require redefining product behavior. The table records pilot-binding defaults from section 14; a later XIP may change a framework after measured evidence, without rewriting contracted product outcomes.

### 3.4 Permit native adaptation without permitting silent divergence

Parity means equivalent product outcomes, not necessarily identical pixels or interaction mechanics.

Each implementation:

- MUST satisfy shared behavioral invariants.
- MUST use native accessibility, navigation, permission, and lifecycle conventions where they differ.
- MAY use different interaction mechanics when required by platform conventions.
- MUST document any material behavioral deviation.
- MUST NOT silently omit a contracted capability.

For example, a settings view may use different native controls and layout on each platform while preserving the same setting meanings, defaults, validation, persistence, and downstream effects.

### 3.5 Integrate ImageEditor into each native platform solution

ImageEditor is a defining XerahS product capability and SHALL be implemented as an internal, first-class feature module in each native platform solution. The greenfield applications SHALL NOT take a production dependency on the existing Avalonia `ShareX.ImageEditor` repository as a Git submodule, linked library, embedded UI, or required runtime process.

The shared ImageEditor asset will be its Product Contract and conformance corpus, not a common UI binary:

```text
ImageEditor Product Contract
        |
        +-- Windows native ImageEditor feature
        +-- macOS native ImageEditor feature
        +-- Linux native ImageEditor feature
        `-- Cross-platform conformance corpus
```

Each native ImageEditor SHALL live in the same repository as its native XerahS application so a behavior change can update the contract, all affected implementations, fixtures, and traceability evidence atomically.

The existing [KovaForge/ShareX.ImageEditor](https://github.com/KovaForge/ShareX.ImageEditor) repository will continue to serve ShareX and the existing Avalonia XerahS. For the greenfield project it is a legacy reference implementation and source of compatibility evidence, not a component of the target runtime architecture.

## 4. Product Contract Design

### 4.1 Proposed repository structure

The pilot SHOULD use a structure similar to:

```text
AGENTS.md

product-contract/
  AGENTS.md
  README.md
  manifest.yaml
  glossary.md
  capabilities/
    capture/
      REGION-CAPTURE-001/
        SPEC.md
        SCENARIOS.feature
        STATE_MACHINE.md
        settings.schema.json
        test-vectors.json
        fixtures/
        platforms/
          windows.md
          macos.md
          linux.md
    image-editor/
      AGENTS.md
      EDITOR-SESSION-001/
      ANNOTATION-DOCUMENT-001/
      ANNOTATION-TOOLS-001/
      IMAGE-EFFECTS-001/
      schemas/
      scenarios/
      fixtures/
      test-vectors/
      compatibility/
        xann-v1.md
  schemas/
  decisions/
  waivers/

platforms/
  AGENTS.md
  windows/
    AGENTS.md
    image-editor/
      AGENTS.md
  macos/
    AGENTS.md
    image-editor/
      AGENTS.md
  linux/
    AGENTS.md
    image-editor/
      AGENTS.md

conformance/
  AGENTS.md
  runner/
  adapters/
    windows/
    macos/
    linux/
  image-editor/
    AGENTS.md
    golden-images/
    compatibility-fixtures/
  reports/

tools/
  contract-linter/
    AGENTS.md
```

This layout is the pilot format (D-CON-001). The pilot SHALL validate it before expanding beyond the four named Phase 1 capabilities.

### 4.2 Required contents of a capability contract

Each capability contract SHALL contain:

1. Stable capability and requirement identifiers.
2. User intent and the problem being solved.
3. Definitions for domain terms.
4. Preconditions, inputs, outputs, and persisted state.
5. Normative workflow and ordering rules.
6. Failure, cancellation, retry, and recovery behavior.
7. Settings, defaults, validation, and migration rules.
8. Cross-platform invariants.
9. Permitted native adaptations.
10. Accessibility, privacy, security, and performance expectations.
11. Acceptance scenarios.
12. Deterministic schemas, fixtures, or test vectors where applicable.
13. Known platform limitations and approved deviations.
14. Compatibility expectations between contract versions.

### 4.3 Example contract excerpt

```md
# POST-CAPTURE-CLIPBOARD-001 Copy captured image to clipboard

## User intent

After completing a valid image capture, the user can have XerahS place the
captured image on the system clipboard automatically.

## Requirements

- PCC-001: When the action is enabled, XerahS MUST attempt to write the final
  captured image to the system clipboard.
- PCC-002: Clipboard failure MUST NOT discard the captured image.
- PCC-003: A failed clipboard action MUST be recorded as failed in the task
  result and MUST NOT prevent later independent actions from running.
- PCC-004: The clipboard representation MAY differ by platform, but pasting
  into the platform's standard image-capable applications MUST reproduce the
  captured image without changing its pixel dimensions.
```

The corresponding scenario can be expressed as:

```gherkin
Scenario: Clipboard failure does not stop later actions
  Given a valid image capture
  And copy-to-clipboard is enabled before save-to-file
  And the system clipboard rejects the image
  When post-capture actions execute
  Then the clipboard action is recorded as failed
  And the save-to-file action still executes
  And the captured image remains available to the workflow
```

### 4.4 Exact behavior requires exact evidence

The following categories SHOULD include machine-readable definitions or reference vectors:

- Configuration and history formats
- Filename token expansion and escaping
- URL construction and uploader requests
- Image transformations and encoders
- Workflow ordering and state transitions
- Data migrations
- Cryptographic and security behavior
- Cross-process or plugin protocols

Example:

```json
{
  "requirement": "FILENAME-COUNTER-004",
  "input": {
    "pattern": "%date%_%counter%",
    "date": "2026-08-30",
    "counter": 7,
    "counter_padding": 3,
    "extension": "png"
  },
  "expected": "2026-08-30_007.png"
}
```

Shared code MAY remain where a single exact implementation is safer or materially more economical. Such code is an implementation choice, not the definition of product behavior. The contract and conformance evidence remain authoritative.

## 5. Hierarchical AGENTS.md Governance

### 5.1 Principle

The greenfield repository SHALL use multiple `AGENTS.md` files so that an agent receives both repository-wide law and precise instructions for the directory it is changing.

The strength of this model comes from scope and enforceability, not from file count alone. Adding instructions everywhere without a hierarchy would increase context consumption, duplication, contradiction, and stale rules. A child file therefore adds local constraints; it does not restate the parent.

The intended instruction chain is:

```text
Root constitution
      |
      +-- Product Contract governance
      |
      +-- Shared platform governance
      |       +-- Windows native rules
      |       +-- macOS native rules
      |       +-- Linux native rules
      |
      +-- Independent conformance governance
      |
      +-- Governance-tooling rules
```

### 5.2 Root constitution

The root `AGENTS.md` SHALL be short, stable, and non-overridable. It defines rules that apply to every agent and every directory, including:

- The Product Contract is the source of product truth.
- Supported platforms and release parity requirements.
- Security, privacy, accessibility, licensing, and data-compatibility invariants.
- The authority and precedence model for instructions.
- Required planning, review, verification, and evidence.
- Prohibited actions, including silently weakening contracts or waiving a platform.
- The process for escalating contradictory or infeasible requirements.
- Links to each first-level child instruction scope.

Root rules SHOULD use stable identifiers, for example `ROOT-CONTRACT-001`, so CI reports and child files can refer to rules without copying their text.

### 5.3 Parent-child authority

The hierarchy SHALL follow these rules:

1. Root constitutional rules apply everywhere and cannot be weakened by a child.
2. A child `AGENTS.md` inherits all applicable ancestors automatically.
3. A child MAY add stricter or more specific rules for its directory subtree.
4. A child MUST NOT duplicate an ancestor rule; it references the ancestor's rule identifier instead.
5. A child MUST NOT contradict an ancestor. If conflict is unavoidable, work stops and the governance owner resolves it explicitly.
6. A rule that applies to sibling trees belongs in their nearest common ancestor.
7. A deeper `AGENTS.md` is created only when that subtree has a real architectural, security, tooling, or verification boundary.

Each child SHALL link to its immediate parent. Each parent SHALL maintain a child-scope index, but agents are not required to load unrelated sibling instructions. This provides navigable parent-to-child and child-to-parent relationships without recursive instruction loading.

### 5.4 Standard child-file schema

Every scoped `AGENTS.md` SHOULD use the same concise structure:

```md
# Scope
Applies to: platforms/macos/**
Parent: ../AGENTS.md

# Purpose
What this subtree owns and does not own.

# Local Rules
Stable rule IDs containing only additions to inherited rules.

# Required Workflow
Planning, implementation, and review steps for this subtree.

# Verification
Commands, test environments, and evidence required before completion.

# Prohibited Changes
Actions that are unsafe or architecturally invalid in this subtree.

# Escalation
Conditions that require a contract change, waiver, security review, or human decision.

# Child Scopes
Links to immediate child AGENTS.md files.
```

Platform-specific files would then carry only genuinely local rules. Examples include Windows packaging and API constraints, macOS entitlements and AppKit/SwiftUI boundaries, and Linux portal, Wayland, X11, toolkit, and packaging requirements.

### 5.5 Role isolation through instruction scopes

The hierarchy SHALL preserve separation of duties:

- `product-contract/AGENTS.md` governs normative language, requirement identifiers, schemas, compatibility, and product approval. It MUST prohibit changing a contract solely to satisfy an implementation.
- `platforms/AGENTS.md` governs requirements shared by all native implementations, including traceability manifests and platform parity.
- Each platform child governs native framework use, OS baselines, packaging, signing, permissions, accessibility, and platform tests.
- Each platform's `image-editor/AGENTS.md` governs native editor UI, input, rendering, persistence adapters, and verification without granting authority to redefine the editor contract.
- `conformance/AGENTS.md` governs independent verification. It MUST prohibit deriving expected results solely from one platform implementation.
- `conformance/image-editor/AGENTS.md` governs editor fixtures, tolerance policies, `.xann` compatibility, and golden-image comparison independently of all three renderers.
- `tools/contract-linter/AGENTS.md` governs tooling that validates the governance system itself.

This separation allows an agent to be highly constrained in its own scope without granting it authority over the specification or another platform.

### 5.6 Governance linting

CI SHALL validate the instruction hierarchy. At minimum, the governance linter SHALL detect:

- Broken parent or child links.
- Missing or duplicate rule identifiers.
- Child rules that attempt to override protected root rules.
- Duplicate normative text that is likely to drift.
- Directories that declare an instruction scope but are absent from the root scope index.
- Invalid verification commands or references where they can be checked statically.
- Contract or platform changes that lack the required traceability updates.

The linter SHOULD generate an effective-instructions report for any repository path. An agent and reviewer can then see exactly which root-to-leaf rules governed a change.

### 5.7 Instruction quality controls

More instructions are not automatically stronger governance. Each `AGENTS.md` SHALL be reviewed for:

- **Necessity**: the rule belongs at this scope and prevents a concrete failure.
- **Uniqueness**: the rule is defined in one authoritative location.
- **Testability**: compliance can be demonstrated where practical.
- **Currency**: commands, paths, SDK versions, and links remain valid.
- **Context cost**: the file is concise enough for an agent to apply reliably.
- **Ownership**: a named role is responsible for resolving ambiguity and maintaining the scope.

Executable architecture tests, CI gates, schemas, and conformance tests remain stronger than prose. `AGENTS.md` tells agents what must happen; repository automation proves that it happened.

## 6. Agentic Development Protocol

### 6.1 Feature workflow

Every product behavior change SHOULD follow this sequence:

1. **Contract change**: Create or update the relevant capability contract and acceptance evidence.
2. **Impact analysis**: Identify affected requirements, data formats, integrations, and platforms.
3. **Platform planning**: Produce a platform-specific implementation plan for Windows, macOS, and Linux.
4. **Native implementation**: Implement the feature independently using the platform's native framework and conventions.
5. **Conformance**: Run shared scenarios, deterministic vectors, and platform-specific integration tests.
6. **Parity review**: Compare outcomes and document any deviation.
7. **Release decision**: Release only when the parity gate passes or an authorized waiver exists.

A feature is not complete because one implementation has landed. It is complete when the contracted product behavior has an accepted disposition on every supported platform.

### 6.2 Separation of agent responsibilities

To reduce correlated mistakes, the same agent SHOULD NOT be the sole author, implementer, verifier, and approver of a material contract change.

Recommended roles are:

- **Contract agent**: turns product intent into normative requirements and examples.
- **Windows agent**: implements and tests the Windows realization.
- **macOS agent**: implements and tests the macOS realization.
- **Linux agent**: implements and tests the Linux realization.
- **Conformance agent**: reviews from the contract rather than from another platform's code.
- **Human product owner**: resolves ambiguous intent and approves durable platform differences.

Platform agents MAY inspect another implementation for interoperability or defect context, but SHOULD implement from the Product Contract. This reduces accidental copying of platform assumptions and bugs.

### 6.3 Change control

Native implementation agents MUST NOT weaken the contract merely to make an implementation pass. If a requirement is infeasible or inappropriate on a platform, the agent SHALL submit one of:

- A contract clarification that preserves the original user intent.
- A platform deviation with rationale and user impact.
- A time-bound waiver with an owner, expiry condition, and remediation plan.
- A proposal to change supported platform scope.

Contract changes that alter user-visible behavior require product review. Mechanical clarifications that do not alter behavior may use the normal documentation review path.

## 7. Conformance and Release Governance

### 7.1 Traceability manifest

Each platform SHALL publish a machine-readable mapping from contract requirements to implementation and evidence. Conceptually:

```yaml
capability: POST-CAPTURE-CLIPBOARD-001
contract_version: 1.2.0
platform: macos
requirements:
  PCC-001:
    status: implemented
    tests:
      - ClipboardActionTests.copy_valid_image
  PCC-002:
    status: implemented
    tests:
      - ClipboardActionTests.failure_preserves_capture
  PCC-004:
    status: implemented
    evidence:
      - pasteboard-preview-golden.png
```

The manifest provides coverage and traceability. It does not prove correctness by itself.

### 7.2 CI parity gate

CI SHALL produce a capability report such as:

| Check | Windows | macOS | Linux |
|---|---:|---:|---:|
| Contract version accepted | Pass | Pass | Pass |
| Required scenarios | Pass | Pass | Pass |
| Deterministic vectors | Pass | Pass | Pass |
| Native integration tests | Pass | Pass | Pass |
| Accessibility checks | Pass | Pass | Pass |
| Unexpired deviations | None | None | 1 |

The release gate SHALL fail when:

- A required platform has no disposition for a new or changed requirement.
- A deterministic conformance vector fails.
- A required scenario fails.
- A deviation or waiver has expired.
- A platform targets a contract version incompatible with the release.

### 7.3 Platform capability matrix

Not all operating systems expose the same capabilities. The contract SHALL distinguish:

- **Required**: release-blocking on this platform.
- **Equivalent**: delivered through a different native mechanism with the same product outcome.
- **Degraded**: supported with a documented limitation and user-facing explanation.
- **Unavailable**: impossible or intentionally unsupported, with product approval.
- **Not applicable**: the concept has no meaning on the platform, with rationale.

"Not implemented" is not a permanent capability category.

### 7.4 Compatibility

Contract versions SHOULD follow semantic compatibility principles:

- Patch: clarification or added evidence with no product behavior change.
- Minor: backward-compatible capability or optional behavior.
- Major: incompatible behavior, persistence, plugin, automation, or integration change.

User data, configuration, history, automation, and plugin compatibility MUST be explicitly addressed when a contract version changes.

## 8. Target Architecture Boundaries

### 8.1 Product behavior versus platform mechanism

The contract defines outcomes rather than prescribing implementation unnecessarily.

Examples:

| Product contract | Native mechanism |
|---|---|
| Register a global capture shortcut | Windows hotkey APIs; macOS native hotkey APIs; Linux portal, compositor, or approved fallback |
| Request screen-recording permission | Windows capability behavior; macOS TCC flow; Linux desktop portal flow |
| Notify the user of upload completion | Windows notification; macOS UserNotifications; Linux desktop notification |
| Present application settings | Native controls and navigation on each platform |

### 8.2 Data and integration boundaries

Independent native implementations increase the importance of stable, language-neutral formats. The Product Contract SHALL define:

- Configuration and migration schemas
- History and task result formats
- CLI behavior and exit codes
- Automation and MCP contracts
- Plugin boundaries
- Uploader definitions and network behavior
- Diagnostic and telemetry semantics

Greenfield applications SHALL NOT load in-process .NET plugins. Decision D-PLUG-001 sets an out-of-process stdio JSON-RPC direction. The handshake, sandbox, secret-passing, and package format remain a follow-up XIP. Until that XIP lands, built-in uploaders and custom HTTP uploaders are the destination floor.

### 8.3 Reference implementations are informative, not normative

During greenfield development, the Avalonia application and the original ShareX implementation are valuable behavioral references and test oracles. They SHALL NOT override an approved Product Contract or dictate the new repository structure. If code and contract disagree, the discrepancy must be resolved explicitly rather than silently copying the code.

### 8.4 ImageEditor module boundary

Each native solution SHALL expose ImageEditor to its host application through a small, platform-idiomatic internal interface with equivalent contract semantics.

Conceptually, the editor input is:

| Input | Purpose |
|---|---|
| Source image | Image to annotate or transform |
| Editing mode | Standalone editing, capture annotation, or workflow task mode |
| Initial annotation document | Optional re-editable project state |
| Editor preferences | Contract-defined defaults and native presentation preferences |
| Host capabilities | Explicitly provided save, clipboard, upload-request, pin, and diagnostic capabilities |

The editor result is:

| Output | Purpose |
|---|---|
| Disposition | Confirmed, cancelled, or failed |
| Rendered image | Final image when confirmed |
| Annotation document | Re-editable, language-neutral project state |
| Requested continuation | Save, copy, upload, pin, or return-to-workflow intent where applicable |
| Diagnostics | Structured errors, warnings, and renderer information |

The ImageEditor feature owns:

- Native editor UI, input, selection, zoom, and accessibility.
- Annotation state, layer ordering, history, undo, and redo.
- Annotation rendering and image-effect execution.
- Editor-local preferences and project-document persistence.
- Import and export of the contract-defined annotation format.

The host XerahS application owns:

- Capture orchestration and source-image acquisition.
- File destinations and naming policy.
- Clipboard, upload, pin, history, automation, and post-capture workflows.
- Permission prompts and platform capabilities outside the editor.
- Interpretation and execution of requested continuation actions.

This boundary prevents ImageEditor from becoming a second application framework inside XerahS while keeping it independently testable within each native solution.

### 8.5 Existing ShareX.ImageEditor compatibility

The current editor's `.xann` version 1 format, annotation inventory, effect behavior, history semantics, and host callbacks SHALL be investigated as migration inputs. The Product Contract SHALL define which behaviors are retained, corrected, or intentionally discontinued.

The greenfield repository SHOULD import approved schemas, fixtures, and golden images as versioned test assets with appropriate license and provenance records. It SHOULD NOT require a live checkout of the legacy repository for normal builds, tests, or releases. If the legacy implementation is temporarily used as an oracle, it MUST run only in isolated development or compatibility tooling and MUST NOT be packaged with a native application.

### 8.6 Shared rendering-kernel exception

The initial architecture SHALL implement editor state, UI, and rendering within each native solution. A shared rendering submodule or common DLL SHALL NOT be introduced by default.

Image processing is nevertheless an exactness-sensitive domain. If pilot evidence shows that independently implementing complex effects causes unacceptable pixel drift, security risk, or maintenance cost, a follow-up XIP MAY propose a small headless rendering kernel with a stable language-neutral ABI.

Any approved kernel:

- MUST contain no UI, windows, dialogs, platform services, workflow orchestration, or product policy.
- MUST remain subordinate to the ImageEditor Product Contract.
- MUST be replaceable by a conforming native implementation.
- MUST use the same conformance vectors as all native renderers.
- MUST justify its repository and dependency model independently; approval is not implicit permission to restore the existing Avalonia submodule.

### 8.7 Coexistence with the Avalonia application

The greenfield applications SHALL NOT replace or reuse the production Avalonia application identity during the pilot.

- Avalonia XerahS keeps the existing product identity (`com.xerahs.app` and current installers).
- Greenfield applications SHALL use a distinct application identifier, window title suffix, and package name, for example `com.xerahs.native` and the display name "XerahS Native".
- Greenfield applications MUST be able to import contract-defined configuration, history, and annotation documents produced by Avalonia XerahS and ShareX, but MUST NOT write over the Avalonia application's live settings without an explicit user action.
- Side-by-side installation MUST be supported for the duration of the pilot.
- Only a subsequent approved XIP may collapse the two identities or retire the Avalonia package.

## 9. Greenfield Implementation Strategy

### Phase 0: Approve principles and boundaries

- Approve the Product Contract as the future source of product truth.
- Confirm Windows, macOS, and Linux as the initial supported native targets.
- Adopt the section 14 architecture decisions as pilot-binding defaults.
- Assign the named roles in D-OWN-001.
- Establish the root and first-level `AGENTS.md` hierarchy in [BriarForge/XerahS](https://github.com/BriarForge/XerahS) using D-AGT-001.
- Record that greenfield development does not deprecate the existing Avalonia application by itself.

### Phase 1: Build a contract pilot

The pilot SHALL specify these four capability packages:

1. `FILENAME-GENERATION-001`: deterministic shared behavior: token expansion, counter padding, date formatting, and illegal-character handling.
2. `POST-CAPTURE-ACTIONS-001`: workflow behavior: ordered post-capture actions, independent failure continuation, and task-result recording.
3. `REGION-CAPTURE-001`: deeply native behavior: interactive region selection, permission prompts, DPI/monitor mapping, and confirm/cancel.
4. `EDITOR-SESSION-001`: ImageEditor slice covering source-image load, rectangle annotation, selection, undo/redo, deterministic export, and annotation-document round-trip.

For each slice:

- Write the contract package.
- Map the current Avalonia/platform implementation to requirement IDs.
- Create shared conformance vectors and scenarios.
- Identify ambiguities and missing product decisions.

### Phase 2: Implement the pilot slices natively on all three platforms

Implement the Phase 1 workflow in each native platform application while retaining the current production application. Each implementation SHALL use the Product Contract rather than porting UI code screen by screen.

Sequencing MAY start on the platform with the highest learning value, but Phase 2 is not complete until Windows, macOS, and Linux each implement the same bounded workflow. The experiment SHALL exercise native UI, lifecycle, permissions, persistence, and at least one XerahS-specific integration on every supported platform.

### Phase 3: Evaluate the economics

Measure:

- Agent and human elapsed time per platform
- Contract-authoring and review effort
- Defects found by conformance versus platform testing
- Behavioral drift
- Native accessibility and UX quality
- Startup time, memory use, and package size
- OS integration quality
- Framework and dependency burden
- Release and signing complexity

Compare these results with equivalent behavior in the existing Avalonia architecture and include the cost of maintaining the `AGENTS.md` hierarchy and conformance system.

### Phase 4: Decide the long-term topology

After the pilot, choose among:

1. Continue the greenfield native applications governed by the Product Contract.
2. Use a shared non-UI engine within the greenfield repository while retaining native shells.
3. Limit the greenfield project to selected native experiences and retain Avalonia for the remainder.
4. Stop the native rollout but retain Product Contracts and hierarchical agent governance for future development.

Only a subsequent approved XIP may authorize a broad native rewrite, change or freeze framework choices beyond the section 14 defaults, or retire Avalonia components.

## 10. Alternatives Considered

### 10.1 Continue with Avalonia as both implementation and behavioral source

This minimizes near-term change and code duplication but leaves product intent embedded in implementation details. It does not solve agent ambiguity or parity governance.

### 10.2 Build three native applications without a formal contract

This maximizes native freedom but creates unacceptable drift risk. AI makes code generation fast enough to produce three applications; it does not ensure that they remain the same product.

### 10.3 Shared engine DLL with three native shells

This retains exact shared business logic while allowing native UI. It is a credible intermediate or permanent architecture after the pilot. Its limitation is that a shared binary and language runtime can constrain native application design and can again become an undocumented behavioral source. Under this XIP, the pilot ships no shared product runtime (D-SHARE-001). A shared engine remains subordinate to the Product Contract and requires D-KERN-001 or a later topology XIP.

### 10.4 Contract-first native applications

This is the proposed strategic direction. It maximizes platform independence and makes product behavior explicit. It also has the highest governance, validation, packaging, and operational burden, which is why a measured pilot is required before committing to a broader native rollout.

## 11. Risks and Mitigations

| Risk | Consequence | Mitigation |
|---|---|---|
| Natural-language ambiguity | Three plausible but inconsistent implementations | Normative keywords, stable requirement IDs, examples, state machines, and executable vectors |
| Correlated AI mistakes | Spec, code, and tests repeat the same misunderstanding | Separate contract, implementation, and conformance roles; require product review for behavior decisions |
| Validation cost exceeds authoring savings | Native strategy becomes slower or less reliable | Measure the pilot end to end, including testing and release work rather than code generation alone |
| Platform drift | Features ship on one OS and remain absent elsewhere | Traceability manifests, parity dashboards, release gates, and expiring waivers |
| Linux fragmentation | "Native Linux" behaves differently across desktops and packaging systems | D-LIN-001: Qt 6, GNOME 46+ and Plasma 6 first-class, Wayland first, Ubuntu 24.04 / Fedora current / Arch, XDG portals required |
| Duplicate security-sensitive logic | Inconsistent or vulnerable implementations | Exact test vectors, security review, protocol standards, and approved shared libraries where appropriate |
| Contract bureaucracy | Small changes become slow | Scale evidence and review requirements according to risk; allow patch-level clarifications without full product approval |
| Premature rewrite | Working functionality is lost while architecture is unproven | Keep Avalonia production paths during the pilot and require an evidence-based follow-up XIP |
| Native ecosystem churn | Three SDK and packaging stacks create operational load | Explicit platform ownership, supported OS baselines, automated builds, and dependency policies |
| Instruction sprawl | Agents miss rules or encounter conflicts | Root constitution, scoped deltas, stable rule IDs, hierarchy linting, and effective-instructions reports |
| Stale local guidance | Child rules preserve obsolete framework or command assumptions | Assigned scope owners, link checks, periodic validation, and removal of duplicated rules |
| ImageEditor submodule version skew | Editor behavior and host integration move on different revisions | Keep native editor modules in the monorepo and land contract, implementation, and evidence atomically |
| Renderer drift | Effects produce materially different images across platforms | Deterministic vectors, golden images, explicit tolerances, and a separately approved headless-kernel option if evidence requires it |

## 12. Success Criteria for the Pilot

The pilot succeeds when:

1. At least four representative capabilities, including the mandatory ImageEditor slice, have approved, versioned contract packages.
2. Every normative pilot requirement maps to evidence on Windows, macOS, and Linux, or to an approved platform disposition.
3. The conformance runner detects intentionally introduced behavioral differences.
4. Independent platform agents can implement a contract change without treating another platform's source code as the specification.
5. Contract and platform review effort is measured, including human decision time.
6. Native implementation produces a demonstrable improvement in at least one of accessibility, OS integration, reliability, performance, or user experience.
7. The total maintenance and release burden is documented well enough to support a go/no-go decision on a broader native rollout.
8. The instruction linter can resolve and report the effective root-to-leaf `AGENTS.md` rules for every pilot path without conflict.

The pilot does not succeed merely because AI generates three compiling implementations.

## 13. Non-Goals

This XIP does not:

- Authorize removal of Avalonia or the current .NET projects.
- Freeze platform frameworks beyond the pilot-binding defaults in section 14. A later XIP may change a framework after measured evidence.
- Require all existing XerahS behavior to be specified immediately.
- Require duplicated implementations where shared code is demonstrably safer.
- Define mobile, web, or server targets.
- Specify the plugin IPC schema or ship a plugin host. Decision D-PLUG-001 locks the direction; a follow-up XIP owns the wire protocol.
- Promise pixel-identical UI across platforms.
- Make AI-generated changes exempt from code review, security review, testing, signing, or release governance.
- Create `AGENTS.md` files in directories that have no distinct governance boundary.
- Modify or retire the existing `KovaForge/ShareX.ImageEditor` repository, which remains independently owned by its existing consumers.
- Authorize MSIX Store distribution, Apple App Store distribution, or Flathub publication of the greenfield applications. Those remain separate packaging XIPs.
- Support Windows 10 or macOS 13 and earlier in the greenfield applications. The Avalonia application continues to serve those users until a later XIP says otherwise.

## 14. Architecture Decisions

These decisions bind the greenfield pilot. Each may be superseded only by a later approved XIP, or by a time-bound waiver under D-REL-001. They are judgments for approval, not observations from a completed pilot.

### D-OWN-001 Product Contract ownership and approval authority

**Decision.** Separate authorship from approval.

| Role | Authority | Current holder |
|---|---|---|
| Product owner (human) | Approves user-visible contract behavior, Unavailable/Degraded dispositions, waivers, root constitution changes, and releases | Michael D |
| Contract steward | Owns `product-contract/` quality, identifiers, versioning, and patch-level clarifications that do not change behavior | Designated agent or human reporting to the product owner |
| Platform owner (Windows, macOS, Linux) | Owns native realization, platform tests, packaging, and signing for that OS | One named owner per platform; a person MAY hold more than one platform |
| Conformance owner | Owns the shared runner, fixtures, tolerances, and independent verification | Must not be the sole platform owner of a platform under test |
| Governance owner | Owns the `AGENTS.md` hierarchy, rule-ID registry, and instruction linter | Product owner unless delegated in writing |

Agents MAY author contracts, implementations, and tests. Agents MUST NOT be the sole approver of a MUST/MUST NOT behavior change, a waiver, or a release.

### D-CON-001 Contract schema, versioning, and tooling

**Decision.** The pilot contract format is the directory layout in section 4.1.

- Each capability is a directory named `<AREA>-<SLUG>-<NNN>/` containing `SPEC.md`, `SCENARIOS.feature`, and any of `STATE_MACHINE.md`, `*.schema.json`, `test-vectors.json`, `fixtures/`, and `platforms/*.md` that the capability requires.
- `product-contract/manifest.yaml` lists every capability, its SemVer, and its requirement IDs. The manifest schema SHALL be published as JSON Schema.
- Normative prose uses RFC 2119 keywords. Requirement IDs are stable (`PCC-001`, `FN-004`) and never reused for a different meaning.
- Versioning follows section 7.4. The contract version is the product version the native apps claim.
- Tooling: a `tools/contract-linter` validates manifests, IDs, parent/child `AGENTS.md` links, and required files. The conformance runner in `conformance/runner` is a pinned Python 3.12-or-later CLI for the pilot; platform adapters are thin executables invoked by that runner.
- English remains the primary human interface. Machine-readable artifacts are mandatory wherever section 4.4 says they SHOULD exist; for the four pilot slices they SHALL exist.

### D-WIN-001 Windows framework and OS baseline

**Decision.** Windows App SDK with WinUI 3 for settings, history, and editor chrome. Win32 and WinRT for region-capture overlay, DXGI Desktop Duplication, global hotkeys, notify-icon tray, startup, and file-type association. Language: C# with CsWin32 or equivalent projections. Packaging for the pilot: unpackaged desktop first (ShareX-class capture and hotkeys are simpler unpackaged). MSIX is a later packaging XIP.

**OS baseline:** Windows 11 version 23H2 and later. Windows 10 is out of scope for greenfield native apps because it is past mainstream support as of this XIP; Avalonia XerahS remains the Windows 10 vehicle.

**Rationale.** WinUI 3 is the current Windows desktop UI stack and gives native Windows 11 accessibility and controls. Capture overlays, DXGI, and tray behavior still live in Win32/WinRT, so a hybrid is required rather than a pure WinUI app. WPF is in maintenance. MAUI and Avalonia would reintroduce a cross-platform UI layer, which this XIP exists to leave. C# is the language agents and the current Windows platform layer already use well; C++ is not required for native Windows behavior.

### D-MAC-001 macOS SwiftUI/AppKit boundary and OS baseline

**Decision.** SwiftUI for settings, history, onboarding, and editor chrome that SwiftUI can host. AppKit for the capture overlay, `NSStatusItem`, panel-style utility windows, and any ScreenCaptureKit preview surface SwiftUI cannot host with acceptable latency. Notifications: `UNUserNotificationCenter`. Login item: `SMAppService`. Hotkeys: Carbon `RegisterEventHotKey` as the primary path so Accessibility is not a prerequisite; it remains the supported hotkey API on macOS 14+ despite the broader Carbon deprecation. Capture: ScreenCaptureKit, with `SCScreenshotManager` on macOS 14+. Language: Swift.

**OS baseline:** macOS 14 Sonoma and later. macOS 12.3 remains the historical ScreenCaptureKit floor for Avalonia XerahS; it is not a greenfield SwiftUI baseline.

**Rationale.** SwiftUI is production-ready on 14+ and matches the agent-native, declarative implementation style. AppKit remains mandatory for overlay windows and menu-bar-only operation. Raising the floor from 12.3 avoids spending the pilot on SwiftUI backports.

### D-LIN-001 Linux toolkit, desktop, display-server, portal, distro, and packaging

**Decision.**

- **Toolkit:** Qt 6, dynamically linked (LGPL), GPL v3 application. Qt Widgets for overlay, tray, and global input. Qt Quick MAY be used for settings and history chrome. GTK4/libadwaita is rejected for the pilot because ShareX-class overlay and power-user density fights the GNOME HIG, while Flameshot, Spectacle, and Ksnip already prove this product shape on Qt.
- **Desktops:** GNOME 46+ and KDE Plasma 6 are first-class. wlroots compositors (Sway, Hyprland) are best-effort through portals.
- **Display server:** Wayland first. X11 is a documented fallback, not the design center.
- **Portals:** xdg-desktop-portal is required for Screenshot, ScreenCast, GlobalShortcuts, Notification, FileChooser, and Inhibit. Direct protocols are fallbacks when a portal is absent, and they MUST be diagnosed in the UI rather than failing silently.
- **Distros:** Ubuntu 24.04 LTS is the documentation and developer target. Fedora current Workstation (SELinux enforcing, Flatpak-first) is the acceptance gate, matching XIP0082. Arch is the tertiary smoke target. NixOS is out of scope.
- **Packaging:** `.tar.gz`, `.deb`, and `.rpm` are first-class. AUR packaging continues. Flatpak is the intended store path but is not a pilot publication gate. AppImage is deferred, matching XIP0079.
- **Filesystem:** XDG Base Directory Specification, matching XIP0075. No home-directory litter.
- **Language:** C++17.

### D-REPO-001 Monorepo

**Decision.** One repository: [BriarForge/XerahS](https://github.com/BriarForge/XerahS). The Product Contract, three platform trees, conformance corpus, and governance tooling share one revision so a behavior change can update spec, implementations, fixtures, and traceability atomically.

CI SHALL use path filters so a Windows-only change does not require a macOS or Linux full build, and vice versa. Sparse checkout is allowed. Splitting into per-platform repositories is forbidden until a later XIP shows that monorepo operational cost exceeds the coordination cost of multi-repo contract changes.

The canonical proposal is BXIP001 in BriarForge/XerahS (`docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/`). This XIP is the ShareX-side snapshot. Execution happens in BriarForge/XerahS.

### D-SHARE-001 Shared binaries

**Decision.** The pilot ships no shared product runtime binary and no common UI or capture library.

Shared artifacts are limited to:

- the Product Contract, schemas, fixtures, and golden images
- the conformance runner and its adapters
- the contract linter and governance tooling

OS secret stores (DPAPI, Keychain, libsecret) are used in-process on each platform; there is no shared crypto DLL.

A later XIP MAY add a shared library only for (a) the headless rendering kernel if D-KERN-001 is met, or (b) a tiny language-neutral filename/token expander if independent implementations diverge on the FILENAME-GENERATION-001 vectors after two remediation cycles. Shared code remains an implementation choice subordinate to the contract.

### D-PLUG-001 Cross-language plugin, automation, and configuration

**Decision.** Greenfield applications SHALL NOT load in-process .NET plugins.

- **Automation:** CLI and MCP remain the language-neutral automation surface (XIP0063, XIP0064). Their contracts are part of the Product Contract, with stable subcommands, JSON output, and exit codes.
- **Configuration, history, and annotation documents:** versioned JSON Schema, UTF-8, language-neutral. Custom uploader HTTP templates stay first-class because they are already data, not code.
- **Plugins:** out-of-process, capability-declared, stdio JSON-RPC, settings as JSON Schema. This XIP locks that direction. A follow-up XIP specifies the handshake, sandbox, secret-passing, and packaging. Until that XIP lands, built-in uploaders and custom HTTP uploaders are the only destinations the native apps MUST implement.
- **Migration:** Avalonia/ShareX plugin settings that can be expressed as custom HTTP uploaders MUST import. Arbitrary in-process .NET plugin code is compatibility-best-effort through the Avalonia app, not through the native apps.

### D-REL-001 Release policy when a platform cannot implement a capability

**Decision.** A platform may claim a contract version only when every required requirement for that version has an accepted disposition on that platform.

- Dispositions are the section 7.3 categories: Required, Equivalent, Degraded, Unavailable, Not applicable.
- Unavailable and Degraded require product-owner approval before the contract version is tagged.
- "Not implemented" is not a disposition.
- Time-bound waivers: named owner, user-visible limitation, expiry no longer than 90 days, and a remediation plan. Expired waivers fail the release gate.
- Staggered *builds* are allowed. Staggered *claimed contract versions* are not. If Windows is ready for contract 1.2.0 and Linux is not, Windows stays on 1.1.x, or 1.2.0 is tagged only after Linux has a disposition.
- Security patches MAY ship per-platform immediately without waiting for contract parity.
- Windows-first silent landing of user-visible features is forbidden as the default.

### D-REV-001 Human review boundaries

**Decision.** Human review is required for:

1. Any contract change that alters user-visible behavior, persistence, security, privacy, permissions, or compatibility.
2. Unavailable, Degraded, and waiver requests.
3. Root `AGENTS.md` constitution changes.
4. Security-sensitive implementation: capture permission flows, credential storage, uploader secrets, plugin hosts, and network trust.
5. Signing, notarization, and release publication.

Human review is not required before landing:

- patch-level contract clarifications that do not change behavior
- additional tests and fixtures
- platform-idiomatic layout or control changes that preserve contracted semantics
- non-behavior refactors inside one platform tree

The same agent SHOULD NOT be the sole author, implementer, verifier, and approver of a material contract change. The product owner is the only authority that can accept Unavailable or an expired-waiver extension.

### D-AGT-001 AGENTS.md hierarchy

**Decision.**

- **Ownership:** governance owner, with product-owner approval for the root constitution.
- **Rule-ID namespaces:** `ROOT-*`, `CONTRACT-*`, `PLATFORM-*`, `WIN-*`, `MAC-*`, `LIN-*`, `CONF-*`, `EDITOR-*`, `TOOL-*`. IDs are unique repository-wide and never reused.
- **Maximum depth:** four levels (root, first-level scope, platform, feature module such as `platforms/macos/image-editor`). Deeper files require a governance-owner exception recorded in `product-contract/decisions/`.
- **Child files** add constraints only; they reference parent IDs and MUST NOT copy parent rule text.
- **CI:** the contract linter is blocking on broken parent/child links, missing or duplicate IDs, child override of protected root rules, directories that declare a scope but are absent from the parent index, and contract or platform changes that lack traceability updates. CI SHALL publish an effective-instructions report for every changed path.

### D-GOLD-001 Golden-image tolerances

**Decision.** Conformance distinguishes exact and perceptual comparisons. The conformance owner maintains `conformance/image-editor/tolerances.yaml`. Missing tolerances fail closed.

| Class | Comparison | Tolerance |
|---|---|---|
| Synthetic vector annotations, no text, integer coordinates, sRGB PNG, CPU rasterizer | Byte-identical PNG after canonical encoding, or per-pixel exact RGB | Zero |
| Lossless round-trip of an input PNG with no effects | Byte-identical | Zero |
| Lossy encode (JPEG/WebP) of a specified quality | SSIM against the contract encoder vector | SSIM >= 0.990 |
| Text rendered with the bundled conformance typeface | Per-pixel in sRGB after rasterizing at contract DPI | Max CIEDE2000 1.0; no system fonts |
| Image effects on CPU | Per-pixel or SSIM as declared per effect | Default max 1 LSB per 8-bit channel, or SSIM >= 0.995 |
| GPU-accelerated path | Same vectors as CPU | Must match the CPU result within the effect's published tolerance |
| Color-managed fixtures | Convert to linear sRGB using the embedded profile, then compare | Fixtures are tagged sRGB unless a test explicitly uses another profile |

Rules:

- Editor-export goldens use a bundled OFL typeface (Noto Sans or equivalent) checked into `conformance/image-editor/fonts/`. Native UI may use system fonts; export goldens MUST NOT.
- Deterministic conformance runs with the GPU rasterizer disabled. GPU is a performance path that must still pass the same vectors within tolerance.
- Resampling algorithm is named in the contract (default: Catmull-Rom). The CPU reference is authoritative.
- Font hinting, subpixel positioning, and ClearType/Core Text/FreeType differences are why export goldens use bundled fonts and a declared DPI. If two platforms still diverge on bundled-font text after one remediation cycle, the conformance owner MAY widen that case to CIEDE2000 2.0 without changing exact classes.

### D-KERN-001 Shared headless rendering kernel threshold

**Decision.** A follow-up XIP proposing a shared headless rendering kernel is justified only when at least one of the following is true:

1. Two or more independent native renderers fail the same required effect vector after two documented remediation cycles.
2. Pixel drift on a required effect exceeds D-GOLD-001 tolerance on two or more platforms, and the cause is algorithmic rather than a single-platform bug.
3. Security review finds duplicated unsafe image-parsing or decoder code with divergent patch status.
4. Over a measured six-week window, the cost of keeping three effect implementations exceeds twice the estimated cost of one kernel plus three thin adapters.
5. A compatibility requirement needs bit-exact export across platforms (for example, reopen `.xann` and export an identical PNG on every OS).

Convenience, one platform falling behind, or a preference for a common DLL is not sufficient. Any kernel remains bound by section 8.6.

### D-ID-001 Pilot application identity

**Decision.** Recorded in section 8.7. Greenfield apps use `com.xerahs.native` (or the platform equivalent) and the display name "XerahS Native" until a later XIP authorizes identity collapse.

## 15. Residual follow-ups

These are intentionally not decided here and do not block approval of this XIP:

- Plugin handshake, sandbox, secret-passing, and package format (follow-up XIP after D-PLUG-001).
- MSIX, Apple notarized DMG/App Store, and Flathub publication of the *native* apps.
- Shared rendering kernel (only if D-KERN-001 is met).
- Avalonia retirement or identity collapse (only after Phase 4 evidence).
- Windows 10 or macOS 13 support for native apps.
- Exact Windows App SDK and Qt 6 minor versions, which are pinned in platform `AGENTS.md` at implementation time.

## 16. Definition of Done for This XIP

- The architectural principles are accepted or rejected explicitly.
- The section 14 architecture decisions are accepted or individually superseded.
- A Product Contract pilot location and format are approved (`product-contract/` as in section 4.1 and D-CON-001).
- The greenfield [BriarForge/XerahS](https://github.com/BriarForge/XerahS) repository has a root constitution and scoped first-level `AGENTS.md` files following D-AGT-001.
- The four named pilot capabilities in Phase 1 are selected: `FILENAME-GENERATION-001`, `POST-CAPTURE-ACTIONS-001`, `REGION-CAPTURE-001`, `EDITOR-SESSION-001`.
- ImageEditor is accepted as an internal feature of each native solution rather than a production submodule.
- The ImageEditor host boundary, annotation-document compatibility policy, and conformance ownership are approved.
- Roles in D-OWN-001 are assigned.
- CI requirements for traceability and parity are agreed, including D-REL-001 and D-GOLD-001.
- No Avalonia deprecation or native rewrite begins without the pilot evidence and a follow-up XIP.

## 17. Related Proposals

- [XIP0013 macOS Implementation](XIP0013-macos-implementation.md)
- [XIP0014 Linux Support Implementation Plan](XIP0014-linux-support-implementation-plan.md)
- [XIP0019 Platform Abstraction and Architecture Audit](XIP0019-platform-abstraction-architecture-audit.md)
- [XIP0052 Agentic Refactoring and Architectural Modernization](XIP0052-agentic-refactoring-architectural-modernization.md)
- [XIP0063 XerahS CLI OpenClaw Compatibility](XIP0063-xerahs-cli-openclaw-compatibility.md)
- [XIP0064 XerahS MCP Server](XIP0064-xerahs-mcp-server.md)
- [XIP0068 Re-editing Saved Annotations](XIP0068-re-editing-saved-annotations.md)
- [XIP0075 Linux XDG + Flathub Readiness](XIP0075-linux-xdg-flathub-readiness.md)
- [XIP0078 macOS Improvement Plan](XIP0078-macos-improvement-plan.md)
- [XIP0079 Linux Improvement Plan](XIP0079-linux-improvement-plan.md)
- [XIP0082 Fedora Linux Validation and Flathub Submission Gate](XIP0082-fedora-linux-validation-and-flathub-submission-gate.md)
- [XIP0084 Windows Region Capture Algorithm Parity](XIP0084-windows-region-capture-algorithm-parity.md)

## 18. Review Record

Review of the proposal before closing section 14 found the following issues. Each is resolved in this revision unless marked residual.

1. **Framework tables deferred while the XIP asked to be approved.** Section 3.3 now records the section 14 defaults instead of leaving Windows and Linux unspecified.
2. **Non-goals contradicted the request to close framework and plugin decisions.** Non-goals now distinguish pilot-binding defaults from frozen forever choices.
3. **Phase 2 asked for one platform and all platforms.** Phase 2 now requires the same slice on Windows, macOS, and Linux, with optional sequencing.
4. **Pilot slices were examples, not identifiers.** Phase 1 now names four capability IDs.
5. **Filename vector omitted padding and extension** that the expected output assumed. The example now includes `counter_padding` and `extension`.
6. **CI parity report was SHOULD while the release gate was SHALL.** The report is now SHALL.
7. **No coexistence rule** for Avalonia and greenfield installs. Section 8.7 and D-ID-001 require a distinct application identity and side-by-side install.
8. **Ownership was a role soup with no human approver.** D-OWN-001 names the product owner as the human authority for behavior, waivers, and releases.
9. **In-process .NET plugins cannot survive a non-.NET Linux or macOS app.** D-PLUG-001 sets out-of-process JSON-RPC as the direction and keeps custom HTTP uploaders as the native-app destination floor.
10. **Windows 10 as an implicit baseline is stale in August 2026.** D-WIN-001 sets Windows 11 23H2+ for greenfield; Avalonia continues to serve Windows 10.
11. **Golden-image policy was an open question in an exactness-sensitive editor.** D-GOLD-001 sets fail-closed exact vs perceptual classes and bundled fonts.
12. **Shared-kernel exception had no evidence bar**, which would let convenience restore a common DLL. D-KERN-001 sets a measurable threshold.

Residual risks that remain acceptable for a proposed XIP:

- Qt on GNOME will look less native than GTK. That is a conscious product trade for overlay and power-user density, to be measured in Phase 3.
- Unpackaged WinUI plus Win32 overlay is a real integration tax. Phase 3 must count it.
- Python as the pilot conformance runner is a convenience choice; pinning a binary later is allowed without a contract change.
- Named platform owners are roles. Filling them with people is an operational step, not an architecture gap.

## 19. Evolution History

| Date | Change | Rationale |
|---|---|---|
| 2026-08-30 | Initial proposal | Capture the contract-first, agent-native architecture and define a governed pilot before any existing framework retirement |
| 2026-08-30 | Defined greenfield repository and hierarchical agent governance | Establish BriarForge/XerahS as the implementation target and make root-to-leaf scoped instructions part of the architecture |
| 2026-08-30 | Defined native ImageEditor ownership | Integrate ImageEditor into each native solution, retain the existing repository as a legacy reference, and prohibit it as a production submodule |
| 2026-08-30 | Closed open decisions as pilot-binding architecture choices | Record ownership, contract format, platform baselines, monorepo, sharing, plugins, release, review, AGENTS.md, goldens, and kernel threshold so the pilot can start without a second architecture XIP |
| 2026-08-30 | Split into BXIP001 | The single-file XIP exceeded a useful review size; the canonical document set lives in BriarForge/XerahS under docs/proposals/BXIP001-contract-first-agent-native-platform-architecture/ |
