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
public record OcrLanguageOption(string LanguageTag, string DisplayName, string NativeName, long DownloadSizeMb);

/// <summary>
/// Step 5: OCR Configuration
/// </summary>
public partial class OcrStepViewModel : StepViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> _selectedLanguages = new();

    [ObservableProperty]
    private bool _downloadInBackground = true;

    public ObservableCollection<OcrLanguageOption> AvailableLanguages { get; } = new();

    public long TotalDownloadSizeMb => SelectedLanguages
        .Select(lang => AvailableLanguages.FirstOrDefault(l => l.LanguageTag == lang))
        .Where(l => l != null)
        .Sum(l => l!.DownloadSizeMb);

    public bool ExceedsRecommendedSize => TotalDownloadSizeMb > 200;

    private string _defaultLanguage = "en";

    public OcrStepViewModel()
    {
        StepTitle = "Text Recognition (OCR)";
        StepSubtitle = "Extract text from images";
        StepDescription = "Select languages for OCR. You can always add more later in Settings.";
        CanSkip = true;

        InitializeLanguages();
    }

    private void InitializeLanguages()
    {
        // Common OCR languages with estimated download sizes
        AvailableLanguages.Add(new OcrLanguageOption("en", "English", "English", 25));
        AvailableLanguages.Add(new OcrLanguageOption("es", "Spanish", "Español", 20));
        AvailableLanguages.Add(new OcrLanguageOption("fr", "French", "Français", 20));
        AvailableLanguages.Add(new OcrLanguageOption("de", "German", "Deutsch", 20));
        AvailableLanguages.Add(new OcrLanguageOption("it", "Italian", "Italiano", 18));
        AvailableLanguages.Add(new OcrLanguageOption("pt", "Portuguese", "Português", 18));
        AvailableLanguages.Add(new OcrLanguageOption("ru", "Russian", "Русский", 22));
        AvailableLanguages.Add(new OcrLanguageOption("ja", "Japanese", "日本語", 35));
        AvailableLanguages.Add(new OcrLanguageOption("ko", "Korean", "한국어", 30));
        AvailableLanguages.Add(new OcrLanguageOption("zh-Hans", "Chinese (Simplified)", "简体中文", 40));
        AvailableLanguages.Add(new OcrLanguageOption("zh-Hant", "Chinese (Traditional)", "繁體中文", 40));
        AvailableLanguages.Add(new OcrLanguageOption("ar", "Arabic", "العربية", 25));
        AvailableLanguages.Add(new OcrLanguageOption("hi", "Hindi", "हिन्दी", 28));
        AvailableLanguages.Add(new OcrLanguageOption("pl", "Polish", "Polski", 18));
        AvailableLanguages.Add(new OcrLanguageOption("nl", "Dutch", "Nederlands", 18));
        AvailableLanguages.Add(new OcrLanguageOption("tr", "Turkish", "Türkçe", 18));

        // Default to English
        SelectedLanguages.Add("en");
    }

    public void SetDefaultLanguage(string languageCode)
    {
        _defaultLanguage = languageCode;

        // Add the default language if available
        var match = AvailableLanguages.FirstOrDefault(l =>
            l.LanguageTag.Equals(languageCode, StringComparison.OrdinalIgnoreCase) ||
            l.LanguageTag.StartsWith(languageCode + "-", StringComparison.OrdinalIgnoreCase));

        if (match != null && !SelectedLanguages.Contains(match.LanguageTag))
        {
            SelectedLanguages.Add(match.LanguageTag);
        }
    }

    [RelayCommand]
    private void ToggleLanguage(string languageTag)
    {
        if (SelectedLanguages.Contains(languageTag))
        {
            // Don't remove the last language
            if (SelectedLanguages.Count > 1)
            {
                SelectedLanguages.Remove(languageTag);
            }
        }
        else
        {
            SelectedLanguages.Add(languageTag);
        }

        OnPropertyChanged(nameof(TotalDownloadSizeMb));
        OnPropertyChanged(nameof(ExceedsRecommendedSize));
    }

    [RelayCommand]
    private async Task RefreshAvailableLanguagesAsync()
    {
        // Query the platform OCR service for available languages
        var ocrService = PlatformServices.Ocr;
        if (ocrService != null && ocrService.IsSupported)
        {
            var platformLanguages = ocrService.GetAvailableLanguages();

            // Update available languages based on platform support
            AvailableLanguages.Clear();
            foreach (var lang in platformLanguages)
            {
                // Estimate size (platform languages typically don't require download)
                AvailableLanguages.Add(new OcrLanguageOption(
                    lang.LanguageTag,
                    lang.DisplayName,
                    lang.DisplayName, // Native name not provided by platform
                    0));
            }
        }
        else
        {
            // OCR not supported on this platform
            AvailableLanguages.Clear();
        }

        await Task.CompletedTask;
    }

    public override void LoadFromState(OnboardingState state)
    {
        SelectedLanguages.Clear();
        foreach (var lang in state.SelectedOcrLanguages)
        {
            SelectedLanguages.Add(lang);
        }

        if (SelectedLanguages.Count == 0)
        {
            SelectedLanguages.Add("en");
        }

        DownloadInBackground = state.DownloadOcrInBackground;
    }

    public override void SaveToState(OnboardingState state)
    {
        state.SelectedOcrLanguages = SelectedLanguages.ToList();
        state.DownloadOcrInBackground = DownloadInBackground;
    }

    public override bool Validate()
    {
        return SelectedLanguages.Count > 0;
    }

    partial void OnSelectedLanguagesChanged(ObservableCollection<string> value)
    {
        OnPropertyChanged(nameof(TotalDownloadSizeMb));
        OnPropertyChanged(nameof(ExceedsRecommendedSize));
    }
}
