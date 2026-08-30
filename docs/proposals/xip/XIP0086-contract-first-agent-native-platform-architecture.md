# XIP0086 Contract-First Agent-Native Platform Architecture

**Status**: Proposed
**Created**: 2026-08-30
**Area**: Architecture | Agentic Development | Windows | macOS | Linux
**Related**: XIP0013, XIP0014, XIP0019, XIP0052, XIP0078, XIP0079, XIP0084
**Implementation repository**: [BriarForge/XerahS](https://github.com/BriarForge/XerahS)
**Decision requested**: Approve a greenfield, contract-first XerahS implementation in which a versioned, plain-English Product Contract becomes the source of product behavior and independent native applications implement that contract on Windows, macOS, and Linux.

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
| Windows | Windows-native UI and Windows APIs | Exact UI framework requires a separate architecture decision; candidates include Windows App SDK/WinUI and established Windows desktop technologies |
| macOS | SwiftUI with AppKit where required | AppKit remains necessary for capabilities not adequately exposed through SwiftUI |
| Linux | Native Linux desktop stack plus portals/Wayland/X11 integrations | "Linux native" is not singular; the primary toolkit, desktop baseline, and packaging targets require a separate decision |

The Product Contract SHALL remain independent of these choices. Replacing a platform framework must not require redefining product behavior.

### 3.4 Permit native adaptation without permitting silent divergence

Parity means equivalent product outcomes, not necessarily identical pixels or interaction mechanics.

Each implementation:

- MUST satisfy shared behavioral invariants.
- MUST use native accessibility, navigation, permission, and lifecycle conventions where they differ.
- MAY use different interaction mechanics when required by platform conventions.
- MUST document any material behavioral deviation.
- MUST NOT silently omit a contracted capability.

For example, a settings view may use different native controls and layout on each platform while preserving the same setting meanings, defaults, validation, persistence, and downstream effects.

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
  schemas/
  decisions/
  waivers/

platforms/
  AGENTS.md
  windows/
    AGENTS.md
  macos/
    AGENTS.md
  linux/
    AGENTS.md

conformance/
  AGENTS.md
  runner/
  adapters/
    windows/
    macos/
    linux/
  reports/

tools/
  contract-linter/
    AGENTS.md
```

This layout is illustrative. The pilot SHALL validate the design before a repository-wide reorganization.

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
    "counter": 7
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
- `conformance/AGENTS.md` governs independent verification. It MUST prohibit deriving expected results solely from one platform implementation.
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

CI SHOULD produce a capability report such as:

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

Independent native implementations increase the importance of stable, language-neutral formats. The Product Contract SHOULD define:

- Configuration and migration schemas
- History and task result formats
- CLI behavior and exit codes
- Automation and MCP contracts
- Plugin boundaries
- Uploader definitions and network behavior
- Diagnostic and telemetry semantics

Where in-process .NET plugins cannot be used by a non-.NET native application, a language-neutral out-of-process or protocol-based plugin model will require a separate XIP.

### 8.3 Reference implementations are informative, not normative

During greenfield development, the Avalonia application and the original ShareX implementation are valuable behavioral references and test oracles. They SHALL NOT override an approved Product Contract or dictate the new repository structure. If code and contract disagree, the discrepancy must be resolved explicitly rather than silently copying the code.

## 9. Greenfield Implementation Strategy

### Phase 0: Approve principles and boundaries

- Approve the Product Contract as the future source of product truth.
- Confirm Windows, macOS, and Linux as the initial supported native targets.
- Define ownership for contract approval, platform deviations, and releases.
- Establish the root and first-level `AGENTS.md` hierarchy in [BriarForge/XerahS](https://github.com/BriarForge/XerahS).
- Record that greenfield development does not deprecate the existing Avalonia application by itself.

### Phase 1: Build a contract pilot

Select three representative vertical slices:

1. A deterministic shared behavior, such as filename generation.
2. A workflow behavior, such as ordered post-capture actions and failure continuation.
3. A deeply native behavior, such as global hotkeys or region capture.

For each slice:

- Write the contract package.
- Map the current Avalonia/platform implementation to requirement IDs.
- Create shared conformance vectors and scenarios.
- Identify ambiguities and missing product decisions.

### Phase 2: Implement one native vertical slice

Implement one bounded user workflow in each native platform application while retaining the current production application. Each implementation SHALL use the Product Contract rather than porting UI code screen by screen.

The platform should be selected according to expected learning value, not convenience alone. The experiment should exercise native UI, lifecycle, permissions, persistence, and at least one XerahS-specific integration.

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

Only a subsequent approved XIP may authorize a broad native rewrite, establish final framework choices, or retire Avalonia components.

## 10. Alternatives Considered

### 10.1 Continue with Avalonia as both implementation and behavioral source

This minimizes near-term change and code duplication but leaves product intent embedded in implementation details. It does not solve agent ambiguity or parity governance.

### 10.2 Build three native applications without a formal contract

This maximizes native freedom but creates unacceptable drift risk. AI makes code generation fast enough to produce three applications; it does not ensure that they remain the same product.

### 10.3 Shared engine DLL with three native shells

This retains exact shared business logic while allowing native UI. It is a credible intermediate or permanent architecture. Its limitation is that a shared binary and language runtime can constrain native application design and can again become an undocumented behavioral source. Under this XIP, a shared engine is permitted but remains subordinate to the Product Contract.

### 10.4 Contract-first native applications

This is the proposed strategic direction. It maximizes platform independence and makes product behavior explicit. It also has the highest governance, validation, packaging, and operational burden, which is why a measured pilot is required before committing to a broader native rollout.

## 11. Risks and Mitigations

| Risk | Consequence | Mitigation |
|---|---|---|
| Natural-language ambiguity | Three plausible but inconsistent implementations | Normative keywords, stable requirement IDs, examples, state machines, and executable vectors |
| Correlated AI mistakes | Spec, code, and tests repeat the same misunderstanding | Separate contract, implementation, and conformance roles; require product review for behavior decisions |
| Validation cost exceeds authoring savings | Native strategy becomes slower or less reliable | Measure the pilot end to end, including testing and release work rather than code generation alone |
| Platform drift | Features ship on one OS and remain absent elsewhere | Traceability manifests, parity dashboards, release gates, and expiring waivers |
| Linux fragmentation | "Native Linux" behaves differently across desktops and packaging systems | Define primary desktop, toolkit, Wayland/X11, portal, distribution, and packaging baselines in a separate decision |
| Duplicate security-sensitive logic | Inconsistent or vulnerable implementations | Exact test vectors, security review, protocol standards, and approved shared libraries where appropriate |
| Contract bureaucracy | Small changes become slow | Scale evidence and review requirements according to risk; allow patch-level clarifications without full product approval |
| Premature rewrite | Working functionality is lost while architecture is unproven | Keep Avalonia production paths during the pilot and require an evidence-based follow-up XIP |
| Native ecosystem churn | Three SDK and packaging stacks create operational load | Explicit platform ownership, supported OS baselines, automated builds, and dependency policies |
| Instruction sprawl | Agents miss rules or encounter conflicts | Root constitution, scoped deltas, stable rule IDs, hierarchy linting, and effective-instructions reports |
| Stale local guidance | Child rules preserve obsolete framework or command assumptions | Assigned scope owners, link checks, periodic validation, and removal of duplicated rules |

## 12. Success Criteria for the Pilot

The pilot succeeds when:

1. Three representative capabilities have approved, versioned contract packages.
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
- Select the final native UI framework for any platform.
- Require all existing XerahS behavior to be specified immediately.
- Require duplicated implementations where shared code is demonstrably safer.
- Define mobile, web, or server targets.
- Redesign the plugin protocol.
- Promise pixel-identical UI across platforms.
- Make AI-generated changes exempt from code review, security review, testing, signing, or release governance.
- Create `AGENTS.md` files in directories that have no distinct governance boundary.

## 14. Open Decisions

The pilot must produce recommendations for:

1. Product Contract ownership and approval authority.
2. Contract schema, versioning rules, and tooling.
3. Windows native UI framework and supported OS baseline.
4. macOS SwiftUI/AppKit boundary and supported OS baseline.
5. Linux toolkit, desktop, display-server, portal, distro, and packaging baselines.
6. Monorepo versus coordinated platform repositories.
7. Which algorithms or services remain shared binaries, if any.
8. Cross-language plugin, automation, and configuration compatibility.
9. Release policy when one platform cannot implement a capability.
10. Required human review boundaries for agent-generated contracts and native implementations.
11. Ownership, rule-ID namespace, maximum scope depth, and CI enforcement for the `AGENTS.md` hierarchy.

## 15. Definition of Done for This XIP

- The architectural principles are accepted or rejected explicitly.
- A Product Contract pilot location and format are approved.
- The greenfield [BriarForge/XerahS](https://github.com/BriarForge/XerahS) repository has a root constitution and scoped first-level `AGENTS.md` files.
- Three pilot capabilities are selected.
- Contract, platform, conformance, and product-owner responsibilities are assigned.
- CI requirements for traceability and parity are agreed.
- No Avalonia deprecation or native rewrite begins without the pilot evidence and a follow-up XIP.

## 16. Related Proposals

- [XIP0013 macOS Implementation](XIP0013-macos-implementation.md)
- [XIP0014 Linux Support Implementation Plan](XIP0014-linux-support-implementation-plan.md)
- [XIP0019 Platform Abstraction and Architecture Audit](XIP0019-platform-abstraction-architecture-audit.md)
- [XIP0052 Agentic Refactoring and Architectural Modernization](XIP0052-agentic-refactoring-architectural-modernization.md)
- [XIP0078 macOS Improvement Plan](XIP0078-macos-improvement-plan.md)
- [XIP0079 Linux Improvement Plan](XIP0079-linux-improvement-plan.md)
- [XIP0084 Windows Region Capture Algorithm Parity](XIP0084-windows-region-capture-algorithm-parity.md)

## 17. Evolution History

| Date | Change | Rationale |
|---|---|---|
| 2026-08-30 | Initial proposal | Capture the contract-first, agent-native architecture and define a governed pilot before any existing framework retirement |
| 2026-08-30 | Defined greenfield repository and hierarchical agent governance | Establish BriarForge/XerahS as the implementation target and make root-to-leaf scoped instructions part of the architecture |
