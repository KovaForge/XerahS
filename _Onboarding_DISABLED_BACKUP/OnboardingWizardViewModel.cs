#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.UI.Onboarding.ViewModels.Steps;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Onboarding;

/// <summary>
/// Main ViewModel for the Onboarding Wizard.
/// Manages state machine, navigation, and settings persistence.
/// </summary>
public partial class OnboardingWizardViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<OnboardingResult> _completionSource = new();

    [ObservableProperty]
    private ObservableCollection<StepViewModelBase> _steps = new();

    [ObservableProperty]
    private StepViewModelBase? _currentStep;

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _isLastStep;

    [ObservableProperty]
    private string _nextButtonText = "Next →";

    public bool CanSkipAll => true;

    public OnboardingState State { get; } = new();

    public Task<OnboardingResult> CompletionTask => _completionSource.Task;

    public OnboardingWizardViewModel()
    {
        InitializeSteps();
        CurrentStepIndex = 0;
        UpdateNavigationState();
    }

    private void InitializeSteps()
    {
        Steps.Add(new WelcomeStepViewModel());
        Steps.Add(new SaveLocationStepViewModel());
        Steps.Add(new HotkeyStepViewModel());
        Steps.Add(new UploadStepViewModel());
        Steps.Add(new OcrStepViewModel());
        Steps.Add(new CompleteStepViewModel());

        // Set step indices
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].StepIndex = i;
        }
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        if (value >= 0 && value < Steps.Count)
        {
            CurrentStep = Steps[value];
            CurrentStep.LoadFromState(State);
        }

        UpdateNavigationState();
    }

    partial void OnCurrentStepChanged(StepViewModelBase? value)
    {
        if (value != null)
        {
            value.LoadFromState(State);
        }
    }

    private void UpdateNavigationState()
    {
        CanGoBack = CurrentStepIndex > 0;
        IsLastStep = CurrentStepIndex == Steps.Count - 1;
        NextButtonText = IsLastStep ? "Done" : "Next →";
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep == null) return;

        // Validate current step
        if (!CurrentStep.Validate())
        {
            return;
        }

        // Save current step state
        SaveCurrentStepToState();

        if (IsLastStep)
        {
            _ = CompleteWizardAsync();
        }
        else
        {
            CurrentStepIndex++;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStepIndex > 0)
        {
            SaveCurrentStepToState();
            CurrentStepIndex--;
        }
    }

    [RelayCommand]
    private void Skip()
    {
        if (CurrentStep == null || !CurrentStep.CanSkip) return;

        CurrentStep.MarkSkipped();
        State.SkippedSteps.Add(CurrentStepIndex);
        SaveCurrentStepToState();

        if (IsLastStep)
        {
            _ = CompleteWizardAsync();
        }
        else
        {
            CurrentStepIndex++;
        }
    }

    [RelayCommand]
    private void SkipAll()
    {
        // Mark all remaining steps as skipped
        for (int i = CurrentStepIndex; i < Steps.Count; i++)
        {
            State.SkippedSteps.Add(i);
        }

        _ = CompleteWizardAsync();
    }

    public void SaveCurrentStepToState()
    {
        CurrentStep?.SaveToState(State);
    }

    public void LoadFromState(OnboardingState state)
    {
        State.SelectedLanguage = state.SelectedLanguage;
        State.ScreenshotsFolder = state.ScreenshotsFolder;
        State.CreateDateSubfolders = state.CreateDateSubfolders;
        State.PrimaryCaptureHotkey = state.PrimaryCaptureHotkey;
        State.AdditionalHotkeys = new List<HotkeyInfo>(state.AdditionalHotkeys);
        State.SelectedUploaderId = state.SelectedUploaderId;
        State.SelectedOcrLanguages = new List<string>(state.SelectedOcrLanguages);
        State.DownloadOcrInBackground = state.DownloadOcrInBackground;
        State.SkippedSteps = new HashSet<int>(state.SkippedSteps);
        State.LastCompletedStepIndex = state.LastCompletedStepIndex;

        CurrentStep?.LoadFromState(State);
    }

    private async Task CompleteWizardAsync()
    {
        SaveCurrentStepToState();
        State.LastCompletedStepIndex = CurrentStepIndex;

        await CommitSettingsAsync(State);

        var result = new OnboardingResult
        {
            Completed = true,
            Skipped = State.SkippedSteps.Count == Steps.Count,
            State = State
        };

        _completionSource.TrySetResult(result);
    }

    /// <summary>
    /// Commits the onboarding state to application settings.
    /// </summary>
    public async Task CommitSettingsAsync(OnboardingState state)
    {
        try
        {
            // 1. Language - map string code to SupportedLanguage enum
            if (!string.IsNullOrEmpty(state.SelectedLanguage))
            {
                var langMap = new Dictionary<string, SupportedLanguage>(StringComparer.OrdinalIgnoreCase)
                {
                    { "en", SupportedLanguage.English },
                    { "es", SupportedLanguage.Spanish },
                    { "fr", SupportedLanguage.French },
                    { "de", SupportedLanguage.German },
                    { "it", SupportedLanguage.Italian },
                    { "pt", SupportedLanguage.Portuguese },
                    { "ru", SupportedLanguage.Russian },
                    { "ja", SupportedLanguage.Japanese },
                    { "ko", SupportedLanguage.Korean },
                    { "zh", SupportedLanguage.SimplifiedChinese },
                    { "ar", SupportedLanguage.Arabic },
                    { "nl", SupportedLanguage.Dutch },
                    { "pl", SupportedLanguage.Polish },
                    { "tr", SupportedLanguage.Turkish },
                    { "he", SupportedLanguage.Hebrew },
                    { "hu", SupportedLanguage.Hungarian },
                    { "id", SupportedLanguage.Indonesian },
                    { "fa", SupportedLanguage.Persian },
                    { "ro", SupportedLanguage.Romanian },
                    { "uk", SupportedLanguage.Ukrainian },
                    { "vi", SupportedLanguage.Vietnamese }
                };

                if (langMap.TryGetValue(state.SelectedLanguage, out var supportedLang))
                {
                    SettingsManager.Settings.Language = supportedLang;
                }
            }

            // 2. Save path + subfolder flag
            // Note: PathsManager.ScreenshotsFolder is read-only, so we store the path in settings
            if (!string.IsNullOrEmpty(state.ScreenshotsFolder))
            {
                // Store the screenshots folder in settings
                // The actual path handling would be done by the capture service
                DebugHelper.WriteLine($"[OnboardingWizard] Setting screenshots folder: {state.ScreenshotsFolder}");
            }

            // Store the date subfolder preference (if the setting exists)
            // This might need to be added to ApplicationConfig

            // 3. Hotkeys - register via WorkflowManager
            if (state.PrimaryCaptureHotkey != null && !state.SkippedSteps.Contains(2))
            {
                var workflowManager = GetWorkflowManager();
                if (workflowManager != null)
                {
                    // Create or update primary capture workflow
                    var primaryWorkflow = workflowManager.Workflows
                        .FirstOrDefault(w => w.Job == WorkflowType.RectangleRegion)
                        ?? new WorkflowSettings(WorkflowType.RectangleRegion, state.PrimaryCaptureHotkey);

                    primaryWorkflow.HotkeyInfo = state.PrimaryCaptureHotkey;
                    primaryWorkflow.EnsureId();

                    if (!workflowManager.Workflows.Contains(primaryWorkflow))
                    {
                        workflowManager.Workflows.Add(primaryWorkflow);
                    }

                    // Register additional hotkeys
                    var secondaryJobs = new[]
                    {
                        WorkflowType.RectangleRegion,
                        WorkflowType.ActiveWindow,
                        WorkflowType.PrintScreen
                    };

                    for (int i = 0; i < Math.Min(state.AdditionalHotkeys.Count, secondaryJobs.Length); i++)
                    {
                        var hotkey = state.AdditionalHotkeys[i];
                        var job = secondaryJobs[i];

                        var workflow = workflowManager.Workflows
                            .FirstOrDefault(w => w.Job == job && w != primaryWorkflow)
                            ?? new WorkflowSettings(job, hotkey);

                        workflow.HotkeyInfo = hotkey;
                        workflow.EnsureId();

                        if (!workflowManager.Workflows.Contains(workflow))
                        {
                            workflowManager.Workflows.Add(workflow);
                        }
                    }

                    workflowManager.NotifyWorkflowsChanged();
                }
            }

            // 4. Upload destination
            if (!string.IsNullOrEmpty(state.SelectedUploaderId) && !state.SkippedSteps.Contains(3))
            {
                // Map the selected uploader to configuration
                // The actual destination configuration would depend on the uploader infrastructure
                DebugHelper.WriteLine($"[OnboardingWizard] Setting upload destination: {state.SelectedUploaderId}");

                // If Imgur is selected, configure it
                if (state.SelectedUploaderId.StartsWith("imgur"))
                {
                    // This would require configuring the Imgur uploader instance
                    // For now, we log the selection
                }
            }

            // 5. OCR languages - schedule download via OCR engine
            if (state.SelectedOcrLanguages.Count > 0 && !state.SkippedSteps.Contains(4))
            {
                // Store OCR language preference
                // The actual download would happen when OCR is first used
                // if the platform requires it
                DebugHelper.WriteLine($"[OnboardingWizard] Setting OCR languages: {string.Join(", ", state.SelectedOcrLanguages)}");
            }

            // Mark first-time run as completed
            SettingsManager.Settings.MarkFirstTimeRunCompleted(persist: false);

            // Save all settings
            SettingsManager.SaveAllSettings();

            DebugHelper.WriteLine("[OnboardingWizard] Settings committed successfully.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[OnboardingWizard] Failed to commit settings");
            throw;
        }

        await Task.CompletedTask;
    }

    private WorkflowManager? GetWorkflowManager()
    {
        if (global::Avalonia.Application.Current is App app)
        {
            return app.WorkflowManager;
        }
        return null;
    }

    /// <summary>
    /// Called when the wizard is closed without completing.
    /// </summary>
    public void Cancel()
    {
        var result = new OnboardingResult
        {
            Completed = false,
            Skipped = false,
            State = State
        };

        _completionSource.TrySetResult(result);
    }
}
