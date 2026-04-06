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
using Avalonia.Markup.Xaml;
using XerahS.UI.Onboarding.ViewModels.Steps;

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
        // Save location step - folder browser
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
                // Trigger a test region capture
                // This would be wired up to the actual capture service
                await Task.CompletedTask;
            };
        }

        // Upload step - ShareX import
        if (ViewModel.Steps.ElementAtOrDefault(3) is UploadStepViewModel uploadStep)
        {
            uploadStep.ImportShareXCallback = async () =>
            {
                // Import from ShareX
                // This would be implemented with actual ShareX import logic
                await Task.CompletedTask;
                return false;
            };
        }

        // Complete step - take first screenshot and open settings
        if (ViewModel.Steps.ElementAtOrDefault(5) is CompleteStepViewModel completeStep)
        {
            completeStep.TakeFirstScreenshotCallback = async () =>
            {
                // Trigger region capture
                await Task.CompletedTask;
            };

            completeStep.OpenSettingsCallback = () =>
            {
                // Open settings dialog
                // This would be wired up to the main window's settings navigation
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
                tcs.TrySetResult(task.Result);
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
