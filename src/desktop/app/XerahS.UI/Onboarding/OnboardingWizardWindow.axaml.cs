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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.UI.Onboarding.ViewModels.Steps;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Onboarding;

/// <summary>
/// The main onboarding wizard window.
/// </summary>
public partial class OnboardingWizardWindow : Window
{
    private bool _openSettingsAfterClose;
    private bool _takeFirstScreenshotAfterClose;

    public OnboardingWizardViewModel ViewModel { get; }

    public OnboardingWizardWindow()
    {
        ViewModel = new OnboardingWizardViewModel();
        InitializeComponent();
        DataContext = ViewModel;

        SetupStepCallbacks();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private T? GetStep<T>() where T : StepViewModelBase =>
        ViewModel.Steps.OfType<T>().FirstOrDefault();

    private void SetupStepCallbacks()
    {
        if (GetStep<SaveLocationStepViewModel>() is { } saveStep)
        {
            saveStep.BrowseFolderCallback = PickFolderAsync;
        }

        if (GetStep<HotkeyStepViewModel>() is { } hotkeyStep)
        {
            hotkeyStep.TestCaptureCallback = ExecuteRegionCaptureAsync;
        }

        if (GetStep<UploadStepViewModel>() is { } uploadStep)
        {
            uploadStep.ConfigureUploaderCallback = ConfigureUploaderAsync;
        }

        if (GetStep<CompleteStepViewModel>() is { } completeStep)
        {
            completeStep.TakeFirstScreenshotCallback = async () =>
            {
                try
                {
                    DebugHelper.WriteLine("[Onboarding] Take first screenshot requested");
                    _takeFirstScreenshotAfterClose = true;
                    await ViewModel.CompleteAsync();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "[Onboarding] Failed to take first screenshot");
                }
            };

            completeStep.OpenSettingsCallback = async () =>
            {
                try
                {
                    DebugHelper.WriteLine("[Onboarding] Open settings requested");
                    _openSettingsAfterClose = true;
                    await ViewModel.CompleteAsync();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "[Onboarding] Failed to open settings");
                }
            };
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        IStorageProvider? storageProvider = StorageProvider;
        if (storageProvider == null)
        {
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select Screenshots Folder",
                AllowMultiple = false,
            });

        if (folders.Count == 0)
        {
            return null;
        }

        try
        {
            return folders[0].TryGetLocalPath();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[Onboarding] Failed to resolve selected folder path");
            return null;
        }
    }

    private async Task ConfigureUploaderAsync(UploaderOption option)
    {
        UploaderInstance instance = OnboardingFileUploaderHelper.EnsureFileUploaderInstance(option.Id);
        UploaderInstanceViewModel instanceViewModel = new(instance);

        object configView = instanceViewModel.ConfigView ?? new TextBlock
        {
            Text = "This file uploader does not expose a configuration view.",
            TextWrapping = TextWrapping.Wrap
        };

        OnboardingUploaderConfigDialogViewModel dialogViewModel = new(instanceViewModel.DisplayName, configView);
        OnboardingUploaderConfigDialog dialog = new()
        {
            DataContext = dialogViewModel
        };

        dialogViewModel.CloseRequested = dialog.Close;
        await dialog.ShowDialog(this);

        OnboardingFileUploaderHelper.EnsureFileUploaderInstances(
            option.Id,
            instance.SettingsJson,
            updateExistingSupportedCategories: true);
    }

    private async Task ExecuteRegionCaptureAsync()
    {
        WorkflowSettings workflow = GetRegionCaptureWorkflow();

        WindowState previousWindowState = WindowState;
        bool wasVisible = IsVisible;

        try
        {
            if (wasVisible)
            {
                WindowState = WindowState.Minimized;
                await Task.Delay(150);
            }

            await XerahS.Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id, hideMainWindow: true);
        }
        finally
        {
            if (wasVisible)
            {
                WindowState = previousWindowState;
                Activate();
            }
        }
    }

    private static WorkflowSettings GetRegionCaptureWorkflow()
    {
        WorkflowSettings? workflow = null;

        if (Application.Current is App app)
        {
            workflow = app.WorkflowManager?.Workflows.FirstOrDefault(w => w.Job == WorkflowType.RectangleRegion);
        }

        workflow ??= SettingsManager.GetFirstWorkflow(WorkflowType.RectangleRegion);
        workflow ??= new WorkflowSettings(WorkflowType.RectangleRegion, new XerahS.Platform.Abstractions.HotkeyInfo());
        workflow.TaskSettings.Job = WorkflowType.RectangleRegion;
        workflow.EnsureId();

        return workflow;
    }

    private void OnOnboardingHotkeyChanged(object? sender, EventArgs e)
    {
        if (sender is Control { DataContext: HotkeyItemViewModel hotkeyItem })
        {
            hotkeyItem.Refresh();
        }

        if (ViewModel.CurrentStep is HotkeyStepViewModel hotkeyStep)
        {
            hotkeyStep.RefreshFromHotkeyItems();
        }
    }

    /// <summary>
    /// Shows the wizard as a modal dialog and returns the result.
    /// </summary>
    public async Task<OnboardingResult> ShowDialogAsync(Window owner)
    {
        Task<OnboardingResult> completionTask = ViewModel.CompletionTask;
        _ = completionTask.ContinueWith(task =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Close);
        }, TaskScheduler.Default);

        Closing += (sender, e) =>
        {
            if (!ViewModel.CompletionTask.IsCompleted)
            {
                ViewModel.Cancel();
            }
        };

        await ShowDialog(owner);

        if (_takeFirstScreenshotAfterClose)
        {
            await ExecuteRegionCaptureAsync();
        }

        if (_openSettingsAfterClose &&
            Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToSettings();
        }

        return await completionTask;
    }
}
