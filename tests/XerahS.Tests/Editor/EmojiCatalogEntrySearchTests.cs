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
using ShareX.ImageEditor.Presentation.Emoji;

namespace XerahS.Tests.Editor;

[TestFixture]
public class EmojiCatalogEntrySearchTests
{
    // SearchIndex uses StringComparison.OrdinalIgnoreCase so case-variant search terms
    // that match group or keyword substrings still reach score 3 instead of falling through
    // to int.MaxValue (absent from results).

    [Test]
    public void GetSearchScore_CaseVariantGroupName_YieldsScore3()
    {
        // Group "Objects" lowercased matches "obj" (ordinal ignore case)
        var entry = new EmojiCatalogEntry
        {
            Name = "keyboard",
            Group = "Objects",
            Unicode = "1f41b",
            Keywords = ["computer", "input", "office"]
        };

        // Case-variant of "objects" (first letter upper) should still hit SearchIndex.Contains
        Assert.That(entry.GetSearchScore("Obj"), Is.EqualTo(3));
    }

    [Test]
    public void GetSearchScore_CaseVariantKeyword_YieldsScore3()
    {
        var entry = new EmojiCatalogEntry
        {
            Name = "laptop computer",
            Group = "Objects",
            Unicode = "1f4bb",
            Keywords = ["computer", "device", "office"]
        };

        // "COMPUTER" (uppercase) should still match the "computer" keyword via case-insensitive
        // StartsWith in score-2, so this test validates OrdinalIgnoreCase on the keyword path.
        Assert.That(entry.GetSearchScore("COMPUTER"), Is.EqualTo(2));
    }

    [Test]
    public void GetSearchScore_ExactNameMatch_YieldsScore0()
    {
        var entry = new EmojiCatalogEntry
        {
            Name = "waving hand",
            Group = "People & Body",
            Unicode = "1f44b",
            Keywords = ["gesture", "hand", "wave"]
        };

        Assert.That(entry.GetSearchScore("waving hand"), Is.EqualTo(0));
    }

    [Test]
    public void GetSearchScore_NamePrefixMatch_YieldsScore1()
    {
        var entry = new EmojiCatalogEntry
        {
            Name = "waving hand",
            Group = "People & Body",
            Unicode = "1f44b",
            Keywords = ["gesture", "hand", "wave"]
        };

        Assert.That(entry.GetSearchScore("waving"), Is.EqualTo(1));
    }

    [Test]
    public void GetSearchScore_KeywordPrefixMatch_YieldsScore2()
    {
        var entry = new EmojiCatalogEntry
        {
            Name = "waving hand",
            Group = "People & Body",
            Unicode = "1f44b",
            Keywords = ["gesture", "hand", "wave"]
        };

        Assert.That(entry.GetSearchScore("gest"), Is.EqualTo(2));
    }

    [Test]
    public void GetSearchScore_MixedCaseSearchTerm_YieldsCorrectScore()
    {
        // Mixed-case search that matches group via ordinal-ignore-case on SearchIndex
        var entry = new EmojiCatalogEntry
        {
            Name = "laptop computer",
            Group = "Objects",
            Unicode = "1f4bb",
            Keywords = ["computer", "device", "office"]
        };

        // "OBJECTS" uppercased — SearchIndex.Contains(..., OrdinalIgnoreCase) should match
        Assert.That(entry.GetSearchScore("OBJECTS"), Is.EqualTo(3));
    }

    [Test]
    public void GetSearchScore_NoMatch_YieldsIntMaxValue()
    {
        var entry = new EmojiCatalogEntry
        {
            Name = "waving hand",
            Group = "People & Body",
            Unicode = "1f44b",
            Keywords = ["gesture", "hand", "wave"]
        };

        Assert.That(entry.GetSearchScore("zzznomatch"), Is.EqualTo(int.MaxValue));
    }
}