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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using XerahS.UI.Services.SettingsSearch;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class SettingsView : PageView
{
    public SettingsView()
    {
        InitializeComponent();
        var viewModel = new SettingsSearchViewModel();
        DataContext = viewModel;
        viewModel.OpenResultHandler = OpenSearchResult;

        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is SettingsSearchViewModel vm)
            {
                vm.RefreshStatus();
                vm.OpenResultHandler = OpenSearchResult;
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnResultsDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SettingsSearchViewModel vm)
        {
            vm.OpenSelectedResultCommand.Execute(null);
        }
    }

    private void OnResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return))
        {
            return;
        }

        if (DataContext is SettingsSearchViewModel vm)
        {
            vm.OpenSelectedResultCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OpenSearchResult(SettingsSearchEntry entry)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow topLevelWindow)
        {
            topLevelWindow.NavigateToSettingsSearchResult(entry);
            return;
        }

        if (VisualRoot is MainWindow visualRootWindow)
        {
            visualRootWindow.NavigateToSettingsSearchResult(entry);
            return;
        }

        for (Control? current = this; current != null; current = current.Parent as Control)
        {
            if (current is MainWindow window)
            {
                window.NavigateToSettingsSearchResult(entry);
                return;
            }
        }
    }
}
