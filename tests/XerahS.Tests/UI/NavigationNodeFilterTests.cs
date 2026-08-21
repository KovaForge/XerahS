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
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

[TestFixture]
public class NavigationNodeFilterTests
{
    [Test]
    public void ApplyFilter_EmptyQuery_ShowsAllNodes()
    {
        NavigationNode tools = new("Tools", "Tools", null, NavigationNodeKind.Page, "tools utilities");
        tools.AddChild(new NavigationNode("Ruler", "Tools_Ruler", null, NavigationNodeKind.Action, "ruler measure"));
        tools.AddChild(new NavigationNode("Color Picker...", "Tools_ColorPicker", null, NavigationNodeKind.Action, "color"));

        Assert.That(tools.ApplyFilter(""), Is.True);
        Assert.That(tools.IsVisible, Is.True);
        Assert.That(tools.Children.All(child => child.IsVisible), Is.True);
    }

    [Test]
    public void ApplyFilter_HidesNonMatchingSiblings_KeepsParentWhenChildMatches()
    {
        NavigationNode tools = new("Tools", "Tools", null, NavigationNodeKind.Page);
        NavigationNode ruler = new("Ruler", "Tools_Ruler", null, NavigationNodeKind.Action, "ruler measure");
        NavigationNode color = new("Color Picker...", "Tools_ColorPicker", null, NavigationNodeKind.Action, "color picker");
        tools.AddChild(ruler);
        tools.AddChild(color);

        Assert.That(tools.ApplyFilter("ruler"), Is.True);
        Assert.That(tools.IsVisible, Is.True);
        Assert.That(ruler.IsVisible, Is.True);
        Assert.That(color.IsVisible, Is.False);
        Assert.That(tools.IsExpanded, Is.True);
    }

    [Test]
    public void ApplyFilter_AndTerms_RequireAllTokens()
    {
        NavigationNode node = new("Video Converter...", "Tools_VideoConverter", null, NavigationNodeKind.Action, "video convert ffmpeg");

        Assert.That(node.ApplyFilter("video ffmpeg"), Is.True);
        Assert.That(node.ApplyFilter("video missing"), Is.False);
    }

    [Test]
    public void ApplyFilter_UsesSearchTextKeywords()
    {
        NavigationNode settings = new("Application Settings", "Settings_App", null, NavigationNodeKind.Page, "proxy theme tray");

        Assert.That(settings.ApplyFilter("proxy"), Is.True);
        Assert.That(settings.ApplyFilter("imgur"), Is.False);
    }
}
