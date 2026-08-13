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
using XerahS.Common;
using XerahS.Platform.Windows.Services;

namespace XerahS.Tests.Common;

[TestFixture]
public class StartupInvocationPolicyTests
{
    [TestCase("-silent")]
    [TestCase("-SILENT")]
    public void SilentOnlyInvocation_IsPassive(string argument)
    {
        Assert.That(AppContracts.Cli.IsPassiveStartupInvocation([argument]), Is.True);
    }

    [Test]
    public void ManualSecondaryInvocation_IsInteractive()
    {
        Assert.That(AppContracts.Cli.IsPassiveStartupInvocation([]), Is.False);
    }

    [Test]
    public void SilentInvocationWithUserContent_IsInteractive()
    {
        Assert.That(
            AppContracts.Cli.IsPassiveStartupInvocation([AppContracts.Cli.SilentStartupFlag, @"C:\capture.png"]),
            Is.False);
    }

    [Test]
    public void WindowsStartupCommand_UsesPassiveStartupMarker()
    {
        const string executablePath = @"C:\Program Files\XerahS\XerahS.exe";

        Assert.That(
            WindowsStartupService.GetStartupCommand(executablePath),
            Is.EqualTo("\"C:\\Program Files\\XerahS\\XerahS.exe\" -silent"));
    }

    [TestCase(@"C:\Program Files\XerahS\XerahS.exe", true)]
    [TestCase(@"C:\test\testhost.exe", false)]
    [TestCase(null, false)]
    public void LegacyMigration_OnlyRunsInsideXerahSProcess(string? processPath, bool expected)
    {
        Assert.That(WindowsStartupService.IsXerahSProcess(processPath), Is.EqualTo(expected));
    }
}
