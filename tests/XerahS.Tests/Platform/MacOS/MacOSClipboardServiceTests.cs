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
using XerahS.Platform.MacOS;
using System.IO;
using System.Linq;

namespace XerahS.Tests.Platform.MacOS;

[TestFixture]
public class MacOSClipboardServiceTests
{
    [Test]
    public void ParseFileDropList_SplitsAppleScriptCarriageReturnSeparatedPaths()
    {
        var files = MacOSClipboardService.ParseFileDropList("/tmp/first.png\r/tmp/second.png\r");

        Assert.That(files, Is.EqualTo(new[] { "/tmp/first.png", "/tmp/second.png" }));
    }

    [Test]
    public void ParseFileDropList_SkipsBlankLinesAcrossLineEndings()
    {
        var files = MacOSClipboardService.ParseFileDropList("/tmp/first.png\r\n\n/tmp/second.png\r\r");

        Assert.That(files, Is.EqualTo(new[] { "/tmp/first.png", "/tmp/second.png" }));
    }

    [Test]
    [Platform("MacOsX")]
    public void BuildPosixFileList_NormalizesRelativePathsBeforeAppleScriptClipboardSet()
    {
        var relativePath = Path.Combine("relative", "capture.png");

        var specifier = MacOSClipboardService.BuildPosixFileList(new[] { relativePath }).Single();

        Assert.That(specifier, Is.EqualTo($"POSIX file \\\"{Path.GetFullPath(relativePath)}\\\""));
    }

    [Test]
    [Platform("MacOsX")]
    public void BuildPosixFileList_PreservesSignificantFilenameWhitespace()
    {
        var pathWithTrailingSpace = Path.Combine(Path.GetTempPath(), "capture ");

        var specifier = MacOSClipboardService.BuildPosixFileList(new[] { pathWithTrailingSpace }).Single();

        Assert.That(specifier, Is.EqualTo($"POSIX file \\\"{Path.GetFullPath(pathWithTrailingSpace)}\\\""));
    }

    [Test]
    public void BuildPosixFileList_SkipsInvalidOrBlankPaths()
    {
        var specifiers = MacOSClipboardService.BuildPosixFileList(new[] { "", "   ", "\0invalid" });

        Assert.That(specifiers, Is.Empty);
    }

    [Test]
    public void CreateOsaScriptStartInfo_PassesScriptAsSingleArgument()
    {
        const string script = "set the clipboard to \"quoted value\"";

        var startInfo = MacOSClipboardService.CreateOsaScriptStartInfo(script);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("osascript"));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.RedirectStandardOutput, Is.True);
            Assert.That(startInfo.RedirectStandardError, Is.True);
            Assert.That(startInfo.Arguments, Is.Empty);
            Assert.That(startInfo.ArgumentList, Has.Count.EqualTo(2));
            Assert.That(startInfo.ArgumentList[0], Is.EqualTo("-e"));
            Assert.That(startInfo.ArgumentList[1], Is.EqualTo(script));
        });
    }
}
