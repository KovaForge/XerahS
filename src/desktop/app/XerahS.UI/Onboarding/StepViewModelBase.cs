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
using XerahS.UI.ViewModels;

namespace XerahS.UI.Onboarding;

/// <summary>
/// Abstract base class for all onboarding step ViewModels.
/// </summary>
public abstract partial class StepViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private OnboardingState _state = new();

    [ObservableProperty]
    private int _stepIndex;

    [ObservableProperty]
    private string _stepTitle = "";

    [ObservableProperty]
    private string _stepSubtitle = "";

    [ObservableProperty]
    private string _stepDescription = "";

    [ObservableProperty]
    private bool _isValid = true;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool _canSkip = true;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    /// <summary>
    /// Loads state into this step's ViewModel.
    /// </summary>
    public virtual void LoadFromState(OnboardingState state) { }

    /// <summary>
    /// Saves this step's data back to the state object.
    /// </summary>
    public virtual void SaveToState(OnboardingState state) { }

    /// <summary>
    /// Validates the current step's data.
    /// </summary>
    public virtual bool Validate() => true;

    /// <summary>
    /// Called when the user skips this step.
    /// </summary>
    public virtual void MarkSkipped() { }

    /// <summary>
    /// Performs any async test/validation for this step.
    /// </summary>
    public virtual Task<bool> TestAsync() => Task.FromResult(true);

    protected void SetValidationState(bool isValid, string? validationError = null)
    {
        IsValid = isValid;
        ValidationError = isValid ? null : validationError;
    }

    partial void OnValidationErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidationError));
    }
}
