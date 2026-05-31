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
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Language option for the welcome step.
/// </summary>
public record LanguageOption(string Code, string DisplayName, string NativeName)
{
    private static LanguageSelectionConverter? _isSelectedConverter;

    public static Avalonia.Data.Converters.IValueConverter IsSelectedConverter => _isSelectedConverter ??= new LanguageSelectionConverter();
}

/// <summary>
/// Compares a language code against the selected language to drive RadioButton.IsChecked.
/// </summary>
public sealed class LanguageSelectionConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string selectedCode && parameter is string itemCode)
        {
            return string.Equals(selectedCode, itemCode, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new System.NotSupportedException();
    }
}

/// <summary>
/// Step 1: Welcome and Language Selection
/// </summary>
public partial class WelcomeStepViewModel : StepViewModelBase
{
    private static readonly (string Code, string CultureName)[] SupportedLanguageDefinitions =
    [
        ("en", "en"),
        ("es", "es"),
        ("es-mx", "es-MX"),
        ("fr", "fr"),
        ("de", "de"),
        ("it", "it"),
        ("pt", "pt-PT"),
        ("pt-br", "pt-BR"),
        ("ru", "ru"),
        ("ja", "ja"),
        ("ko", "ko"),
        ("zh-hans", "zh-CN"),
        ("zh-hant", "zh-TW"),
        ("ar", "ar"),
        ("nl", "nl"),
        ("pl", "pl"),
        ("tr", "tr"),
        ("he", "he"),
        ("hu", "hu"),
        ("id", "id"),
        ("fa", "fa"),
        ("ro", "ro"),
        ("uk", "uk"),
        ("vi", "vi")
    ];

    private bool _syncingSelection;

    [ObservableProperty]
    private string _selectedLanguage = "en";

    [ObservableProperty]
    private string _fallbackHint = "";

    [ObservableProperty]
    private LanguageOption? _selectedLanguageOption;

    public bool HasFallbackHint => !string.IsNullOrWhiteSpace(FallbackHint);

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

    public CommunityToolkit.Mvvm.Input.IRelayCommand<LanguageOption> SelectLanguageCommand { get; }

    public WelcomeStepViewModel()
    {
        StepTitle = "Welcome to XerahS";
        StepSubtitle = "Let's get you set up";
        StepDescription = "Choose your preferred language to continue.";
        CanSkip = false;

        SelectLanguageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<LanguageOption>(SelectLanguage);

        LoadAvailableLanguages();
        SetValidationState(true);
    }

    private void SelectLanguage(LanguageOption? option)
    {
        if (option != null)
        {
            SelectedLanguageOption = option;
        }
    }

    private void LoadAvailableLanguages()
    {
        CultureInfo currentCulture = CultureInfo.CurrentUICulture;
        AvailableLanguages.Clear();
        FallbackHint = string.Empty;

        foreach ((string code, string cultureName) in SupportedLanguageDefinitions)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            AvailableLanguages.Add(new LanguageOption(
                code,
                culture.EnglishName,
                culture.NativeName));
        }

        string? currentCode = NormalizeSupportedLanguageCode(currentCulture.Name)
            ?? NormalizeSupportedLanguageCode(currentCulture.TwoLetterISOLanguageName);

        if (!string.IsNullOrWhiteSpace(currentCode))
        {
            SelectedLanguage = currentCode;
        }
        else
        {
            SelectedLanguage = "en";
            FallbackHint = $"Your system language ({currentCulture.DisplayName}) is not available. English has been selected as the default.";
        }

        SyncSelectedLanguageOption();
    }

    public override void LoadFromState(OnboardingState state)
    {
        string? normalizedLanguageCode = NormalizeSupportedLanguageCode(state.SelectedLanguage);
        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            SelectedLanguage = normalizedLanguageCode;
        }

        SyncSelectedLanguageOption();
    }

    public override void SaveToState(OnboardingState state)
    {
        state.SelectedLanguage = SelectedLanguage;
    }

    public override bool Validate()
    {
        SetValidationState(true);
        return true;
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_syncingSelection)
        {
            return;
        }

        SyncSelectedLanguageOption();
        SetValidationState(true);
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (value == null || string.Equals(SelectedLanguage, value.Code, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _syncingSelection = true;
        SelectedLanguage = value.Code;
        _syncingSelection = false;
        SetValidationState(true);
    }

    partial void OnFallbackHintChanged(string value)
    {
        OnPropertyChanged(nameof(HasFallbackHint));
    }

    private void SyncSelectedLanguageOption()
    {
        _syncingSelection = true;
        SelectedLanguageOption = AvailableLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, SelectedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, "en", StringComparison.OrdinalIgnoreCase));
        _syncingSelection = false;
    }

    private static string? NormalizeSupportedLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.ToLowerInvariant() switch
        {
            "ar" => "ar",
            "nl" => "nl",
            "en" => "en",
            "fr" => "fr",
            "de" => "de",
            "he" => "he",
            "hu" => "hu",
            "id" => "id",
            "it" => "it",
            "ja" => "ja",
            "ko" => "ko",
            "es-mx" => "es-mx",
            "fa" => "fa",
            "pl" => "pl",
            "pt" => "pt",
            "pt-br" => "pt-br",
            "ro" => "ro",
            "ru" => "ru",
            "zh" => "zh-hans",
            "zh-cn" => "zh-hans",
            "zh-sg" => "zh-hans",
            "zh-hans" => "zh-hans",
            "es" => "es",
            "zh-tw" => "zh-hant",
            "zh-hk" => "zh-hant",
            "zh-mo" => "zh-hant",
            "zh-hant" => "zh-hant",
            "tr" => "tr",
            "uk" => "uk",
            "vi" => "vi",
            _ => null
        };
    }
}
