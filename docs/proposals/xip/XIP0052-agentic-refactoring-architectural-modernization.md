# XIP0052 Agentic Refactoring & Architectural Modernization
## 1. Executive Summary
This proposal outlines a plan to refactor three major architectural pain points within the XerahS codebase to improve its maintainability, testability, and suitability for AI-assisted (agentic) coding. The core focus areas are eliminating the global service locator pattern in favor of Dependency Injection, breaking down massive monolithic classes, and strictly enforcing MVVM separation in the UI layer.

## 2. Motivation
As XerahS grows and incorporates more automated agentic engineering workflows, structural pain points become significant bottlenecks:
- **Global State**: `PlatformServices`, `TaskManager.Instance`, and `ScreenRecordingManager.Instance` hide dependencies. AI agents (and human developers) cannot easily determine what a class requires just by looking at its constructor, hindering test isolation.
- **Monoliths**: Classes like `WorkerTask.cs` (51KB) and `ScreenCaptureService.cs` (47KB) mix orchestration, OS-specific API calls, and UI logic. Large files consume AI context windows rapidly and increase the risk of unintended side-effects during modification.
- **UI Coupling**: ViewModels (e.g., `TaskSettingsViewModel.FFmpeg.cs`) directly referencing `Avalonia.Controls` violate MVVM principles, making cross-platform UI changes brittle and ViewModels untestable.

## 3. Implementation Plan

### 3.1. Migrate to Microsoft.Extensions.DependencyInjection
* **Scope**: Replace the static `PlatformServices` locator with an `IServiceCollection` based DI container.
* **Compatibility Note**: `Microsoft.Extensions.DependencyInjection` is a standard, cross-platform .NET library. It works natively across Windows, macOS, and Linux targets without any OS-specific restrictions.
* **Action Items**:
  1. Add `Microsoft.Extensions.Hosting` or `Microsoft.Extensions.DependencyInjection` to the `XerahS.Bootstrap` and `XerahS.App` projects.
  2. Map all current `PlatformServices.*` registrations to `services.AddSingleton<I...>()` or `services.AddTransient<I...>()`.
  3. Refactor constructors in Core managers (e.g., `ScreenRecordingManager`, `TaskManager`) to accept these interfaces.

### 3.2. Deconstruct Monolithic Classes
* **Scope**: Break down `WorkerTask.cs` and capture services into focused, composable units using the Strategy or Chain of Responsibility patterns.
* **Action Items**:
  1. Split `WorkerTask.cs` into distinct pipeline stages (e.g., Validation, Execution, Finalization, Uploading).
  2. Extract platform-specific capture orchestration from `ScreenCaptureService.cs` into smaller wrapper classes that strictly implement `IScreenCaptureService`.
  3. Ensure no individual class exceeds ~15KB implicitly.

### 3.3. Enforce Strict MVVM Separation
* **Scope**: Remove all `Avalonia.Controls` and `Avalonia.Controls.ApplicationLifetimes` imports from the `XerahS.UI.ViewModels` namespace.
* **Action Items**:
  1. Audit ViewModels like `TaskSettingsViewModel.*` and `ImageEffectsViewModel.cs`.
  2. Abstract UI interactions (like showing dialogs or managing window lifetimes) behind new interfaces (e.g., `IDialogService`, `ILifecycleService`).
  3. Implement these interfaces in the `XerahS.UI.Services` namespace and inject them into the ViewModels.

## 4. Verification Plan
- **Build Integrity**: Ensure `dotnet build` passes with 0 zero warnings/errors after DI container injection.
- **Unit Testing**: Introduce isolated unit tests for the newly decoupled ViewModels (which will now be possible without spinning up Avalonia UI components).
- **Manual Regression**: Perform a full capture-to-upload workflow manually to ensure the new DI lifecycle correctly resolves the platform-specific capture and upload services.