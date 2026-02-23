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
using XerahS.Core;
using XerahS.UI.Services;
using XerahS.UI.Views;

namespace XerahS.UI.Helpers;

/// <summary>
/// Central routing helper for all Tools navigation tags.
/// Used by both menu-bar and navigation-bar flows.
/// </summary>
public static class ToolNavigationHelper
{
    public static bool TryHandleToolsTag(
        string tag,
        Window? owner,
        ContentControl contentFrame,
        Func<WorkflowType, Task> executeWorkflowFromNavigationAsync)
    {
        if (string.IsNullOrEmpty(tag) || !tag.StartsWith("Tools", StringComparison.Ordinal))
        {
            return false;
        }

        switch (tag)
        {
            case "Tools":
                contentFrame.Content = new ToolsView();
                return true;
            case "Tools_IndexFolder":
                ShowIndexFolderWindow(owner);
                return true;
        }

        if (!ToolNavigationRegistry.TryResolve(tag, out var route))
        {
            return false;
        }

        if (route.DispatchMode == ToolNavigationDispatchMode.ExecuteWorkflow)
        {
            _ = executeWorkflowFromNavigationAsync(route.WorkflowType);
            return true;
        }

        if (ToolWorkflowDispatcher.TryDispatch(route.WorkflowType, owner, null, out var dispatchTask))
        {
            _ = dispatchTask;
            return true;
        }

        return false;
    }

    private static void ShowIndexFolderWindow(Window? owner)
    {
        var window = new IndexFolderView();
        if (owner != null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }
}
