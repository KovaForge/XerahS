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
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Represents an OCR language option.
/// </summary>
public partial class OcrLanguageOption : ObservableObject
{
    public string LanguageTag { get; }
    public string DisplayName { get; }
    public string NativeName { get; }
    public long DownloadSizeMb { get; }

    [ObservableProperty]
    private bool _isSelected;

    public OcrLanguageOption(string languageTag, string displayName, string nativeName, long downloadSizeMb)
    {
        LanguageTag = languageTag;
        DisplayName = displayName;
        NativeName = nativeName;
        DownloadSizeMb = downloadSizeMb;
    }
}

/// <summary>
/// Step 5: OCR Configuration
/// </summary>
public partial class OcrStepViewModel : StepViewModelBase
{
    private bool _syncingSelections;

    [ObservableProperty]
    private ObservableCollection<string> _selectedLanguages = new();

    [ObservableProperty]
    private bool _downloadInBackground = true;

    public ObservableCollection<OcrLanguageOption> AvailableLanguages { get; } = new();

    public long TotalDownloadSizeMb => AvailableLanguages
        .Where(language => language.IsSelected)
        .Sum(language => language.DownloadSizeMb);

    public bool ExceedsRecommendedSize => TotalDownloadSizeMb > 200;

    public OcrStepViewModel()
    {
        StepTitle = "Text Recognition (OCR)";
        StepSubtitle = "Extract text from images";
        StepDescription = "Select languages for OCR. You can always add more later in Settings.";
        CanSkip = true;

        SelectedLanguages.CollectionChanged += SelectedLanguages_CollectionChanged;
        InitializeLanguages();
        UpdateValidationState();
    }

    private void InitializeLanguages()
    {
        RegisterLanguage(new OcrLanguageOption("en", "English", "English", 25) { IsSelected = true });
        RegisterLanguage(new OcrLanguageOption("es", "Spanish", "Spanish", 20));
        RegisterLanguage(new OcrLanguageOption("fr", "French", "French", 20));
        RegisterLanguage(new OcrLanguageOption("de", "German", "German", 20));
        RegisterLanguage(new OcrLanguageOption("it", "Italian", "Italian", 18));
        RegisterLanguage(new OcrLanguageOption("pt", "Portuguese", "Portuguese", 18));
        RegisterLanguage(new OcrLanguageOption("ru", "Russian", "Russian", 22));
        RegisterLanguage(new OcrLanguageOption("ja", "Japanese", "Japanese", 35));
        RegisterLanguage(new OcrLanguageOption("ko", "Korean", "Korean", 30));
        RegisterLanguage(new OcrLanguageOption("zh-Hans", "Chinese (Simplified)", "Chinese (Simplified)", 40));
        RegisterLanguage(new OcrLanguageOption("zh-Hant", "Chinese (Traditional)", "Chinese (Traditional)", 40));
        RegisterLanguage(new OcrLanguageOption("ar", "Arabic", "Arabic", 25));
        RegisterLanguage(new OcrLanguageOption("hi", "Hindi", "Hindi", 28));
        RegisterLanguage(new OcrLanguageOption("pl", "Polish", "Polish", 18));
        RegisterLanguage(new OcrLanguageOption("nl", "Dutch", "Dutch", 18));
        RegisterLanguage(new OcrLanguageOption("tr", "Turkish", "Turkish", 18));

        SyncSelectedLanguagesFromOptions();
    }

    private void RegisterLanguage(OcrLanguageOption option)
    {
        option.PropertyChanged += OnLanguageOptionPropertyChanged;
        AvailableLanguages.Add(option);
    }

    public void SetDefaultLanguage(string languageCode)
    {
        OcrLanguageOption? match = AvailableLanguages.FirstOrDefault(language =>
            language.LanguageTag.Equals(languageCode, StringComparison.OrdinalIgnoreCase) ||
            language.LanguageTag.StartsWith(languageCode + "-", StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            match.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ToggleLanguage(string languageTag)
    {
        OcrLanguageOption? option = AvailableLanguages.FirstOrDefault(language => language.LanguageTag == languageTag);
        if (option == null)
        {
            return;
        }

        option.IsSelected = !option.IsSelected;
    }

    [RelayCommand]
    private async Task RefreshAvailableLanguagesAsync()
    {
        var ocrService = PlatformServices.Ocr;
        if (ocrService != null && ocrService.IsSupported)
        {
            IEnumerable<OcrLanguage> platformLanguages = ocrService.GetAvailableLanguages();

            AvailableLanguages.Clear();
            foreach (OcrLanguage language in platformLanguages)
            {
                RegisterLanguage(new OcrLanguageOption(
                    language.LanguageTag,
                    language.DisplayName,
                    language.DisplayName,
                    0));
            }

            SyncOptionsFromSelectedLanguages();
            UpdateValidationState();
        }

        await Task.CompletedTask;
    }

    public override void LoadFromState(OnboardingState state)
    {
        _syncingSelections = true;
        SelectedLanguages.Clear();

        List<string> languagesToSelect = state.SelectedOcrLanguages.Count > 0
            ? state.SelectedOcrLanguages
            : ["en"];

        foreach (string language in languagesToSelect)
        {
            SelectedLanguages.Add(language);
        }

        _syncingSelections = false;
        SyncOptionsFromSelectedLanguages();
        DownloadInBackground = state.DownloadOcrInBackground;
        UpdateValidationState();
    }

    public override void SaveToState(OnboardingState state)
    {
        state.SelectedOcrLanguages = SelectedLanguages.ToList();
        state.DownloadOcrInBackground = DownloadInBackground;
    }

    public override bool Validate()
    {
        UpdateValidationState();
        return SelectedLanguages.Count > 0;
    }

    partial void OnSelectedLanguagesChanged(ObservableCollection<string> value)
    {
        value.CollectionChanged += SelectedLanguages_CollectionChanged;
        UpdateValidationState();
    }

    private void SelectedLanguages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateValidationState();
    }

    private void OnLanguageOptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_syncingSelections || e.PropertyName != nameof(OcrLanguageOption.IsSelected) || sender is not OcrLanguageOption option)
        {
            return;
        }

        if (option.IsSelected)
        {
            if (!SelectedLanguages.Contains(option.LanguageTag))
            {
                SelectedLanguages.Add(option.LanguageTag);
            }
        }
        else if (SelectedLanguages.Contains(option.LanguageTag))
        {
            if (SelectedLanguages.Count == 1)
            {
                _syncingSelections = true;
                option.IsSelected = true;
                _syncingSelections = false;
                return;
            }

            SelectedLanguages.Remove(option.LanguageTag);
        }

        UpdateValidationState();
    }

    private void SyncSelectedLanguagesFromOptions()
    {
        _syncingSelections = true;
        SelectedLanguages.Clear();

        foreach (OcrLanguageOption option in AvailableLanguages.Where(language => language.IsSelected))
        {
            SelectedLanguages.Add(option.LanguageTag);
        }

        _syncingSelections = false;
        UpdateValidationState();
    }

    private void SyncOptionsFromSelectedLanguages()
    {
        _syncingSelections = true;

        foreach (OcrLanguageOption option in AvailableLanguages)
        {
            option.IsSelected = SelectedLanguages.Contains(option.LanguageTag);
        }

        if (SelectedLanguages.Count == 0)
        {
            OcrLanguageOption? english = AvailableLanguages.FirstOrDefault(language => language.LanguageTag == "en");
            if (english != null)
            {
                english.IsSelected = true;
                SelectedLanguages.Add(english.LanguageTag);
            }
        }

        _syncingSelections = false;
        UpdateValidationState();
    }

    private void UpdateValidationState()
    {
        OnPropertyChanged(nameof(TotalDownloadSizeMb));
        OnPropertyChanged(nameof(ExceedsRecommendedSize));
        SetValidationState(SelectedLanguages.Count > 0, SelectedLanguages.Count > 0 ? null : "Select at least one OCR language.");
    }
}
