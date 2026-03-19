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
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class UploadContentToolService
{
    private static UploadContentWindow? _window;

    /// <param name="background">
    /// When <c>true</c>, the window is shown without stealing focus (used by the
    /// clipboard-monitor auto-open path so menus and popups are not disrupted).
    /// </param>
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner, bool background = false)
    {
        ShowWindow(owner, background);

        if (job is WorkflowType.ClipboardUploadWithContentViewer or WorkflowType.ClipboardViewer)
        {
            _window?.ViewModel?.LoadFromClipboardCommand.Execute(null);
        }

        return Task.CompletedTask;
    }

    private static void ShowWindow(Window? owner, bool background)
    {
        if (_window != null)
        {
            try
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                _window.Show();

                if (!background)
                {
                    _window.Activate();
                }

                return;
            }
            catch
            {
                _window = null;
            }
        }

        var viewModel = UiViewModelFactoryAccessor.GetRequired().CreateUploadContentViewModel();
        _window = new UploadContentWindow();
        _window.Initialize(viewModel);

        _window.Closed += (_, _) =>
        {
            _window = null;
        };

        if (owner != null)
        {
            _window.Show(owner);
        }
        else
        {
            _window.Show();
        }

        if (!background)
        {
            _window.Activate();
        }

        DebugHelper.WriteLine($"UploadContent: Window shown (background={background}).");
    }
}
