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
using CommunityToolkit.Mvvm.Input;

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Language option for the welcome step.
/// </summary>
public record LanguageOption(string Code, string DisplayName, string NativeName);

/// <summary>
/// Step 1: Welcome and Language Selection
/// </summary>
public partial class WelcomeStepViewModel : StepViewModelBase
{
    [ObservableProperty]
    private string _selectedLanguage = "en";

    [ObservableProperty]
    private string _fallbackHint = "";

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

    public WelcomeStepViewModel()
    {
        StepTitle = "Welcome to XerahS";
        StepSubtitle = "Let's get you set up";
        StepDescription = "Choose your preferred language to continue.";
        CanSkip = false;

        LoadAvailableLanguages();
    }

    private void LoadAvailableLanguages()
    {
        // Get cultures but filter to avoid overwhelming list
        // Prioritize common languages and the current UI culture
        var currentCulture = CultureInfo.CurrentUICulture;
        var allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(c => !c.IsNeutralCulture && !string.IsNullOrEmpty(c.Name))
            .GroupBy(c => c.TwoLetterISOLanguageName)
            .Select(g => g.First())
            .OrderBy(c => c.DisplayName)
            .ToList();

        // Common languages to show at the top
        var priorityCodes = new[] { "en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh" };
        var priorityLanguages = allCultures
            .Where(c => priorityCodes.Contains(c.TwoLetterISOLanguageName))
            .OrderBy(c => Array.IndexOf(priorityCodes, c.TwoLetterISOLanguageName))
            .ToList();

        // Add priority languages first
        foreach (var culture in priorityLanguages)
        {
            AvailableLanguages.Add(new LanguageOption(
                culture.TwoLetterISOLanguageName,
                culture.DisplayName,
                culture.NativeName));
        }

        // Add separator indicator (represented by empty code)
        AvailableLanguages.Add(new LanguageOption("", "──────────", ""));

        // Add remaining languages
        foreach (var culture in allCultures.Where(c => !priorityCodes.Contains(c.TwoLetterISOLanguageName)))
        {
            AvailableLanguages.Add(new LanguageOption(
                culture.TwoLetterISOLanguageName,
                culture.DisplayName,
                culture.NativeName));
        }

        // Pre-select current UI culture or English as fallback
        var currentCode = currentCulture.TwoLetterISOLanguageName;
        var match = AvailableLanguages.FirstOrDefault(l => l.Code == currentCode);
        if (match != null)
        {
            SelectedLanguage = match.Code;
        }
        else
        {
            SelectedLanguage = "en";
            FallbackHint = $"Your system language ({currentCulture.DisplayName}) is not available. English has been selected as the default.";
        }
    }

    public override void LoadFromState(OnboardingState state)
    {
        SelectedLanguage = state.SelectedLanguage;
    }

    public override void SaveToState(OnboardingState state)
    {
        state.SelectedLanguage = SelectedLanguage;
    }

    public override bool Validate() => true; // Language selection is always valid
}
