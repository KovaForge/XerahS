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
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxClipboardServiceTests
{
    [Test]
    [Platform("Linux")]
    public void ToClipboardFileUri_EscapesSpacesAndSpecialCharacters()
    {
        var uri = LinuxClipboardService.ToClipboardFileUri("/tmp/Capture Folder/#1 %.png");

        Assert.That(uri, Is.EqualTo("file:///tmp/Capture%20Folder/%231%20%25.png"));
    }

    [Test]
    public void ParseClipboardFileUri_DecodesFileUrisBackToPaths()
    {
        var path = LinuxClipboardService.ParseClipboardFileUri("file:///tmp/Capture%20Folder/%231%20%25.png");

        Assert.That(path, Is.EqualTo("/tmp/Capture Folder/#1 %.png"));
    }

    [Test]
    public void ParseFileDropList_SkipsBlankLinesAndPreservesPlainPaths()
    {
        var files = LinuxClipboardService.ParseFileDropList("file:///tmp/first%20shot.png\n\n/tmp/plain.txt\r\n");

        Assert.That(files, Is.EqualTo(new[] { "/tmp/first shot.png", "/tmp/plain.txt" }));
    }
}
