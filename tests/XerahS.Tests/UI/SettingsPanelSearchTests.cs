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

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using XerahS.UI.Controls;

namespace XerahS.Tests.UI;

[TestFixture]
[NonParallelizable]
public class SettingsPanelSearchTests
{
    [AvaloniaTest]
    public void Apply_EmptyQuery_ShowsAllPanels()
    {
        StackPanel theme = CreatePanel("Theme", "Application Theme");
        StackPanel proxy = CreatePanel("Proxy", "Proxy method Host Port");
        UserControl root = CreateRoot(theme, proxy);

        SettingsSearch.Apply(root, "");

        Assert.That(theme.IsVisible, Is.True);
        Assert.That(proxy.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public void Apply_HidesNonMatchingPanels()
    {
        StackPanel theme = CreatePanel("Theme", "Application Theme");
        StackPanel proxy = CreatePanel("Proxy", "Proxy method Host Port");
        UserControl root = CreateRoot(theme, proxy);

        SettingsSearch.Apply(root, "proxy");

        Assert.That(theme.IsVisible, Is.False);
        Assert.That(proxy.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public void Apply_AndTerms_RequireAllTokens()
    {
        StackPanel tray = CreatePanel("Tray Icon", "tray progress left click");
        UserControl root = CreateRoot(tray);

        SettingsSearch.Apply(root, "tray click");
        Assert.That(tray.IsVisible, Is.True);

        SettingsSearch.Apply(root, "tray missing");
        Assert.That(tray.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void Matches_IsCaseInsensitive()
    {
        Assert.That(SettingsSearch.Matches("Proxy Configuration", "proxy"), Is.True);
        Assert.That(SettingsSearch.Matches("Proxy Configuration", "PROXY"), Is.True);
    }

    private static UserControl CreateRoot(params Control[] panels)
    {
        var page = new StackPanel();
        SettingsSearch.SetPageId(page, "general");
        foreach (Control panel in panels)
        {
            page.Children.Add(panel);
        }

        return new UserControl { Content = page };
    }

    private static StackPanel CreatePanel(string title, string body)
    {
        var panel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = title },
                new TextBlock { Text = body }
            }
        };
        SettingsSearch.SetIsPanel(panel, true);
        return panel;
    }
}
