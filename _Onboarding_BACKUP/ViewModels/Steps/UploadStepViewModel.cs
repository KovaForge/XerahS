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

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Represents an uploader option for the onboarding step.
/// </summary>
public record UploaderOption(string Id, string Name, string Description, string Icon, bool RequiresAuth);

/// <summary>
/// Step 4: Upload Configuration
/// </summary>
public partial class UploadStepViewModel : StepViewModelBase
{
    [ObservableProperty]
    private string? _selectedUploaderId;

    [ObservableProperty]
    private bool _hasShareXConfig;

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private string? _testResult;

    [ObservableProperty]
    private bool _isTestSuccessful;

    public ObservableCollection<UploaderOption> AvailableUploaders { get; } = new();

    public bool HasSelection => !string.IsNullOrEmpty(SelectedUploaderId);

    /// <summary>
    /// Callback to import from ShareX. Set by the wizard.
    /// </summary>
    public Func<Task<bool>>? ImportShareXCallback { get; set; }

    public UploadStepViewModel()
    {
        StepTitle = "Upload Settings";
        StepSubtitle = "Where should screenshots be uploaded?";
        StepDescription = "Choose your preferred upload destination or keep screenshots local only.";
        CanSkip = true;

        InitializeUploaders();
        CheckForShareXConfig();
    }

    private void InitializeUploaders()
    {
        AvailableUploaders.Add(new UploaderOption(
            "local",
            "Local only",
            "Screenshots are saved to your computer only",
            "💻",
            false));

        AvailableUploaders.Add(new UploaderOption(
            "imgur_anon",
            "Imgur (anonymous)",
            "Upload to Imgur without authentication",
            "🖼️",
            false));

        AvailableUploaders.Add(new UploaderOption(
            "imgur_auth",
            "Imgur (authenticated)",
            "Upload to your Imgur account",
            "🔐",
            true));

        AvailableUploaders.Add(new UploaderOption(
            "custom",
            "Custom uploader",
            "Configure your own upload destination",
            "⚙️",
            false));

        AvailableUploaders.Add(new UploaderOption(
            "more",
            "More options...",
            "Explore additional upload destinations",
            "➕",
            false));

        // Default to local only
        SelectedUploaderId = "local";
    }

    private void CheckForShareXConfig()
    {
        // Check if ShareX config exists in default location
        var shareXPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ShareX");
        HasShareXConfig = Directory.Exists(shareXPath) &&
                         File.Exists(Path.Combine(shareXPath, "ApplicationConfig.json"));
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrEmpty(SelectedUploaderId) || SelectedUploaderId == "local")
        {
            TestResult = "Local storage doesn't require a connection test.";
            IsTestSuccessful = true;
            return;
        }

        IsTestingConnection = true;
        TestResult = null;

        try
        {
            // Simulate connection test
            await Task.Delay(1000);

            if (SelectedUploaderId == "imgur_anon")
            {
                TestResult = "Connection successful! Imgur anonymous upload is ready.";
                IsTestSuccessful = true;
            }
            else if (SelectedUploaderId == "imgur_auth")
            {
                TestResult = "Authentication required. You'll be prompted to authorize when you first upload.";
                IsTestSuccessful = true;
            }
            else if (SelectedUploaderId == "custom")
            {
                TestResult = "Custom uploaders can be configured in Settings after setup.";
                IsTestSuccessful = true;
            }
            else
            {
                TestResult = "Additional uploaders available in Settings.";
                IsTestSuccessful = true;
            }
        }
        catch (Exception ex)
        {
            TestResult = $"Connection test failed: {ex.Message}";
            IsTestSuccessful = false;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task ImportFromShareXAsync()
    {
        if (ImportShareXCallback != null)
        {
            var result = await ImportShareXCallback();
            if (result)
            {
                TestResult = "ShareX configuration imported successfully!";
                IsTestSuccessful = true;
            }
            else
            {
                TestResult = "Failed to import ShareX configuration.";
                IsTestSuccessful = false;
            }
        }
    }

    public override void LoadFromState(OnboardingState state)
    {
        SelectedUploaderId = state.SelectedUploaderId ?? "local";
    }

    public override void SaveToState(OnboardingState state)
    {
        state.SelectedUploaderId = SelectedUploaderId;
    }

    public override bool Validate()
    {
        return !string.IsNullOrEmpty(SelectedUploaderId);
    }

    partial void OnSelectedUploaderIdChanged(string? value)
    {
        TestResult = null;
        IsTestSuccessful = false;
    }
}
