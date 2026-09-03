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
public class BackupSettingsCommandTests
{
    [Test]
    public void Execute_WhenBackupSucceeds_ForwardsOutputPathAndReturnsZero()
    {
        string? capturedPath = null;
        int exitCode = BackupSettingsCommand.Execute(
            "portable.xsbak",
            initializeProviders: () => { },
            createBackup: path =>
            {
                capturedPath = path;
                return new PortableSettingsBackupResult(path, 2, 5, Array.Empty<string>());
            });

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(capturedPath, Is.EqualTo(Path.GetFullPath("portable.xsbak")));
        });
    }

    [Test]
    public void Execute_WithoutOutputPath_UsesVersionedComputerSpecificDefaultFileName()
    {
        string? capturedPath = null;
        int exitCode = BackupSettingsCommand.Execute(
            initializeProviders: () => { },
            createBackup: path =>
            {
                capturedPath = path;
                return new PortableSettingsBackupResult(path, 0, 5, Array.Empty<string>());
            });

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(capturedPath, Is.EqualTo(Path.Combine(Environment.CurrentDirectory, PortableSettingsBackupService.DefaultFileName)));
            Assert.That(Path.GetFileName(capturedPath), Does.Match(@"^xerahs-\d+\.\d+\.\d+-.+-backup\.xsbak$"));
        });
    }

    [Test]
    public void Execute_WhenOutputHasAnotherExtension_ReplacesItWithXsbak()
    {
        string? capturedPath = null;
        int exitCode = BackupSettingsCommand.Execute(
            "portable.zip",
            initializeProviders: () => { },
            createBackup: path =>
            {
                capturedPath = path;
                return new PortableSettingsBackupResult(path, 0, 5, Array.Empty<string>());
            });

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(capturedPath, Is.EqualTo(Path.GetFullPath("portable.xsbak")));
        });
    }

    [Test]
    public void Execute_WhenProviderInitializationFails_ReturnsNonZero()
    {
        int exitCode = BackupSettingsCommand.Execute(
            "portable.xsbak",
            initializeProviders: () => throw new InvalidOperationException("plugins unavailable"),
            createBackup: _ => AssertAndReturnUnexpected());

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void Execute_WhenBackupFails_ReturnsNonZero()
    {
        int exitCode = BackupSettingsCommand.Execute(
            "portable.xsbak",
            initializeProviders: () => { },
            createBackup: _ => throw new IOException("disk unavailable"));

        Assert.That(exitCode, Is.EqualTo(1));
    }

    private static PortableSettingsBackupResult AssertAndReturnUnexpected()
    {
        Assert.Fail("Backup should not run after provider initialization failure.");
        return null!;
    }
}
