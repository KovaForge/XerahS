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

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Partial class additions for WelcomeStepViewModel to support UI bindings.
/// </summary>
public partial class WelcomeStepViewModel
{
    private bool _isSyncing;

    /// <summary>
    /// The currently selected language as a LanguageOption object.
    /// Synced with the string-based SelectedLanguage property.
    /// </summary>
    [ObservableProperty]
    private LanguageOption? _selectedLanguageItem;

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        try
        {
            // Sync SelectedLanguageItem when code changes
            if (string.IsNullOrEmpty(value))
            {
                _selectedLanguageItem = null;
                OnPropertyChanged(nameof(SelectedLanguageItem));
            }
            else
            {
                var option = AvailableLanguages?.FirstOrDefault(l => l.Code == value);
                if (option != null)
                {
                    _selectedLanguageItem = option;
                    OnPropertyChanged(nameof(SelectedLanguageItem));
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    partial void OnSelectedLanguageItemChanged(LanguageOption? value)
    {
        if (_isSyncing) return;
        if (value == null) return;

        _isSyncing = true;
        try
        {
            // Sync SelectedLanguage (code) when item changes
            if (value.Code != _selectedLanguage)
            {
                SetProperty(ref _selectedLanguage, value.Code, nameof(SelectedLanguage));
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
