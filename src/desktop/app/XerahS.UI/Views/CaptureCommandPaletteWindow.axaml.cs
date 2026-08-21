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
using Avalonia.Input;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class CaptureCommandPaletteWindow : OverlayWindow
{
    public CaptureCommandPaletteWindow()
    {
        InitializeComponent();
        Opened += (_, _) => SearchTextBox.Focus();
        KeyDown += OnKeyDown;
    }

    public void Initialize(CaptureCommandPaletteViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        viewModel.RequestFocusSearch += () => SearchTextBox.Focus();
        Closed += (_, _) =>
        {
            viewModel.RequestClose -= Close;
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CaptureCommandPaletteViewModel viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                viewModel.HandleEscape();
                e.Handled = true;
                break;
            case Key.Down:
                viewModel.MoveSelection(1);
                if (viewModel.SelectedItem != null)
                {
                    ResultsListBox.ScrollIntoView(viewModel.SelectedItem);
                }
                e.Handled = true;
                break;
            case Key.Up:
                viewModel.MoveSelection(-1);
                if (viewModel.SelectedItem != null)
                {
                    ResultsListBox.ScrollIntoView(viewModel.SelectedItem);
                }
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                if (viewModel.ExecuteSelectedCommand.CanExecute(null))
                {
                    viewModel.ExecuteSelectedCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }
}
