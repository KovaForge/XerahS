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

using System.Reflection;
using NUnit.Framework;
using ShareX.ImageEditor.Presentation.Theming;
using XerahS.UI.Theming;

namespace XerahS.Tests.Theming;

[TestFixture]
public class HostIconsTests
{
    private static IEnumerable<FieldInfo> ConstStrings(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string));

    [Test]
    public void EveryHostIconIsALucideGlyph()
    {
        var lucideGlyphs = ConstStrings(typeof(LucideIcons))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet();

        var missing = ConstStrings(typeof(HostIcons))
            .Where(field => !lucideGlyphs.Contains((string)field.GetRawConstantValue()!))
            .Select(field => field.Name)
            .ToList();

        Assert.That(missing, Is.Empty, "HostIcons must use glyphs from the bundled Lucide font.");
    }

    [Test]
    public void WorkflowActionIconsUseExpectedGlyphs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HostIcons.ActionAdd, Is.EqualTo(LucideIcons.plus));
            Assert.That(HostIcons.ActionMoveUp, Is.EqualTo(LucideIcons.move_up));
            Assert.That(HostIcons.ActionMoveDown, Is.EqualTo(LucideIcons.move_down));
        });
    }
}
