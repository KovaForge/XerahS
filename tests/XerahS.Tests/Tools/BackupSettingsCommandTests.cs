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
public class BackupSettingsCommandTests
{
    [Test]
    public void Execute_WhenBackupSucceeds_ReturnsZero()
    {
        int exitCode = BackupSettingsCommand.Execute(
            loadInitialSettings: () => { },
            saveAllSettings: () => { },
            getBackupFolder: () => "/tmp/xerahs-backups");

        Assert.That(exitCode, Is.EqualTo(0));
    }

    [Test]
    public void Execute_WhenSettingsLoadFails_ReturnsNonZero()
    {
        int exitCode = BackupSettingsCommand.Execute(
            loadInitialSettings: () => throw new InvalidOperationException("settings unavailable"),
            saveAllSettings: () => Assert.Fail("Backup should not run after settings load failure."),
            getBackupFolder: () => "/tmp/xerahs-backups");

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void Execute_WhenBackupFails_ReturnsNonZero()
    {
        int exitCode = BackupSettingsCommand.Execute(
            loadInitialSettings: () => { },
            saveAllSettings: () => throw new IOException("disk unavailable"),
            getBackupFolder: () => "/tmp/xerahs-backups");

        Assert.That(exitCode, Is.EqualTo(1));
    }
}
