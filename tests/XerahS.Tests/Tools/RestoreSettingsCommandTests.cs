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
using XerahS.Core.Managers;

namespace XerahS.Tests.Tools;

[TestFixture]
public class RestoreSettingsCommandTests
{
    [Test]
    public void Execute_WithoutInput_ReturnsUsageError()
    {
        Assert.That(RestoreSettingsCommand.Execute(null, force: true), Is.EqualTo(2));
    }

    [Test]
    public void Execute_WithoutForce_DoesNotRestore()
    {
        bool called = false;
        int exitCode = RestoreSettingsCommand.Execute(
            "portable.xsbak",
            force: false,
            restoreBackup: path =>
            {
                called = true;
                return new PortableSettingsRestoreResult(path, 0, 0, Array.Empty<string>());
            });

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(called, Is.False);
        });
    }

    [Test]
    public void Execute_WithForce_ForwardsInputAndReturnsZero()
    {
        string? capturedPath = null;
        int exitCode = RestoreSettingsCommand.Execute(
            "portable.xsbak",
            force: true,
            restoreBackup: path =>
            {
                capturedPath = path;
                return new PortableSettingsRestoreResult(path, 2, 6, Array.Empty<string>());
            });

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(capturedPath, Is.EqualTo(Path.GetFullPath("portable.xsbak")));
        });
    }

    [Test]
    public void Execute_WhenRestoreFails_ReturnsNonZero()
    {
        int exitCode = RestoreSettingsCommand.Execute(
            "portable.xsbak",
            force: true,
            restoreBackup: _ => throw new InvalidDataException("corrupt backup"));

        Assert.That(exitCode, Is.EqualTo(1));
    }
}
