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
using XerahS.CLI.Commands;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class ReClipCommandWatchFolderValidationTests
{
    [Test]
    public void TryValidateWatchFolder_WhenNullOrWhitespace_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReClipCommand.TryValidateWatchFolder(null, out var p1, out var e1), Is.False);
            Assert.That(p1, Is.Empty);
            Assert.That(e1, Does.Contain("required"));

            Assert.That(ReClipCommand.TryValidateWatchFolder("   ", out var p2, out var e2), Is.False);
            Assert.That(p2, Is.Empty);
            Assert.That(e2, Does.Contain("required"));
        });
    }

    [Test]
    public void TryValidateWatchFolder_WhenEmbeddedNull_ReturnsFalse()
    {
        bool ok = ReClipCommand.TryValidateWatchFolder("bad\0folder", out var fullPath, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(fullPath, Is.Empty);
            Assert.That(error, Does.Contain("null character").IgnoreCase);
        });
    }

    [Test]
    public void TryValidateWatchFolder_WhenParentDirectorySegment_ReturnsFalse()
    {
        bool ok = ReClipCommand.TryValidateWatchFolder("../../malicious/path", out var fullPath, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(fullPath, Is.Empty);
            Assert.That(error, Does.Contain(".."));
        });
    }

    [Test]
    public void TryValidateWatchFolder_WhenFilesystemRoot_ReturnsFalse()
    {
        string root = Path.GetPathRoot(Path.GetTempPath()) ?? "/";
        bool ok = ReClipCommand.TryValidateWatchFolder(root, out var fullPath, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(fullPath, Is.Empty);
            Assert.That(error, Does.Contain("filesystem root").IgnoreCase);
        });
    }

    [Test]
    public void TryValidateWatchFolder_WhenAbsoluteConcretePath_ReturnsTrueAndCanonicalizes()
    {
        string temp = Path.Combine(Path.GetTempPath(), "xerahs-reclip-" + Guid.NewGuid().ToString("N"));
        try
        {
            bool ok = ReClipCommand.TryValidateWatchFolder(temp, out var fullPath, out var error);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(error, Is.Null);
                Assert.That(fullPath, Is.EqualTo(Path.GetFullPath(temp)));
            });
        }
        finally
        {
            // validator must not create the directory
            Assert.That(Directory.Exists(temp), Is.False);
        }
    }
}
