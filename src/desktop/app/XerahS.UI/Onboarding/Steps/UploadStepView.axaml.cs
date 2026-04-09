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
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.UI.Onboarding.Steps;

/// <summary>
/// Step 4: Upload Destination Configuration view.
/// </summary>
public partial class UploadStepView : UserControl
{
    public UploadStepView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is UploadStepViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(UploadStepViewModel.SelectedUploaderId))
                {
                    UpdateCardSelection();
                }
                else if (args.PropertyName == nameof(UploadStepViewModel.IsTestSuccessful) ||
                         args.PropertyName == nameof(UploadStepViewModel.TestResult))
                {
                    UpdateTestResultDisplay(vm);
                }
            };
        }
    }

    private void UpdateCardSelection()
    {
        if (DataContext is not UploadStepViewModel vm) return;

        if (UploadersList?.ItemsPanelRoot is not null)
        {
            var items = UploadersList.ItemCount;
            for (int i = 0; i < items; i++)
            {
                var container = UploadersList.ContainerFromIndex(i) as ContentPresenter;
                if (container == null) continue;

                var border = container.FindDescendantOfType<Border>();
                if (border == null) continue;

                var option = vm.AvailableUploaders[i];
                var isSelected = option.Id == vm.SelectedUploaderId;

                // Update border classes
                border.Classes.Set("WizardRadioCardSelected", isSelected);
                border.Classes.Set("WizardRadioCard", !isSelected);

                // Update radio circle
                var ellipse = border.FindDescendantOfType<Avalonia.Controls.Shapes.Ellipse>();
                if (ellipse != null)
                {
                    var selectedAccent = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#00B4A6"));
                    var unselectedBorder = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E2E5EA"));
                    ellipse.Stroke = isSelected ? selectedAccent : unselectedBorder;
                    ellipse.Fill = isSelected ? selectedAccent : Avalonia.Media.Brushes.Transparent;
                }
            }
        }
    }

    private void UpdateTestResultDisplay(UploadStepViewModel vm)
    {
        if (TestResultBorder == null) return;

        TestResultBorder.Classes.Clear();
        TestResultBorder.Classes.Add(vm.IsTestSuccessful ? "WizardSuccessCard" : "WizardConflictCard");
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.Tag is not UploaderOption option) return;
        if (DataContext is not UploadStepViewModel vm) return;

        vm.SelectedUploaderId = option.Id;
        UpdateCardSelection();
    }

    private void OnRadioClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        if (rb.Tag is not string uploaderId) return;
        if (DataContext is not UploadStepViewModel vm) return;

        vm.SelectedUploaderId = uploaderId;
        UpdateCardSelection();
    }
}
