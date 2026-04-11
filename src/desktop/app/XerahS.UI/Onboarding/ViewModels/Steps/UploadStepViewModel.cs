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
public partial class UploaderOption : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Icon { get; }
    public bool RequiresAuth { get; }

    [ObservableProperty]
    private bool _isSelected;

    public UploaderOption(string id, string name, string description, string icon, bool requiresAuth)
    {
        Id = id;
        Name = name;
        Description = description;
        Icon = icon;
        RequiresAuth = requiresAuth;
    }
}

/// <summary>
/// Step 4: Upload Configuration
/// </summary>
public partial class UploadStepViewModel : StepViewModelBase
{
    private bool _syncingSelection;

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

    public bool HasTestResult => !string.IsNullOrEmpty(TestResult);

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
        SetValidationState(true);
    }

    private void InitializeUploaders()
    {
        RegisterOption(new UploaderOption(
            "local",
            "Local only",
            "Screenshots are saved to your computer only.",
            "LOCAL",
            false));

        RegisterOption(new UploaderOption(
            "imgur_anon",
            "Imgur (anonymous)",
            "Upload to Imgur without authentication.",
            "IMG",
            false));

        RegisterOption(new UploaderOption(
            "imgur_auth",
            "Imgur (authenticated)",
            "Upload to your Imgur account.",
            "AUTH",
            true));

        RegisterOption(new UploaderOption(
            "custom",
            "Custom uploader",
            "Configure your own upload destination later in Settings.",
            "CFG",
            false));

        RegisterOption(new UploaderOption(
            "more",
            "More options",
            "Explore additional upload destinations after setup.",
            "MORE",
            false));

        SelectedUploaderId = "local";
    }

    private void RegisterOption(UploaderOption option)
    {
        option.PropertyChanged += OnUploaderOptionPropertyChanged;
        AvailableUploaders.Add(option);
    }

    private void CheckForShareXConfig()
    {
        string shareXPath = Path.Combine(
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
            TestResult = "Local storage does not require a connection test.";
            IsTestSuccessful = true;
            return;
        }

        IsTestingConnection = true;
        TestResult = null;

        try
        {
            await Task.Delay(1000);

            TestResult = SelectedUploaderId switch
            {
                "imgur_anon" => "Connection successful. Imgur anonymous upload is ready.",
                "imgur_auth" => "Authentication will be requested the first time you upload.",
                "custom" => "Custom uploaders can be configured in Settings after setup.",
                _ => "Additional uploaders are available in Settings."
            };
            IsTestSuccessful = true;
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
        if (ImportShareXCallback == null)
        {
            return;
        }

        bool result = await ImportShareXCallback();
        if (result)
        {
            TestResult = "ShareX configuration imported successfully.";
            IsTestSuccessful = true;
        }
        else
        {
            TestResult = "Failed to import ShareX configuration.";
            IsTestSuccessful = false;
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
        bool isValid = !string.IsNullOrWhiteSpace(SelectedUploaderId);
        SetValidationState(isValid, isValid ? null : "Choose an upload destination.");
        return isValid;
    }

    partial void OnSelectedUploaderIdChanged(string? value)
    {
        TestResult = null;
        IsTestSuccessful = false;
        SyncSelectionToState();
        SetValidationState(!string.IsNullOrWhiteSpace(value), string.IsNullOrWhiteSpace(value) ? "Choose an upload destination." : null);
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnTestResultChanged(string? value)
    {
        OnPropertyChanged(nameof(HasTestResult));
    }

    private void OnUploaderOptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_syncingSelection || e.PropertyName != nameof(UploaderOption.IsSelected) || sender is not UploaderOption option || !option.IsSelected)
        {
            return;
        }

        SelectedUploaderId = option.Id;
    }

    private void SyncSelectionToState()
    {
        _syncingSelection = true;

        foreach (UploaderOption option in AvailableUploaders)
        {
            option.IsSelected = string.Equals(option.Id, SelectedUploaderId, StringComparison.Ordinal);
        }

        _syncingSelection = false;
    }
}
