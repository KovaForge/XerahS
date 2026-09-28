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

#nullable enable

using NUnit.Framework;
using XerahS.UI.Helpers;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class StartupReadinessTests
{
    [Test]
    public async Task AlreadyReady_CompletesTrue()
    {
        Assert.That(await StartupReadiness.WaitUntilAsync(() => true, TimeSpan.FromMilliseconds(10)), Is.True);
    }

    [Test]
    public async Task BecomesReady_CompletesTrue()
    {
        int calls = 0;
        Assert.That(await StartupReadiness.WaitUntilAsync(() => ++calls >= 3, TimeSpan.FromSeconds(5)), Is.True);
    }

    [Test]
    public async Task NeverReady_TimesOutFalse()
    {
        Assert.That(await StartupReadiness.WaitUntilAsync(() => false, TimeSpan.FromMilliseconds(300)), Is.False);
    }
}
