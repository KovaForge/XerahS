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

using System.Drawing;
using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Wayland.WindowQuery;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class WaylandWindowPointQueryHelperTests
{
    [Test]
    public void HyprlandHelper_SelectWindowFromClientsJson_FiltersOverlayAndPrefersFocusedMatch()
    {
        string json =
            $$"""
            [
              {
                "address": "0x111",
                "mapped": true,
                "hidden": false,
                "at": [0, 0],
                "size": [1920, 1080],
                "title": "{{PlatformWindowTitles.RegionCaptureOverlay}}",
                "class": "xerahs",
                "focusHistoryID": 0
              },
              {
                "address": "0x222",
                "mapped": true,
                "hidden": false,
                "at": [10, 10],
                "size": [900, 700],
                "title": "Terminal",
                "class": "org.wezfurlong.wezterm",
                "focusHistoryID": 2
              },
              {
                "address": "0x333",
                "mapped": true,
                "hidden": false,
                "at": [50, 50],
                "size": [600, 400],
                "title": "Notes",
                "class": "org.example.Notes",
                "focusHistoryID": 0
              }
            ]
            """;

        var window = HyprlandWindowPointQueryHelper.SelectWindowFromClientsJson(json, new Point(120, 120));

        Assert.That(window, Is.Not.Null);
        Assert.That(window!.Handle, Is.EqualTo((nint)0x333));
        Assert.That(window.Title, Is.EqualTo("Notes"));
    }

    [Test]
    public void HyprlandHelper_SelectWindowFromClientsJson_IgnoresMalformedCoordinateArrays()
    {
        const string json = """
            [
              {
                "address": "0x444",
                "mapped": true,
                "hidden": false,
                "at": ["not-an-int", 10],
                "size": [800, 600],
                "title": "Broken metadata",
                "class": "org.example.Broken",
                "focusHistoryID": 0
              }
            ]
            """;

        WindowInfo? window = null;

        Assert.DoesNotThrow(() =>
        {
            window = HyprlandWindowPointQueryHelper.SelectWindowFromClientsJson(json, new Point(20, 20));
        });
        Assert.That(window, Is.Null);
    }

    [Test]
    public void SwayHelper_SelectWindowFromTreeJson_ReturnsFocusedWindowAtPoint()
    {
        string json =
            $$"""
            {
              "id": 1,
              "type": "root",
              "nodes": [
                {
                  "id": 2,
                  "type": "output",
                  "nodes": [
                    {
                      "id": 3,
                      "type": "workspace",
                      "focus": [30, 20],
                      "nodes": [
                        {
                          "id": 20,
                          "type": "con",
                          "name": "Terminal",
                          "app_id": "org.example.Terminal",
                          "visible": true,
                          "rect": { "x": 0, "y": 0, "width": 1000, "height": 800 },
                          "nodes": [],
                          "floating_nodes": []
                        }
                      ],
                      "floating_nodes": [
                        {
                          "id": 30,
                          "type": "floating_con",
                          "name": "Dialog",
                          "app_id": "org.example.Dialog",
                          "visible": true,
                          "rect": { "x": 100, "y": 120, "width": 400, "height": 200 },
                          "nodes": [],
                          "floating_nodes": []
                        },
                        {
                          "id": 40,
                          "type": "floating_con",
                          "name": "{{PlatformWindowTitles.RegionCaptureOverlay}}",
                          "app_id": "xerahs",
                          "visible": true,
                          "rect": { "x": 0, "y": 0, "width": 1600, "height": 900 },
                          "nodes": [],
                          "floating_nodes": []
                        }
                      ]
                    }
                  ],
                  "floating_nodes": []
                }
              ],
              "floating_nodes": []
            }
            """;

        var window = SwayWindowPointQueryHelper.SelectWindowFromTreeJson(json, new Point(150, 150));

        Assert.That(window, Is.Not.Null);
        Assert.That(window!.Handle, Is.EqualTo((nint)30));
        Assert.That(window.Title, Is.EqualTo("Dialog"));
    }

    [Test]
    public void GnomeHelper_ParseEvalResult_ProjectsWindowMetadata()
    {
        const string json = """
            {"stableSequence": 77, "title": "Files", "className": "org.gnome.Nautilus", "x": 120, "y": 80, "width": 1440, "height": 900}
            """;

        var window = GnomeShellWindowPointQueryHelper.ParseEvalResult(json);

        Assert.That(window, Is.Not.Null);
        Assert.That(window!.Handle, Is.EqualTo((nint)77));
        Assert.That(window.Bounds, Is.EqualTo(new Rectangle(120, 80, 1440, 900)));
        Assert.That(window.ClassName, Is.EqualTo("org.gnome.Nautilus"));
    }

    [Test]
    public void KdeKdotoolHelper_ParsesMouseLocationAndGeometry()
    {
        const string mouseOutput = """
            X=400
            Y=300
            SCREEN=0
            WINDOW={12345678-90ab-cdef-1234-567890abcdef}
            """;

        const string geometryOutput = """
            Window {12345678-90ab-cdef-1234-567890abcdef}
              Position: 200,100 (screen: 0)
              Geometry: 1200x800
            """;

        bool parsedWindow = KdeKdotoolWindowPointQueryHelper.TryParseMouseLocationWindowId(mouseOutput, out string windowId);
        bool parsedGeometry = KdeKdotoolWindowPointQueryHelper.TryParseWindowGeometry(geometryOutput, out Rectangle bounds);

        Assert.Multiple(() =>
        {
            Assert.That(parsedWindow, Is.True);
            Assert.That(windowId, Is.EqualTo("{12345678-90ab-cdef-1234-567890abcdef}"));
            Assert.That(parsedGeometry, Is.True);
            Assert.That(bounds, Is.EqualTo(new Rectangle(200, 100, 1200, 800)));
        });
    }

    [Test]
    public void KdeKdotoolHelper_TryParseWindowGeometry_RejectsOverflowingNumbers()
    {
        const string geometryOutput = """
            Window {12345678-90ab-cdef-1234-567890abcdef}
              Position: 200,999999999999999999999999999999 (screen: 0)
              Geometry: 1200x800
            """;

        Rectangle bounds = Rectangle.Empty;

        Assert.DoesNotThrow(() =>
        {
            bool parsedGeometry = KdeKdotoolWindowPointQueryHelper.TryParseWindowGeometry(geometryOutput, out bounds);
            Assert.That(parsedGeometry, Is.False);
        });
        Assert.That(bounds, Is.EqualTo(Rectangle.Empty));
    }
}
