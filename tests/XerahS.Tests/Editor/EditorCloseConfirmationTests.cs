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
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.Tests.Xip0052;
using XerahS.UI.Views;

namespace XerahS.Tests.Editor;

[TestFixture]
public class EditorCloseConfirmationTests
{
    [Test]
    public void RequestClose_DoesNotCreateDuplicateConfirmation_WhenModalAlreadyOpen()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions
        {
            ShowExitConfirmation = true
        })
        {
            IsDirty = true
        };

        viewModel.RequestClose();
        object? initialModal = viewModel.ModalContent;

        viewModel.RequestClose();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsModalOpen, Is.True);
            Assert.That(initialModal, Is.Not.Null);
            Assert.That(viewModel.ModalContent, Is.SameAs(initialModal));
        });
    }

    [AvaloniaTest]
    public void MainWindow_Hides_Shell_ModalOverlay_For_Embedded_Editor_Modal()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions
        {
            ShowExitConfirmation = true
        })
        {
            IsDirty = true
        };
        var taskManager = new FakeDesktopTaskManager();

        var window = new MainWindow(taskManager)
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        try
        {
            window.Show();
            viewModel.RequestClose();

            var contentFrame = window.FindControl<ContentControl>("ContentFrame");
            var overlay = window.FindControl<Grid>("MainWindowModalOverlay");

            Assert.Multiple(() =>
            {
                Assert.That(contentFrame?.Content, Is.TypeOf<EditorView>());
                Assert.That(viewModel.IsModalOpen, Is.True);
                Assert.That(overlay?.IsVisible, Is.False);
            });
        }
        finally
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }

    [AvaloniaTest]
    public void MainWindow_Shows_Shell_ModalOverlay_For_NonEditor_Content()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions());
        var taskManager = new FakeDesktopTaskManager();
        var window = new MainWindow(taskManager)
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        try
        {
            window.Show();
            window.NavigateToSettings();

            viewModel.ModalContent = new object();
            viewModel.IsModalOpen = true;

            var contentFrame = window.FindControl<ContentControl>("ContentFrame");
            var overlay = window.FindControl<Grid>("MainWindowModalOverlay");

            Assert.Multiple(() =>
            {
                Assert.That(contentFrame?.Content, Is.Not.TypeOf<EditorView>());
                Assert.That(overlay?.IsVisible, Is.True);
            });
        }
        finally
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }
}
