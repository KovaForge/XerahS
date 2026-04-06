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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Step 6: Completion and Summary
/// </summary>
public partial class CompleteStepViewModel : StepViewModelBase
{
    [ObservableProperty]
    private int _configuredStepCount;

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private bool _showTipsOnStartup = true;

    /// <summary>
    /// Callback to trigger first screenshot. Set by the wizard.
    /// </summary>
    public Func<Task>? TakeFirstScreenshotCallback { get; set; }

    /// <summary>
    /// Callback to open full settings. Set by the wizard.
    /// </summary>
    public Action? OpenSettingsCallback { get; set; }

    public CompleteStepViewModel()
    {
        StepTitle = "All Set!";
        StepSubtitle = "You're ready to capture";
        StepDescription = "Here's a summary of your configuration.";
        CanSkip = false;
    }

    public void GenerateSummary(OnboardingState state)
    {
        var parts = new List<string>();
        int configuredCount = 0;

        // Language
        if (!state.SkippedSteps.Contains(0))
        {
            parts.Add($"• Language: {state.SelectedLanguage}");
            configuredCount++;
        }

        // Save location
        if (!state.SkippedSteps.Contains(1))
        {
            parts.Add($"• Screenshots saved to: {state.ScreenshotsFolder}");
            if (state.CreateDateSubfolders)
            {
                parts.Add("  (with date subfolders)");
            }
            configuredCount++;
        }

        // Hotkeys
        if (!state.SkippedSteps.Contains(2) && state.PrimaryCaptureHotkey != null)
        {
            parts.Add($"• Primary hotkey: {state.PrimaryCaptureHotkey}");
            if (state.AdditionalHotkeys.Count > 0)
            {
                parts.Add($"  ({state.AdditionalHotkeys.Count} additional shortcuts)");
            }
            configuredCount++;
        }

        // Upload
        if (!state.SkippedSteps.Contains(3))
        {
            var uploadText = state.SelectedUploaderId switch
            {
                "local" => "Local storage only",
                "imgur_anon" => "Imgur (anonymous)",
                "imgur_auth" => "Imgur (authenticated)",
                "custom" => "Custom uploader",
                _ => "Not configured"
            };
            parts.Add($"• Upload destination: {uploadText}");
            configuredCount++;
        }

        // OCR
        if (!state.SkippedSteps.Contains(4) && state.SelectedOcrLanguages.Count > 0)
        {
            var langList = string.Join(", ", state.SelectedOcrLanguages.Take(3));
            if (state.SelectedOcrLanguages.Count > 3)
            {
                langList += $" (+{state.SelectedOcrLanguages.Count - 3} more)";
            }
            parts.Add($"• OCR languages: {langList}");
            configuredCount++;
        }

        ConfiguredStepCount = configuredCount;
        SummaryText = string.Join(Environment.NewLine, parts);
    }

    [RelayCommand]
    private async Task TakeFirstScreenshotAsync()
    {
        if (TakeFirstScreenshotCallback != null)
        {
            await TakeFirstScreenshotCallback();
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        OpenSettingsCallback?.Invoke();
    }

    public override void LoadFromState(OnboardingState state)
    {
        GenerateSummary(state);
    }

    public override void SaveToState(OnboardingState state)
    {
        // Nothing to save from this step
    }

    public override bool Validate() => true;
}
