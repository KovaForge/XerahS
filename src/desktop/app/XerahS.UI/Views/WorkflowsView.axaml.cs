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
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class WorkflowsView : PageView
{
    public WorkflowsView()
    {
        InitializeComponent();
        DebugHelper.WriteLine("[WorkflowsView] ctor start");
        var vm = UiViewModelFactoryAccessor.GetRequired().CreateWorkflowsViewModel();
        DataContext = vm;
        DebugHelper.WriteLine($"[WorkflowsView] DataContext set: {vm.GetType().Name}, Workflows={vm.Workflows.Count}");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
