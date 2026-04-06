# XIP0060 First-Run Onboarding Wizard

**Status**: Proposed  
**Created**: 2026-04-06  
**Updated**: 2026-04-06  
**Area**: Desktop | UX | Core  
**Goal**: Deliver a polished, multi-step onboarding experience that guides first-time users through essential configuration, dramatically improving activation rates and time-to-value.

---

## Summary

This proposal introduces a comprehensive first-run onboarding wizard for XerahS that activates when `IsFirstTimeRun` is true. The wizard will guide users through five essential configuration steps: language selection, default save location, hotkey assignment, upload destination setup, and OCR language pack selection. By replacing the current minimal first-run experience (a single ShareX import button) with an intentional, step-by-step flow, we can reduce user drop-off, prevent misconfiguration, and ensure users capture their first screenshot within minutes of installation. The wizard integrates seamlessly with existing `SettingsBase` infrastructure and leverages Avalonia's navigation capabilities for a modern, cross-platform experience.

---

## Motivation

### The Problem

Current XerahS first-run behavior is minimal: users install the application and are dropped directly into the main interface with no guidance. The only first-run affordance is a one-time legacy ShareX config import button in DestinationSettings. This creates several critical UX gaps:

1. **Silent Failures**: Users often don't realize captures are being saved to `%TEMP%` until they can't find their screenshots later.

2. **Hotkey Conflicts**: Default hotkeys frequently clash with system or application shortcuts, leaving users wondering why "nothing happens" when they press Print Screen.

3. **Upload Confusion**: New users don't understand the distinction between local saves and cloud uploads, leading to accidental public uploads or frustration when sharing links don't work.

4. **OCR Friction**: The OCR feature appears broken out-of-the-box because no language packs are downloaded by default, with no indication of how to fix it.

5. **ShareX Migration Blindness**: Users coming from ShareX may not notice the import button or understand what will be imported.

### The Cost

Analytics from comparable tools (ShareX, Greenshot, Flameshot) suggest that **40-60% of new users never capture a single screenshot** in their first session. Of those who do, a significant portion encounter configuration issues that lead to negative reviews or uninstalls within 24 hours.

### The Opportunity

A well-designed onboarding wizard can:

- **Increase activation rate** by ensuring every user completes at least one successful capture in their first session
- **Reduce support burden** by surfacing configuration options proactively rather than reactively
- **Improve retention** by establishing correct defaults that match user intent
- **Differentiate XerahS** as the polished, user-friendly alternative to legacy tools

### Prior Art

**Yoink** (macOS) provides an excellent reference implementation with its `SetupWizard` and `InstallWizard`:
- Clean, single-purpose steps with clear progress indication
- Smart defaults that can be accepted with one click
- Ability to skip individual steps or the entire wizard
- Contextual help without overwhelming the user
- Completion celebration that leaves users feeling accomplished

---

## Detailed Design

### Wizard Architecture

The onboarding wizard will be implemented as a modal dialog flow using Avalonia's `TransitioningContentControl` for smooth step transitions. The architecture follows a state machine pattern with the following characteristics:

```
┌─────────────────────────────────────────────────────────────┐
│                    OnboardingWizard                         │
│  ┌─────────────────────────────────────────────────────┐   │
│  │         TransitioningContentControl                  │   │
│  │  ┌─────────┐   ┌─────────┐   ┌─────────┐            │   │
│  │  │ Step 1  │ → │ Step 2  │ → │ Step 3  │ → ...      │   │
│  │  │ Language│   │ Save    │   │ Hotkey  │            │   │
│  │  └─────────┘   └─────────┘   └─────────┘            │   │
│  └─────────────────────────────────────────────────────┘   │
│  [Back]  [Skip]                    [Next] / [Finish]       │
└─────────────────────────────────────────────────────────────┘
```

### Navigation Rules

1. **Back Button**: Enabled from Step 2 onwards. Returns to previous step, preserving user selections.
2. **Next Button**: Advances to next step. Validates current step before proceeding.
3. **Skip Button**: Available on every step. Skips current step and advances. Can skip entire wizard via "Skip All".
4. **Finish Button**: Appears on final step. Completes wizard and persists all settings.
5. **Progress Indicator**: Visual progress bar or step dots showing current position (e.g., "Step 3 of 5").

### Step Persistence

All selections are held in a transient `OnboardingState` object until the wizard completes. If the user cancels mid-wizard, no changes are persisted. On completion, all settings are committed atomically via `SettingsBase.SaveAsync()`.

---

## Proposed Steps

### Step 1: Welcome & Language Selection

**Purpose**: Set the tone and establish localization.

**Content**:
- Welcome message: "Welcome to XerahS — let's get you set up in under 2 minutes"
- Language dropdown (auto-detected from OS, fallback to English)
- Brief value proposition: "The modern screenshot tool that works everywhere"

**Smart Defaults**:
- Language pre-selected from `CultureInfo.CurrentUICulture`
- If exact match unavailable, fall back to language-only match (e.g., `pt-BR` → `pt`)

**Validation**:
- Language selection is optional (defaults to English)

**Edge Cases**:
- RTL languages: Ensure wizard UI itself respects RTL layout
- Missing translations: Fall back to English gracefully

---

### Step 2: Default Save Location

**Purpose**: Prevent the "where did my screenshots go?" problem.

**Content**:
- Directory picker with browse button
- Quick-select buttons for common locations:
  - Pictures/Screenshots (default)
  - Desktop
  - Documents
  - Custom...
- Checkbox: "Create subfolder with today's date" (default: true)
- Preview of path: `~/Pictures/Screenshots/2026-04-06/`

**Smart Defaults**:
- Primary: `Environment.GetFolderPath(SpecialFolder.MyPictures)/Screenshots`
- Fallback: User profile root if Pictures unavailable

**Validation**:
- Path must be writable (test write on Next)
- Create directory if it doesn't exist
- Warn if path is a network drive or removable media

**Edge Cases**:
- Path becomes unavailable after selection (handled at capture time, not here)
- Cloud sync folders (OneDrive, Dropbox): Show informational tooltip
- Long paths (>260 chars on Windows): Warn about potential issues

---

### Step 3: Hotkey Configuration

**Purpose**: Ensure users can actually trigger captures without conflicts.

**Content**:
- Visual hotkey recorder for primary capture hotkey
- Default suggestion: `Print Screen` (or `Cmd+Shift+5` on macOS)
- Conflict detection: Check against system hotkeys and common apps
- "Test your hotkey" button that triggers a test capture
- Secondary hotkeys (collapsed by default):
  - Region capture
  - Window capture
  - Fullscreen capture
  - Screen recording

**Smart Defaults**:
- Primary: `Print Screen`
- Region: `Ctrl+Print Screen` (or `Cmd+Shift+4` on macOS)
- Window: `Alt+Print Screen` (or `Cmd+Shift+4+Space` on macOS)

**Validation**:
- Detect conflicts with known system shortcuts
- Warn if hotkey is already bound by XerahS or detected common apps
- Require at least one capture hotkey to be set (can be same as default)

**Conflict Resolution UI**:
```
⚠️ Conflict Detected
"Print Screen" is currently bound to Windows Snipping Tool.
[X] Disable Windows Snipping Tool shortcut
[ ] Choose different hotkey
```

**Edge Cases**:
- Media keys: Filter out non-modifier keys that can't be used as hotkeys
- Accessibility: Ensure hotkey recorder is keyboard-navigable
- Wayland: Warn that global hotkeys require specific desktop environment support

---

### Step 4: Upload Destination

**Purpose**: Clarify the local vs. cloud distinction and set up sharing.

**Content**:
- Radio button selection:
  - **Local only** (default for privacy-conscious users)
  - **Imgur** (anonymous)
  - **Imgur** (authenticated)
  - **Custom uploader** (from XIP0024)
  - **More options...** (opens full DestinationSettings)
- For authenticated options: "Connect Account" button triggering OAuth flow
- Privacy note: "Your images are only uploaded when you explicitly choose 'Upload'"

**Smart Defaults**:
- Default: Local only (opt-in for uploads)
- If ShareX config imported with uploaders: Pre-select primary uploader

**Validation**:
- Test connection for authenticated uploaders before completing
- Anonymous Imgur: No validation needed (works without auth)

**Edge Cases**:
- No internet connection: Skip or show "Configure later" option
- OAuth flow interruption: Allow retry or skip
- Enterprise environments: Detect proxy settings, offer manual configuration

---

### Step 5: OCR Language Setup

**Purpose**: Ensure OCR works out-of-the-box for the user's language.

**Content**:
- Multi-select list of available OCR languages
- Pre-selected: Language from Step 1 + English
- Download size indicator per language (e.g., "English ~25 MB")
- "Download now" vs "Download in background" options
- Preview: "You'll be able to extract text from screenshots using your hotkey + T"

**Smart Defaults**:
- Primary: Match Step 1 language selection
- Secondary: English (as fallback)
- Only select, don't download until wizard completes (or background)

**Validation**:
- At least one language must be selected
- Warn if selected languages exceed ~200 MB total

**Edge Cases**:
- Low disk space: Warn and offer to skip
- Slow connection: Offer background download with notification on completion
- Language pack download failure: Retry logic, fallback to manual download instructions

---

### Step 6: Completion & First Capture

**Purpose**: Celebrate completion and drive immediate activation.

**Content**:
- Success animation/illustration
- Summary of configured settings (expandable)
- "Take your first screenshot" button (triggers region capture)
- Checkbox: "Show tips on startup" (default: true for first week)
- Link: "Open full settings" for power users

**Post-Wizard Actions**:
- Persist all settings to `ApplicationConfig.json`
- Call `MarkFirstTimeRunCompleted()`
- If "Take first screenshot" clicked: Trigger region capture immediately
- Schedule OCR language downloads if background option selected

---

## Technical Approach

### Integration with Existing Infrastructure

#### IsFirstTimeRun Detection

The wizard hooks into the existing `SettingsBase.IsFirstTimeRun` mechanism:

```csharp
// In App.axaml.cs or MainWindowViewModel
protected override async void OnInitialized()
{
    await base.OnInitialized();
    
    if (Settings.IsFirstTimeRun)
    {
        var wizard = new OnboardingWizard();
        var result = await wizard.ShowDialog<OnboardingResult>(this);
        
        if (result.Completed || result.Skipped)
        {
            Settings.MarkFirstTimeRunCompleted();
            await Settings.SaveAsync();
        }
    }
}
```

#### Settings Persistence

The wizard uses a transient state object that maps to existing settings:

```csharp
public class OnboardingState
{
    // Step 1: Language
    public string SelectedLanguage { get; set; } = "en";
    
    // Step 2: Save Location
    public string ScreenshotsFolder { get; set; }
    public bool CreateDateSubfolders { get; set; } = true;
    
    // Step 3: Hotkeys
    public HotkeyConfig PrimaryCaptureHotkey { get; set; }
    public List<HotkeyConfig> AdditionalHotkeys { get; set; } = new();
    
    // Step 4: Upload
    public UploaderConfig SelectedUploader { get; set; }
    
    // Step 5: OCR
    public List<string> SelectedOcrLanguages { get; set; } = new();
    public bool DownloadOcrInBackground { get; set; }
}
```

On completion, the state is committed:

```csharp
public async Task CommitSettingsAsync(OnboardingState state)
{
    // Step 1: Language
    Settings.Language = state.SelectedLanguage;
    
    // Step 2: Save Location
    Settings.ScreenshotsFolder = state.ScreenshotsFolder;
    Settings.CreateDateSubfolders = state.CreateDateSubfolders;
    
    // Step 3: Hotkeys
    HotkeyManager.SetPrimaryCapture(state.PrimaryCaptureHotkey);
    foreach (var hotkey in state.AdditionalHotkeys)
    {
        HotkeyManager.Register(hotkey);
    }
    
    // Step 4: Upload
    Settings.DefaultUploader = state.SelectedUploader;
    
    // Step 5: OCR (schedule downloads)
    if (state.DownloadOcrInBackground)
    {
        _ = Task.Run(() => OcrEngine.DownloadLanguagesAsync(state.SelectedOcrLanguages));
    }
    else
    {
        await OcrEngine.DownloadLanguagesAsync(state.SelectedOcrLanguages);
    }
    
    await Settings.SaveAsync();
}
```

### Avalonia UI Implementation

#### Project Structure

```
src/desktop/XerahS.Desktop/
├── Views/
│   └── Onboarding/
│       ├── OnboardingWizard.axaml         # Main container
│       ├── OnboardingWizard.axaml.cs
│       ├── Steps/
│       │   ├── WelcomeStep.axaml
│       │   ├── SaveLocationStep.axaml
│       │   ├── HotkeyStep.axaml
│       │   ├── UploadStep.axaml
│       │   ├── OcrStep.axaml
│       │   └── CompleteStep.axaml
│       └── Controls/
│           ├── HotkeyRecorder.axaml       # Reusable hotkey input
│           ├── ProgressIndicator.axaml
│           └── ConflictWarning.axaml
└── ViewModels/
    └── Onboarding/
        ├── OnboardingWizardViewModel.cs
        ├── OnboardingState.cs
        └── Steps/
            ├── WelcomeStepViewModel.cs
            ├── SaveLocationStepViewModel.cs
            ├── HotkeyStepViewModel.cs
            ├── UploadStepViewModel.cs
            ├── OcrStepViewModel.cs
            └── CompleteStepViewModel.cs
```

#### Navigation Pattern

```csharp
public class OnboardingWizardViewModel : ViewModelBase
{
    private readonly OnboardingState _state = new();
    private int _currentStepIndex;
    
    public ObservableCollection<StepViewModelBase> Steps { get; } = new()
    {
        new WelcomeStepViewModel(),
        new SaveLocationStepViewModel(),
        new HotkeyStepViewModel(),
        new UploadStepViewModel(),
        new OcrStepViewModel(),
        new CompleteStepViewModel()
    };
    
    public StepViewModelBase CurrentStep => Steps[_currentStepIndex];
    
    public bool CanGoBack => _currentStepIndex > 0;
    public bool CanGoNext => _currentStepIndex < Steps.Count - 1;
    public bool IsLastStep => _currentStepIndex == Steps.Count - 1;
    
    public void Next()
    {
        if (!CurrentStep.Validate()) return;
        CurrentStep.SaveToState(_state);
        
        if (IsLastStep)
        {
            CompleteWizard();
        }
        else
        {
            _currentStepIndex++;
            CurrentStep.LoadFromState(_state);
            OnPropertyChanged(nameof(CurrentStep));
        }
    }
    
    public void Skip()
    {
        // Mark step as skipped but continue
        CurrentStep.MarkSkipped();
        Next();
    }
    
    private async void CompleteWizard()
    {
        await CommitSettingsAsync(_state);
        Close(new OnboardingResult { Completed = true });
    }
}
```

### Platform-Specific Considerations

#### Windows
- Hotkey conflict detection via `RegisterHotKey` API (test before setting)
- Shell integration: Option to pin to taskbar/start menu on completion step

#### macOS
- Hotkey conflict detection limited (macOS doesn't expose global hotkey registry)
- Accessibility permissions: Prompt for screen recording permission on first capture attempt

#### Linux
- Wayland: Hotkey configuration may be limited; show informational message
- Portal integration: Use `xdg-desktop-portal` for file picker if available

---

## Compatibility & Edge Cases

### Upgrade vs. Fresh Install

| Scenario | Behavior |
|----------|----------|
| Fresh install (no `ApplicationConfig.json`) | Show wizard on first launch |
| Upgrade (existing config, `IsFirstTimeRun=false`) | Skip wizard entirely |
| Upgrade (existing config, `IsFirstTimeRun=true` from beta) | Show wizard with existing settings pre-populated |
| Portable mode | Same behavior, store config relative to executable |

### Import from ShareX

The existing ShareX import functionality will be integrated into Step 4 (Upload Destination):

```csharp
public class UploadStepViewModel : StepViewModelBase
{
    public bool HasShareXConfig => ShareXImporter.DetectConfig();
    
    [RelayCommand]
    private async Task ImportFromShareX()
    {
        var result = await ShareXImporter.ImportAsync();
        if (result.Success)
        {
            // Pre-populate uploaders from imported config
            AvailableUploaders.AddRange(result.Uploaders);
            SelectedUploader = result.PrimaryUploader;
        }
    }
}
```

### Skipping Behavior

| Skip Action | Result |
|-------------|--------|
| Skip single step | Step uses defaults, continues to next |
| "Skip All" button | All steps use defaults, wizard closes, `IsFirstTimeRun` marked complete |
| Close window (X) | Wizard cancels, no settings changed, `IsFirstTimeRun` remains true (will show again on next launch) |

### Enterprise/MDM Environments

Support for enterprise deployment via configuration files:

```json
// deployment-config.json
{
  "onboarding": {
    "enabled": false,           // Skip wizard entirely
    "screenshotsFolder": "\\\server\share\screenshots",
    "language": "en",
    "uploaders": ["none"],
    "disableOcr": true
  }
}
```

If `onboarding.enabled` is false, the wizard is suppressed regardless of `IsFirstTimeRun`.

### Accessibility

- All steps keyboard-navigable (Tab order, Enter to advance)
- Screen reader announcements for progress changes
- High contrast theme support
- Minimum 4.5:1 contrast ratio for all text
- Focus indicators visible on all interactive elements

---

## Acceptance Criteria

### Functional Requirements

1. **Wizard Trigger**: Wizard appears automatically on first launch when `IsFirstTimeRun` is true
2. **Step Completion**: User can complete all 5 configuration steps in sequence
3. **Settings Persistence**: All configured settings are saved to `ApplicationConfig.json` on completion
4. **First Capture**: User can trigger a test capture directly from the completion step
5. **Skip Support**: User can skip individual steps or the entire wizard
6. **Back Navigation**: User can return to previous steps to change selections
7. **Validation**: Invalid configurations are blocked with clear error messages
8. **Conflict Detection**: Hotkey conflicts are detected and surfaced to the user

### UX Requirements

1. **Completion Time**: Average user completes wizard in under 2 minutes
2. **Skip Rate**: Less than 20% of users skip the entire wizard
3. **Error Rate**: Less than 5% of users encounter validation errors
4. **First Capture**: 80%+ of users who complete wizard take a screenshot within 5 minutes
5. **Accessibility**: Wizard passes WCAG 2.1 AA standards

### Technical Requirements

1. **Performance**: Wizard loads in under 500ms on recommended hardware
2. **Memory**: Wizard adds less than 50MB RAM usage during operation
3. **Cross-Platform**: All steps functional on Windows, macOS, and Linux
4. **Localization**: Wizard UI itself is localizable (at minimum: EN, DE, FR, ES, JA, ZH)
5. **Test Coverage**: 80%+ unit test coverage for ViewModels, integration tests for full flow

### Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Wizard completion rate | >70% | % of first-run users who complete all steps |
| Time to first capture | <3 min | Median time from app launch to first screenshot |
| Support tickets (config) | -50% | Reduction in "where are my screenshots" tickets |
| Day-1 retention | +20% | % of users who return within 24 hours |
| App Store rating | +0.5 stars | Improvement in "ease of use" category |

---

## Implementation Phases

### Phase 1: Core Framework (Week 1-2)
- Create `OnboardingWizard` shell with navigation
- Implement `OnboardingState` and settings persistence
- Build progress indicator and navigation controls

### Phase 2: Essential Steps (Week 3-4)
- Step 1: Welcome & Language
- Step 2: Save Location
- Step 3: Hotkey Configuration

### Phase 3: Advanced Steps (Week 5-6)
- Step 4: Upload Destination (integrate with existing uploader UI)
- Step 5: OCR Language Setup
- Step 6: Completion

### Phase 4: Polish & Integration (Week 7-8)
- Hotkey conflict detection
- ShareX import integration
- Accessibility audit
- Localization

### Phase 5: Testing & Release (Week 9-10)
- Cross-platform testing
- User testing with 5-10 participants
- Analytics instrumentation
- Documentation

---

## References

### Yoink Reference

Yoink's `SetupWizard` implementation provides excellent UX patterns:
- **Progressive disclosure**: Only show advanced options when requested
- **Smart defaults**: Pre-select options based on system detection
- **Immediate feedback**: Test buttons that validate configuration in real-time
- **Graceful exit**: Users can skip without penalty, but are encouraged to complete

Source: [Yoink Mac App Store](https://apps.apple.com/us/app/yoink/id457622435) — observe the onboarding flow on first launch.

### Related XIPs

- **XIP0012**: Import ShareX uploaders config — Upload destination step builds on this
- **XIP0024**: Custom uploader integration — Upload step UI should accommodate custom uploaders
- **XIP0026**: Task settings UX redesign — Wizard should align with new settings patterns
- **XIP0045**: ShareX config import runtime migration — Wizard replaces the one-time import button

### External References

- [Avalonia Dialogs Documentation](https://docs.avaloniaui.net/docs/controls/window)
- [Fluent Design Onboarding Patterns](https://learn.microsoft.com/en-us/windows/apps/design/ux-templates/onboarding)
- [Apple Human Interface Guidelines: Onboarding](https://developer.apple.com/design/human-interface-guidelines/patterns/onboarding)

---

## Open Questions

1. **Should we A/B test step order?** The proposed order (Language → Save → Hotkey → Upload → OCR) prioritizes essential configuration, but we may want to test placing OCR earlier for users who specifically downloaded for text extraction.

2. **Video tutorial integration?** Should Step 1 include an optional 30-second video demonstrating XerahS capabilities?

3. **Cloud sync for settings?** Should we offer to sync wizard selections to cloud for multi-device setup?

4. **Post-wizard tips?** After wizard completion, should we show contextual tips (e.g., "Did you know you can drag files to the tray icon?") for the first week?

---

## Non-Negotiable Rules

1. **Never block the user**: Skip option always available, close button always works
2. **Preserve existing settings**: If user has partial config, pre-populate and don't overwrite
3. **Test before finish**: Every configuration should be testable before completing the wizard
4. **Respect privacy**: Upload configuration defaults to local-only; cloud upload requires explicit opt-in
5. **Work offline**: Wizard must function without internet connection (OCR languages download later)
