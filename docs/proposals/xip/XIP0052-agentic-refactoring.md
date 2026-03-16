# XIP0052: Agentic Refactoring & Architectural Modernization

**Status**: PHASE 2 PROPOSAL (partial groundwork already implemented)
**Priority**: High
**Audit date**: 2026-03-16
**Related**: XIP0038

---

## Executive Summary

XerahS has already completed part of the architectural modernization that this XIP originally proposed:

- `Microsoft.Extensions.DependencyInjection` is already referenced in `XerahS.Bootstrap` and `XerahS.UI`.
- `ShareXBootstrap` and `CompositionRoot` already build service providers.
- `WorkerTask` has already been split into partials and a basic pipeline.
- `IDialogService` and `ILifecycleService` already exist as abstractions.

The remaining problem is not "start DI and MVVM cleanup from zero." The remaining problem is that the codebase is in a mixed state:

- DI exists, but composition is duplicated and still backfilled from `PlatformServices`.
- `PlatformServices`, `TaskManager.Instance`, and `ScreenRecordingManager.Instance` still act as global access points.
- Several view models still resolve services from `PlatformServices.RootProvider` or name concrete UI windows directly.
- The largest remaining workflow and capture files are different from the ones cited in the original draft.

This XIP updates the plan to a realistic phase 2 modernization effort focused on consolidation, boundary hardening, and targeted hotspot extraction.

---

## Problem Statement

The current architecture is workable, but it still has three structural issues that slow down change and make AI-assisted refactoring riskier than it needs to be.

### 1. Mixed composition model

XerahS currently has both:

- static platform/service access through `PlatformServices`
- DI containers built from that static state

This leaves the system in an in-between design where constructors do not reliably describe dependencies and multiple hosts can drift in registration behavior.

### 2. MVVM boundaries are only partially enforced

Some view models now depend on abstractions, but others still:

- resolve services via `PlatformServices.RootProvider`
- fall back to `new Avalonia...` services in constructors
- reference concrete window types from `XerahS.UI.Views`
- directly create UI controls or windows

That means "MVVM-ready" and "framework-coupled" patterns currently coexist.

### 3. Refactoring effort is aimed at the wrong current hotspots

The original draft cited `WorkerTask.cs` and `ScreenCaptureService.cs` as if they were still untouched monoliths. That is no longer accurate. The remaining work should now target:

- duplicated or host-specific composition logic
- `WorkerTaskRecording.cs` and remaining `WorkerTask` callbacks/static hooks
- the still-large orchestration inside `ScreenCaptureService.cs`
- missing tests around composition, injected view models, and pipeline behavior

---

## Code Audit (2026-03-16)

### What Is Already Implemented

| Area | Current state | Status |
|------|---------------|--------|
| DI packages | `Microsoft.Extensions.DependencyInjection` already referenced in `XerahS.Bootstrap` and `XerahS.UI` | Complete |
| Bootstrap DI | `ShareXBootstrap.InitializeAsync()` builds a service provider and sets `PlatformServices.RootProvider` | Partial |
| UI DI | `CompositionRoot.BuildAndSetRootProvider()` builds another service provider for UI/app services | Partial |
| ViewModel abstractions | `IDialogService` and `ILifecycleService` exist | Partial |
| Worker task split | `WorkerTask` is now split across partials and uses `WorkerTaskPipeline` | Partial |
| Capture extraction | Linux selector and image compositing helpers already extracted from `ScreenCaptureService` | Partial |

### What Is Still Structurally Wrong

1. **Two composition roots are maintaining overlapping registration lists**
   - `XerahS.Bootstrap/ServiceCollectionExtensions.cs`
   - `XerahS.UI/Services/CompositionRoot.cs`

2. **DI still depends on global state instead of replacing it**
   - both composition paths register objects by reading `PlatformServices`
   - `ScreenRecordingManager.Instance` is still injected by singleton instance

3. **Global singletons still dominate orchestration**
   - `TaskManager.Instance`
   - `ScreenRecordingManager.Instance`
   - widespread direct `PlatformServices.*` usage

4. **View models are not yet constructor-pure**
   - several still resolve services from `PlatformServices.RootProvider`
   - several still fall back to `new AvaloniaDialogService()` or `new AvaloniaDialogServiceAdapter()`
   - some still reference `XerahS.UI.Views` directly

5. **The remaining monolith work is now concentrated elsewhere**
   - `WorkerTaskRecording.cs` is the largest `WorkerTask` partial
   - `ScreenCaptureService.cs` still mixes platform delegation, Linux fallback policy, UI-thread overlay capture, and compositing orchestration

6. **Test coverage has not caught up to the refactor state**
   - extracted Linux selector logic has tests
   - composition-root parity, bootstrap registration, and most dialog-heavy view models do not

---

## Goals

This XIP should deliver the following outcomes:

1. One shared service-registration path for desktop hosts.
2. Fewer callers using `PlatformServices.RootProvider` as a service locator.
3. No view model should need to name a concrete Avalonia window type.
4. `TaskManager` and `ScreenRecordingManager` should become injectable services behind interfaces, even if compatibility shims remain temporarily.
5. Remaining large workflow and capture orchestration should be decomposed by responsibility, not by arbitrary file size targets.
6. Tests should validate the refactor boundaries so future agentic edits are safer.

---

## Benefits After Refactoring

When this XIP is complete, the codebase should feel materially better to work in, not just cleaner on paper.

### 1. Faster, lower-risk feature work

- New desktop features should require fewer edits across unrelated files.
- Constructor signatures will communicate dependencies more clearly than static lookups.
- Changes to capture, recording, or workflow orchestration will have a smaller review surface.

### 2. Safer AI-assisted and human-assisted refactoring

- Agents and contributors will be able to infer dependencies from constructors and registrations instead of chasing global state.
- Smaller, responsibility-focused classes will reduce context-window pressure and accidental breakage.
- Shared composition logic will make it easier to reason about what is actually available in each host.

### 3. Better testability and easier regression prevention

- View models will be testable with explicit fake services instead of Avalonia windows or service-locator setup.
- Task and recording orchestration can be validated through interfaces rather than global singleton initialization.
- Composition tests will catch registration drift before it turns into runtime failures.

### 4. More predictable host behavior

- CLI and UI will resolve services through the same core registration path.
- Fewer host-specific wiring differences means fewer "works in UI but not CLI" or "works in CLI but not UI" regressions.
- The root provider will become an implementation detail of composition, not an application-wide escape hatch.

### 5. Cleaner MVVM and UI boundaries

- View models will describe user-intent and workflow behavior rather than window construction details.
- UI-specific concerns will stay in UI services and composition code.
- Future UI framework or shell changes will be less invasive because behavior and presentation are better separated.

### 6. Better onboarding and maintenance

- New contributors will have clearer architectural entry points.
- Debugging will be easier because control flow and dependency ownership will be more explicit.
- The project will be easier to evolve incrementally without reopening the same global-state seams every time.

---

## Non-Goals

This XIP does not require:

- removing every `PlatformServices.*` call in one pass
- rewriting mobile projects
- redesigning all Avalonia UI patterns
- forcing every class under a strict KB threshold

The goal is to complete the architectural transition cleanly, not to churn the entire codebase at once.

---

## Implementation Plan

### 1. Consolidate Composition Into One Shared Builder

#### Scope

Replace duplicated registration lists with one shared desktop registration path used by both UI and CLI hosts.

#### Problems addressed

- duplicated service registration logic between bootstrap and UI
- drift risk between hosts
- ambiguity over which provider is authoritative

#### Action items

1. Introduce a shared service-registration builder used by both desktop hosts.
   - This can live in `XerahS.Bootstrap` or another shared desktop composition project.
   - Both CLI and UI should call the same registration method.

2. Split registration into clear phases:
   - platform services
   - host/app services
   - optional UI-only services

3. Stop maintaining two separate overlapping registration lists.

4. Keep `PlatformServices` only as a bootstrap/platform boundary during migration, not as the long-term source of truth for application wiring.

#### Acceptance criteria

- CLI and UI use the same core registration path.
- service registration parity can be tested without starting Avalonia.
- `PlatformServices.RootProvider` is no longer overwritten by multiple unrelated registration lists.

---

### 2. Move From "Inject Static Instance" to Injectable Managers

#### Scope

Replace direct reliance on `TaskManager.Instance` and `ScreenRecordingManager.Instance` with injectable interfaces and implementations.

#### Problems addressed

- hidden dependencies
- event wiring through global state
- hard-to-isolate tests

#### Action items

1. Introduce service abstractions for the core managers.
   - `ITaskManager`
   - `IScreenRecordingManager` or equivalent naming aligned with current conventions

2. Register the concrete implementations in DI.

3. Convert highest-fanout consumers first:
   - workflow orchestration
   - recording view models
   - upload/capture entry points
   - CLI commands

4. Keep temporary compatibility shims only where necessary during migration.

5. Remove new registrations of `.Instance` into DI once constructor migration is complete.

#### Acceptance criteria

- new code depends on interfaces, not `.Instance`.
- manager consumers can be unit tested with fakes.
- static singleton usage is reduced to compatibility edges only.

---

### 3. Finish MVVM Boundary Hardening

#### Scope

Complete the UI separation work that has started, but target the actual remaining leaks.

#### Problems addressed

- view models resolving services from `RootProvider`
- concrete window types named in view models
- direct construction of Avalonia views/windows from non-view code

#### Action items

1. Remove service-locator fallback from view model constructors.
   - No `PlatformServices.RootProvider?.GetService(...)`
   - No `new AvaloniaDialogService()` fallback inside the view model itself

2. Evolve dialog/window abstractions so they represent use cases, not concrete window types.
   - file picker and folder picker APIs can stay generic
   - modal workflows should be launched through application services, not `ShowDialogAsync<TWindow>`

3. Remove direct `XerahS.UI.Views` references from view models where practical.

4. Move direct window/control creation out of view models and into UI services or composition code.

5. Keep `ILifecycleService`, but actually route shutdown/show-main-window behavior through it where lifecycle behavior is currently implicit or static.

#### Acceptance criteria

- view models do not name concrete Avalonia windows.
- view models do not resolve dependencies from `RootProvider`.
- view models can be instantiated in tests with explicit constructor-supplied collaborators.

---

### 4. Retarget Monolith Reduction to Current Hotspots

#### Scope

Continue the partial extraction work already in progress, but focus on the files that are still large and responsibility-heavy today.

#### Action items

1. Continue `WorkerTask` decomposition around recording and host interaction.
   - extract recording workflow orchestration from `WorkerTaskRecording.cs`
   - reduce reliance on static callback delegates on `WorkerTask`
   - keep `WorkerTask` focused on task lifecycle and pipeline coordination

2. Continue `ScreenCaptureService` decomposition.
   - isolate Linux selection-policy decisions further if needed
   - isolate overlay/UI-thread orchestration from plain capture delegation
   - keep `IScreenCaptureService` behavior stable while shrinking orchestration density

3. Prefer responsibility-based decomposition over arbitrary size limits.
   - file size is a useful smell, not the primary requirement
   - a class should be small enough to review safely and own one coherent concern

#### Acceptance criteria

- remaining orchestration-heavy files shrink because responsibilities moved out, not because code was split mechanically
- capture and recording logic become easier to test independently
- the pipeline stages are easier to extend without reopening the same giant files

---

### 5. Add Refactor-Specific Verification

#### Scope

The verification plan should prove that the new architecture is real, not just cosmetically renamed.

#### Required verification

1. **Build integrity**
   - `dotnet build` passes with 0 warnings/errors for affected projects

2. **Composition tests**
   - verify shared registration path for CLI and UI hosts
   - verify required services are available without relying on `PlatformServices.RootProvider` inside consumers

3. **ViewModel tests**
   - instantiate refactored view models with fake dialog/lifecycle services
   - verify no Avalonia window or desktop lifetime is needed for core behavior

4. **Pipeline and manager tests**
   - verify recording and capture orchestration through injected interfaces
   - verify task completion and recording state transitions without global singleton setup

5. **Manual regression**
   - capture to upload flow
   - record / pause / resume / stop flow
   - destination settings / provider explorer / FFmpeg options flows after dialog abstraction changes

---

## Priority Order

1. Shared composition builder and registration consolidation.
2. Manager interfaces and singleton-to-DI migration.
3. View model boundary hardening and dialog API cleanup.
4. Further `WorkerTaskRecording` and `ScreenCaptureService` extraction.
5. Refactor-specific tests and regression coverage.

This order reduces architectural ambiguity first, then removes the highest-risk global access patterns, then finishes the UI and workflow cleanup on top of a more stable base.

---

## Success Criteria

This XIP is complete when all of the following are true:

1. Desktop hosts share one core service-registration path.
2. `TaskManager` and `ScreenRecordingManager` are consumed primarily through injected interfaces.
3. View models no longer depend on `PlatformServices.RootProvider` or concrete window types.
4. Remaining large workflow/capture classes are reduced by extracting responsibilities, not by cosmetic splitting.
5. Tests exist for composition, injected view models, and the refactored task/recording boundaries.

---

## Summary

The original version of XIP0052 correctly identified the direction of travel, but it described work that is already partially complete. The updated proposal treats the codebase as it actually exists on 2026-03-16:

- the next step is not "introduce DI"
- the next step is "finish the transition away from mixed static + DI architecture"

That makes this XIP a consolidation and boundary-hardening proposal, which is a much better fit for the current XerahS codebase and for safe agentic refactoring.
