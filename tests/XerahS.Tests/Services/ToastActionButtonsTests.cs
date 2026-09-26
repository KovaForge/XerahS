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

using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Services;

[TestFixture]
public class ToastActionButtonsTests
{
    [TestCase(ToastClickAction.CopyImageToClipboard, true)]
    [TestCase(ToastClickAction.AnnotateMedia, true)]
    [TestCase(ToastClickAction.PinToScreen, true)]
    [TestCase(ToastClickAction.Upload, true)]
    [TestCase(ToastClickAction.CopyUrl, false)]
    public void CanExecuteAction_ImageFileWithoutUrl(ToastClickAction action, bool expected)
    {
        var config = new ToastConfig { FilePath = "/tmp/capture.png" };

        Assert.That(ToastViewModel.CanExecuteAction(action, config), Is.EqualTo(expected));
    }

    [TestCase(ToastClickAction.CopyImageToClipboard, false)]
    [TestCase(ToastClickAction.PinToScreen, false)]
    [TestCase(ToastClickAction.AnnotateMedia, true)]
    [TestCase(ToastClickAction.Upload, true)]
    public void CanExecuteAction_VideoFile(ToastClickAction action, bool expected)
    {
        var config = new ToastConfig { FilePath = "/tmp/recording.mp4" };

        Assert.That(ToastViewModel.CanExecuteAction(action, config), Is.EqualTo(expected));
    }

    [Test]
    public void CanExecuteAction_TextOnlyToastHidesFileActions()
    {
        var config = new ToastConfig { Text = "Done" };

        Assert.Multiple(() =>
        {
            Assert.That(ToastViewModel.CanExecuteAction(ToastClickAction.Upload, config), Is.False);
            Assert.That(ToastViewModel.CanExecuteAction(ToastClickAction.OpenUrl, config), Is.False);
            Assert.That(ToastViewModel.CanExecuteAction(ToastClickAction.CloseNotification, config), Is.True);
        });
    }

    [Test]
    public void DefaultTaskSettingsMatchShareXToastButtons()
    {
        var general = new XerahS.Core.TaskSettingsGeneral();

        Assert.That(general.ToastWindowButtons, Is.EqualTo(new[]
        {
            ToastClickAction.CopyImageToClipboard,
            ToastClickAction.AnnotateMedia,
            ToastClickAction.PinToScreen,
            ToastClickAction.Upload
        }));
    }
}
