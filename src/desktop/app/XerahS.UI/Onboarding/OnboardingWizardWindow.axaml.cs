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

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Onboarding.ViewModels.Steps;
using XerahS.UI.Views;

namespace XerahS.UI.Onboarding;

/// <summary>
/// The main onboarding wizard window.
/// </summary>
public partial class OnboardingWizardWindow : Window
{
    public OnboardingWizardViewModel ViewModel { get; }

    public OnboardingWizardWindow()
    {
        ViewModel = new OnboardingWizardViewModel();
        InitializeComponent();
        DataContext = ViewModel;

        // Set up callbacks for steps that need them
        SetupStepCallbacks();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupStepCallbacks()
    {
        // Save location step - folder browser using Avalonia StorageProvider
        if (ViewModel.Steps.ElementAtOrDefault(1) is SaveLocationStepViewModel saveStep)
        {
            saveStep.BrowseFolderCallback = async () =>
            {
                var storageProvider = StorageProvider;
                if (storageProvider == null) return null;

                var folders = await storageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select Screenshots Folder",
                    AllowMultiple = false
                });

                return folders.FirstOrDefault()?.Path.LocalPath;
            };
        }

        // Hotkey step - test capture
        if (ViewModel.Steps.ElementAtOrDefault(2) is HotkeyStepViewModel hotkeyStep)
        {
            hotkeyStep.TestCaptureCallback = async () =>
            {
                try
                {
                    // Trigger a test region capture via the workflow orchestrator
                    if (Avalonia.Application.Current is App app && app.WorkflowManager != null)
                    {
                        var workflows = app.WorkflowManager.Workflows;
                        var regionWorkflow = workflows.FirstOrDefault(w => w.Job == Core.Hotkeys.WorkflowType.RectangleRegion);

                        if (regionWorkflow != null)
                        {
                            DebugHelper.WriteLine("[Onboarding] Triggering test region capture");
                            // Trigger capture if we have an orchestrator
                            // The actual capture triggering would be done via the task manager
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "[Onboarding] Failed to trigger test capture");
                }
            };
        }

        // Upload step - ShareX import
        if (ViewModel.Steps.ElementAtOrDefault(3) is UploadStepViewModel uploadStep)
        {
            uploadStep.ImportShareXCallback = async () =>
            {
                try
                {
                    // Detect ShareX config in standard location
                    var shareXPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "ShareX",
                        "ApplicationConfig.json");

                    if (System.IO.File.Exists(shareXPath))
                    {
                        DebugHelper.WriteLine($"[Onboarding] ShareX config found at {shareXPath}, import not yet implemented");
                        // ShareX import would be implemented here using the ShareXImporter
                        // For now, return false to indicate import wasn't done
                        return false;
                    }

                    DebugHelper.WriteLine("[Onboarding] No ShareX config found");
                    return false;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "[Onboarding] Failed to import ShareX config");
                    return false;
                }
            };
        }

        // Complete step - take first screenshot and open settings
        if (ViewModel.Steps.ElementAtOrDefault(5) is CompleteStepViewModel completeStep)
        {
            completeStep.TakeFirstScreenshotCallback = async () =>
            {
                try
                {
                    DebugHelper.WriteLine("[Onboarding] Take first screenshot requested");
                    // Close this wizard first
                    Close();

                    // Trigger region capture via the main window
                    if (Avalonia.Application.Current is App app)
                    {
                        // The actual triggering would be done via the workflow orchestrator
                        // For now, the workflow is set up and will respond to hotkey
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "[Onboarding] Failed to take first screenshot");
                }
            };

            completeStep.OpenSettingsCallback = () =>
            {
                try
                {
                    DebugHelper.WriteLine("[Onboarding] Open settings requested");
                    // Navigate to settings in the main window
                    if (Avalonia.Application.Current is App app &&
                        Avalonia.Controls.Application.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        if (desktop.MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.NavigateToSettings();
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "[Onboarding] Failed to open settings");
                }
            };
        }
    }

    /// <summary>
    /// Shows the wizard as a modal dialog and returns the result.
    /// </summary>
    public async Task<OnboardingResult> ShowDialogAsync(Window owner)
    {
        var tcs = new TaskCompletionSource<OnboardingResult>();

        // Subscribe to completion
        _ = ViewModel.CompletionTask.ContinueWith(task =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Close();
                if (!tcs.TrySetResult(task.Result))
                {
                    // If result was already set (e.g., by cancel), try to get it from the source
                    tcs.TrySetResult(task.Result);
                }
            });
        }, TaskScheduler.Default);

        // Handle window closing (cancel)
        Closing += (sender, e) =>
        {
            if (!ViewModel.CompletionTask.IsCompleted)
            {
                ViewModel.Cancel();
            }
        };

        await ShowDialog(owner);
        return await tcs.Task;
    }
}