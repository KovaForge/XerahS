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
using XerahS.Core;
using XerahS.Core.SendTo;

namespace XerahS.Tests.SendTo;

[TestFixture]
public class SendToSelectionClassifierTests
{
    [Test]
    public void Create_WithOnlyImageFiles_ClassifiesAsAllFiles()
    {
        SendToSelection selection = SendToSelectionClassifier.Create(
            ["C:\\captures\\one.png", "C:\\captures\\two.jpg"],
            []);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Kind, Is.EqualTo(SendToSelectionKind.AllFiles));
            Assert.That(selection.AllFilesAreImages, Is.True);
            Assert.That(selection.CanOpenImageEditor, Is.True);
            Assert.That(selection.CanPinToScreen, Is.True);
            Assert.That(selection.CanIndexFolders, Is.False);
        });
    }

    [Test]
    public void Create_WithOnlyFolders_ClassifiesAsAllFolders()
    {
        SendToSelection selection = SendToSelectionClassifier.Create(
            [],
            ["C:\\captures\\first", "C:\\captures\\second"]);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Kind, Is.EqualTo(SendToSelectionKind.AllFolders));
            Assert.That(selection.HasFolders, Is.True);
            Assert.That(selection.HasFiles, Is.False);
            Assert.That(selection.CanIndexFolders, Is.True);
            Assert.That(selection.IndexActionLabel, Is.EqualTo("Index folders"));
        });
    }

    [Test]
    public void Create_WithFilesAndFolders_ClassifiesAsMixed()
    {
        SendToSelection selection = SendToSelectionClassifier.Create(
            ["C:\\captures\\one.png"],
            ["C:\\captures\\folder"]);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Kind, Is.EqualTo(SendToSelectionKind.Mixed));
            Assert.That(selection.CanIndexFolders, Is.True);
            Assert.That(selection.IndexActionLabel, Is.EqualTo("Index folders only"));
            Assert.That(selection.CanOpenImageEditor, Is.False);
            Assert.That(selection.CanPinToScreen, Is.False);
        });
    }

    [Test]
    public void Create_WithNonImageFiles_DoesNotEnableImageActions()
    {
        SendToSelection selection = SendToSelectionClassifier.Create(
            ["C:\\captures\\document.txt", "C:\\captures\\photo.png"],
            []);

        Assert.Multiple(() =>
        {
            Assert.That(selection.Kind, Is.EqualTo(SendToSelectionKind.AllFiles));
            Assert.That(selection.AllFilesAreImages, Is.False);
            Assert.That(selection.CanOpenImageEditor, Is.False);
            Assert.That(selection.CanPinToScreen, Is.False);
        });
    }
}
