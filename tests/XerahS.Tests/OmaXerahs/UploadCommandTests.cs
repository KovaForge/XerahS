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
using XerahS.OmaXerahs.Commands;
using XerahS.OmaXerahs.Models;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
public class UploadCommandTests
{
    [Test]
    public void TryValidateImagePath_RejectsMissingPathAsUsage()
    {
        bool ok = UploadCommand.TryValidateImagePath(null, out _, out string errorCode, out string errorMessage);

        Assert.That(ok, Is.False);
        Assert.That(errorCode, Is.EqualTo(CliErrorCodes.Usage));
        Assert.That(errorMessage, Does.Contain("--"));
    }

    [Test]
    public void TryValidateImagePath_RejectsNonImageAsUnsupportedType()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "omaxerahs-test-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tempFile, "not an image");

        try
        {
            bool ok = UploadCommand.TryValidateImagePath(tempFile, out _, out string errorCode, out string errorMessage);

            Assert.That(ok, Is.False);
            Assert.That(errorCode, Is.EqualTo(CliErrorCodes.UnsupportedType));
            Assert.That(errorMessage, Does.Contain("image"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void TryValidateImagePath_RejectsMissingFileAsInvalidPath()
    {
        string missing = Path.Combine(Path.GetTempPath(), "omaxerahs-missing-" + Guid.NewGuid().ToString("N") + ".png");

        bool ok = UploadCommand.TryValidateImagePath(missing, out _, out string errorCode, out _);

        Assert.That(ok, Is.False);
        Assert.That(errorCode, Is.EqualTo(CliErrorCodes.InvalidPath));
    }

    [Test]
    public void TryValidateImagePath_AcceptsPng()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "screenshot-2026-09-02_14-22-05.png");
        File.WriteAllBytes(tempFile, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        try
        {
            bool ok = UploadCommand.TryValidateImagePath(tempFile, out string canonical, out string errorCode, out string errorMessage);

            Assert.That(ok, Is.True, errorMessage);
            Assert.That(errorCode, Is.Empty);
            Assert.That(canonical, Is.EqualTo(Path.GetFullPath(tempFile)));
            Assert.That(Path.GetFileName(canonical), Is.EqualTo("screenshot-2026-09-02_14-22-05.png"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
