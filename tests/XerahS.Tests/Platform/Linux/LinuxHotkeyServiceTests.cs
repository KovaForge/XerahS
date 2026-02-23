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

using Avalonia.Input;
using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

public class LinuxHotkeyServiceTests
{
    [Test]
    public void CandidateKeysymNames_PrintScreen_IncludePrintAliases()
    {
        var names = LinuxHotkeyService.GetCandidateKeysymNames(Key.PrintScreen);

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Print"));
            Assert.That(names, Does.Contain("Sys_Req"));
        });
    }

    [Test]
    public void CandidateKeysymNames_Snapshot_IncludePrintAliases()
    {
        var names = LinuxHotkeyService.GetCandidateKeysymNames(Key.Snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Print"));
            Assert.That(names, Does.Contain("Sys_Req"));
        });
    }

    [Test]
    public void CandidateKeysymNames_LetterKey_MapsToSingleLiteral()
    {
        var names = LinuxHotkeyService.GetCandidateKeysymNames(Key.A);

        Assert.That(names, Is.EqualTo(new[] { "A" }));
    }
}

