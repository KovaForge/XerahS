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
using XerahS.UI.Services.SettingsSearch;

namespace XerahS.Tests.UI;

[TestFixture]
[NonParallelizable]
public class SettingsSearchTests
{
    [Test]
    public void Catalog_Search_FindsProxyAlias()
    {
        IReadOnlyList<SettingsSearchEntry> hits = SettingsSearchService.Instance.Search("proxy");
        Assert.That(hits.Any(hit => hit.AppTab == "Proxy" || hit.Title.Contains("Proxy", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void Catalog_Search_FindsDestinationImageUploaders()
    {
        IReadOnlyList<SettingsSearchEntry> hits = SettingsSearchService.Instance.Search("imgur");
        Assert.That(hits.Any(hit =>
            hit.Area == SettingsSearchArea.Destination &&
            (hit.DestinationCategory == "Image Uploaders" || hit.Title.Contains("Image", StringComparison.OrdinalIgnoreCase))), Is.True);
    }

    [AvaloniaTest]
    public void VisualIndexer_IndexesCheckboxLabelsByTab()
    {
        var root = new UserControl
        {
            Content = new TabControl
            {
                Items =
                {
                    new TabItem
                    {
                        Header = "General",
                        Content = new StackPanel
                        {
                            Children =
                            {
                                new CheckBox { Content = "Automatically check for updates" }
                            }
                        }
                    },
                    new TabItem
                    {
                        Header = "Integration",
                        Content = new StackPanel
                        {
                            Children =
                            {
                                new CheckBox { Content = "Show tray icon" }
                            }
                        }
                    }
                }
            }
        };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = root
        };

        try
        {
            window.Show();
            root.UpdateLayout();

            IReadOnlyList<SettingsSearchEntry> entries = SettingsVisualTreeIndexer.IndexApplicationSettings(root);
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(
                entries.Any(entry =>
                    entry.AppTab == "General" &&
                    entry.Title.Contains("Automatically check for updates", StringComparison.OrdinalIgnoreCase)),
                Is.True);
            Assert.That(
                entries.Any(entry =>
                    entry.AppTab == "Integration" &&
                    entry.Title.Contains("Show tray icon", StringComparison.OrdinalIgnoreCase)),
                Is.True);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void TabControl_SelectTabByHeader_SelectsProxy()
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "General" },
                new TabItem { Header = "Proxy" }
            }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = tabs
        };

        try
        {
            window.Show();
            Assert.That(TrySelectTabByHeader(tabs, "Proxy"), Is.True);
            Assert.That(((TabItem)tabs.SelectedItem!).Header?.ToString(), Is.EqualTo("Proxy"));
        }
        finally
        {
            window.Close();
        }
    }

    private static bool TrySelectTabByHeader(TabControl tabs, string tabHeader)
    {
        foreach (object? item in tabs.Items)
        {
            if (item is not TabItem tabItem)
            {
                continue;
            }

            string header = tabItem.Header?.ToString()?.Trim() ?? string.Empty;
            if (string.Equals(header, tabHeader, StringComparison.OrdinalIgnoreCase))
            {
                tabs.SelectedItem = tabItem;
                return true;
            }
        }

        return false;
    }

    [Test]
    public void ExtractCheckboxLabel_PrefersStringContent()
    {
        var checkBox = new CheckBox { Content = "Show tray icon" };
        global::Avalonia.Automation.AutomationProperties.SetName(checkBox, "Show Tray Icon");

        Assert.That(SettingsVisualTreeIndexer.ExtractCheckboxLabel(checkBox), Is.EqualTo("Show tray icon"));
    }
}
