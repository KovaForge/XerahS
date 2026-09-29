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
using XerahS.UI.Helpers;

namespace XerahS.Tests.UI;

public class NamePatternMenuTests
{
    [Test]
    public void GetEntries_OmitsIgnoredTokens()
    {
        var entries = NamePatternMenu.GetEntries(CodeMenuEntryFilename.n, CodeMenuEntryFilename.t, CodeMenuEntryFilename.pn);

        Assert.Multiple(() =>
        {
            Assert.That(entries, Does.Not.Contain(CodeMenuEntryFilename.n));
            Assert.That(entries, Does.Not.Contain(CodeMenuEntryFilename.t));
            Assert.That(entries, Does.Not.Contain(CodeMenuEntryFilename.pn));
            Assert.That(entries, Does.Contain(CodeMenuEntryFilename.y));
            Assert.That(entries, Does.Contain(CodeMenuEntryFilename.ra));
        });
    }

    [Test]
    public void BuildGroups_UsesCategoryAndPrefixedPattern()
    {
        var groups = NamePatternMenu.BuildGroups(CodeMenuEntryFilename.n);
        var dateGroup = groups.Single(group => group.Category == "Date and Time");
        var year = dateGroup.Items.Single(item => item.Pattern == "%y");

        Assert.That(year.Header, Is.EqualTo("%y - Current year"));
    }

    [Test]
    public void InsertAtSelection_InsertsAtCaretWithoutReplacing()
    {
        (string text, int caret) = NamePatternMenu.InsertAtSelection("capture", 3, 5, "%y");

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("cap%yture"));
            Assert.That(caret, Is.EqualTo(5));
        });
    }
}
