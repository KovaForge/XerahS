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
using XerahS.Uploaders.PluginSystem;

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
    private int _currentStepIndex = -1;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _isLastStep;

    [ObservableProperty]
    private string _nextButtonText = "Next";

    public bool CanSkipAll => true;

    public bool HasCurrentStep => CurrentStep != null;

    public int CurrentStepDisplayIndex => CurrentStepIndex >= 0 ? CurrentStepIndex + 1 : 0;

    public OnboardingState State { get; } = new();

    public Task<OnboardingResult> CompletionTask => _completionSource.Task;

    public OnboardingWizardViewModel()
    {
        InitializeSteps();
        CurrentStepIndex = 0;
    }

    private void InitializeSteps()
    {
        Steps.Add(new WelcomeStepViewModel());
        Steps.Add(new SaveLocationStepViewModel());
        Steps.Add(new HotkeyStepViewModel());
        Steps.Add(new UploadStepViewModel());
        Steps.Add(new OcrStepViewModel());
        Steps.Add(new CompleteStepViewModel());

        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].StepIndex = i;
        }
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        CurrentStep = value >= 0 && value < Steps.Count ? Steps[value] : null;
        OnPropertyChanged(nameof(CurrentStepDisplayIndex));
        UpdateNavigationState();
    }

    partial void OnCurrentStepChanged(StepViewModelBase? value)
    {
        OnPropertyChanged(nameof(HasCurrentStep));

        if (value != null)
        {
            value.LoadFromState(State);
        }
    }

    private void UpdateNavigationState()
    {
        CanGoBack = CurrentStepIndex > 0;
        IsLastStep = Steps.Count > 0 && CurrentStepIndex == Steps.Count - 1;
        NextButtonText = IsLastStep ? "Done" : "Next";
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep == null)
        {
            return;
        }

        if (!CurrentStep.Validate())
        {
            return;
        }

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
        if (CurrentStepIndex <= 0)
        {
            return;
        }

        SaveCurrentStepToState();
        CurrentStepIndex--;
    }

    [RelayCommand]
    private void Skip()
    {
        if (CurrentStep == null || !CurrentStep.CanSkip)
        {
            return;
        }

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

    public Task CompleteAsync()
    {
        return CompleteWizardAsync();
    }

    private async Task CompleteWizardAsync()
    {
        if (_completionSource.Task.IsCompleted)
        {
            return;
        }

        SaveCurrentStepToState();
        State.LastCompletedStepIndex = CurrentStepIndex;

        await CommitSettingsAsync(State);

        OnboardingResult result = new()
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
            IReadOnlyDictionary<UploaderCategory, UploaderInstance>? selectedUploaderInstances = null;

            if (!string.IsNullOrEmpty(state.SelectedUploaderId) &&
                !state.SkippedSteps.Contains(2) &&
                !string.Equals(state.SelectedUploaderId, "local", StringComparison.OrdinalIgnoreCase))
            {
                selectedUploaderInstances = OnboardingFileUploaderHelper.EnsureFileUploaderInstances(state.SelectedUploaderId);
            }

            if (!string.IsNullOrEmpty(state.ScreenshotsFolder) && !state.SkippedSteps.Contains(0))
            {
                SettingsManager.Settings.CustomScreenshotsPath = state.ScreenshotsFolder;
                SettingsManager.Settings.UseCustomScreenshotsPath = true;
                SettingsManager.Settings.SaveImageSubFolderPattern = state.CreateDateSubfolders ? "%y-%mo-%d" : string.Empty;
                DebugHelper.WriteLine($"[OnboardingWizard] Setting screenshots folder: {state.ScreenshotsFolder}");
            }

            if (state.PrimaryCaptureHotkey != null && !state.SkippedSteps.Contains(1))
            {
                WorkflowManager? workflowManager = GetWorkflowManager();
                if (workflowManager != null)
                {
                    WorkflowSettings primaryWorkflow = workflowManager.Workflows
                        .FirstOrDefault(workflow => workflow.Job == WorkflowType.RectangleRegion)
                        ?? new WorkflowSettings(WorkflowType.RectangleRegion, state.PrimaryCaptureHotkey);

                    primaryWorkflow.HotkeyInfo = state.PrimaryCaptureHotkey;
                    primaryWorkflow.EnsureId();
                    ApplyOnboardingDestination(primaryWorkflow, selectedUploaderInstances);

                    if (!workflowManager.Workflows.Contains(primaryWorkflow))
                    {
                        workflowManager.Workflows.Add(primaryWorkflow);
                    }

                    IReadOnlyList<WorkflowType> secondaryJobs = GetSecondaryOnboardingWorkflowJobs();

                    for (int i = 0; i < Math.Min(state.AdditionalHotkeys.Count, secondaryJobs.Count); i++)
                    {
                        HotkeyInfo hotkey = state.AdditionalHotkeys[i];
                        WorkflowType job = secondaryJobs[i];

                        WorkflowSettings workflow = workflowManager.Workflows
                            .FirstOrDefault(existingWorkflow => existingWorkflow.Job == job && existingWorkflow != primaryWorkflow)
                            ?? new WorkflowSettings(job, hotkey);

                        workflow.HotkeyInfo = hotkey;
                        workflow.EnsureId();
                        ApplyOnboardingDestination(workflow, selectedUploaderInstances);

                        if (!workflowManager.Workflows.Contains(workflow))
                        {
                            workflowManager.Workflows.Add(workflow);
                        }
                    }

                    workflowManager.UpdateHotkeys(workflowManager.Workflows);
                }
            }

            if (!string.IsNullOrEmpty(state.SelectedUploaderId) && !state.SkippedSteps.Contains(2))
            {
                DebugHelper.WriteLine($"[OnboardingWizard] Setting upload destination: {state.SelectedUploaderId}");
            }

            if (state.SelectedOcrLanguages.Count > 0 && !state.SkippedSteps.Contains(4))
            {
                string primaryOcrLanguage = state.SelectedOcrLanguages[0];
                var ocrOptions = SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions;
                ocrOptions.Language = primaryOcrLanguage;
                // Persist the full selection so it survives for a future
                // multi-language picker. Today the OCR runtime is single-
                // language per RecognizeAsync call, so the tool uses
                // OCROptions.Language; the full list is metadata that must
                // not be silently dropped.
                ocrOptions.PreferredLanguages = new List<string>(state.SelectedOcrLanguages);
                DebugHelper.WriteLine($"[OnboardingWizard] Setting primary OCR language: {primaryOcrLanguage} (preferred: {state.SelectedOcrLanguages.Count})");
            }

            SettingsManager.Settings.MarkFirstTimeRunCompleted(persist: false);
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

    private static void ApplyOnboardingDestination(
        WorkflowSettings workflow,
        IReadOnlyDictionary<UploaderCategory, UploaderInstance>? selectedUploaderInstances)
    {
        if (selectedUploaderInstances == null)
        {
            return;
        }

        string category = workflow.Job.GetHotkeyCategory();
        UploaderCategory? destinationCategory = category switch
        {
            EnumExtensions.WorkflowType_Category_ScreenCapture => UploaderCategory.File,
            EnumExtensions.WorkflowType_Category_ScreenRecord => UploaderCategory.File,
            EnumExtensions.WorkflowType_Category_Upload => UploaderCategory.File,
            EnumExtensions.WorkflowType_Category_Tools => workflow.Job == WorkflowType.OCR ? UploaderCategory.Text : UploaderCategory.File,
            _ => null
        };

        if (destinationCategory.HasValue &&
            selectedUploaderInstances.TryGetValue(destinationCategory.Value, out UploaderInstance? instance))
        {
            workflow.TaskSettings.SetDestinationInstanceId(workflow.Job, instance.InstanceId);
        }
    }

    internal static IReadOnlyList<WorkflowType> GetSecondaryOnboardingWorkflowJobs()
    {
        return
        [
            WorkflowType.ActiveWindow,
            WorkflowType.PrintScreen,
            WorkflowType.OCR
        ];
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
        OnboardingResult result = new()
        {
            Completed = false,
            Skipped = false,
            State = State
        };

        _completionSource.TrySetResult(result);
    }
}
