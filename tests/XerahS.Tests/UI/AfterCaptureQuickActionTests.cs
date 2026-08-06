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

using Avalonia.Headless.NUnit;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class AfterCaptureQuickActionTests
{
    private SKBitmap _image = null!;
    private AfterCaptureViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _image = new SKBitmap(2, 2);
        _viewModel = new AfterCaptureViewModel(
            _image,
            AfterCaptureTasks.ShowAfterCaptureWindow | AfterCaptureTasks.UploadImageToHost,
            AfterUploadTasks.CopyURLToClipboard);
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel.PreviewImage.Dispose();
        _image.Dispose();
    }

    [AvaloniaTest]
    public void CopyImageCommand_EndsWorkflowWithOnlyCopyImage()
    {
        bool closeRequested = false;
        _viewModel.RequestClose += () => closeRequested = true;

        _viewModel.CopyImageCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Cancelled, Is.False);
            Assert.That(_viewModel.QuickAction, Is.EqualTo(AfterCaptureQuickAction.CopyImage));
            Assert.That(_viewModel.AfterCaptureTasks, Is.EqualTo(AfterCaptureTasks.CopyImageToClipboard));
            Assert.That(_viewModel.AfterUploadTasks, Is.EqualTo(AfterUploadTasks.None));
            Assert.That(closeRequested, Is.True);
        });
    }

    [AvaloniaTest]
    public void CopyFilePathCommand_EndsWorkflowWithSaveAndCopyPath()
    {
        bool closeRequested = false;
        _viewModel.RequestClose += () => closeRequested = true;

        _viewModel.CopyFilePathCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Cancelled, Is.False);
            Assert.That(_viewModel.QuickAction, Is.EqualTo(AfterCaptureQuickAction.CopyFilePath));
            Assert.That(
                _viewModel.AfterCaptureTasks,
                Is.EqualTo(AfterCaptureTasks.SaveImageToFile | AfterCaptureTasks.CopyFilePathToClipboard));
            Assert.That(_viewModel.AfterUploadTasks, Is.EqualTo(AfterUploadTasks.None));
            Assert.That(closeRequested, Is.True);
        });
    }
}
