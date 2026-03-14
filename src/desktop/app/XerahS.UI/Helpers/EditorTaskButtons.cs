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
using Avalonia.VisualTree;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;

namespace XerahS.UI.Helpers;

internal static class EditorTaskButtons
{
    public static void SetVisible(EditorView editorView, bool isVisible)
    {
        if (editorView.DataContext is not MainViewModel viewModel)
        {
            return;
        }

        Button? cancelButton = editorView
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => ReferenceEquals(button.Command, viewModel.CancelCommand));

        Button? continueButton = editorView
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => ReferenceEquals(button.Command, viewModel.ContinueCommand));

        if (cancelButton?.Parent is Control container &&
            continueButton != null &&
            ReferenceEquals(container, continueButton.Parent))
        {
            container.IsVisible = isVisible;
            return;
        }

        if (cancelButton != null)
        {
            cancelButton.IsVisible = isVisible;
        }

        if (continueButton != null)
        {
            continueButton.IsVisible = isVisible;
        }
    }
}
