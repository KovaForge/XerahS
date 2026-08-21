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
using XerahS.Platform.Abstractions;
using XerahS.WatchFolder.Daemon;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class DaemonOptionsTests
{
    [Test]
    public void Parse_InvalidScope_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => DaemonOptions.Parse(["--scope", "typo"]));

        Assert.That(exception!.Message, Does.Contain("Invalid value for --scope"));
    }

    [Test]
    public void Parse_UserScope_IsAcceptedCaseInsensitively()
    {
        DaemonOptions options = DaemonOptions.Parse(["--scope", "UsEr"]);

        Assert.That(options.Scope, Is.EqualTo(WatchFolderDaemonScope.User));
        Assert.That(options.ScopeExplicitlySet, Is.True);
    }

    [Test]
    public void Parse_ServiceDefaultsToSystemScope_WhenScopeNotExplicit()
    {
        DaemonOptions options = DaemonOptions.Parse(["--service"]);

        Assert.That(options.Scope, Is.EqualTo(WatchFolderDaemonScope.System));
        Assert.That(options.ScopeExplicitlySet, Is.False);
    }
}
