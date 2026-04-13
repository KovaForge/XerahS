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

public partial class AssistantOverlayWindow : OverlayWindow
{
    public AssistantOverlayWindow()
    {
        InitializeComponent();
        Opened += (_, _) => PromptTextBox.Focus();
        KeyDown += OnKeyDown;
    }

    public void Initialize(AssistantViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        viewModel.RequestFocusPrompt += () => PromptTextBox.Focus();
        Closed += (_, _) =>
        {
            viewModel.RequestClose -= Close;
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not AssistantViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.HandleEscape();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            if (viewModel.SubmitCommand.CanExecute(null))
            {
                viewModel.SubmitCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}

