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
using XerahS.Assistant.Services;

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class AssistantLocalMemoryStoreTests
{
    [Test]
    public void BuiltInAlias_ResolvesCopyLastFivePaths()
    {
        var store = CreateStore();

        bool resolved = store.TryResolveAlias("copy last five paths", out string command);

        Assert.That(resolved, Is.True);
        Assert.That(command, Does.Contain("last 5 screenshots"));
    }

    [Test]
    public void SavedAlias_RoundTripsCommand()
    {
        var store = CreateStore();
        Assert.That(store.TryParseAliasDefinition("alias bug path = copy the path of the latest screenshot", out var definition), Is.True);

        store.SaveAlias(definition);
        bool resolved = store.TryResolveAlias("bug path", out string command);

        Assert.That(resolved, Is.True);
        Assert.That(command, Is.EqualTo("copy the path of the latest screenshot"));
    }

    [Test]
    public void SavedAlias_WhenNameMatchesBuiltIn_OverridesBuiltInCommand()
    {
        var store = CreateStore();
        Assert.That(store.TryParseAliasDefinition("alias copy last five paths = Show the last five file paths without copying them", out var definition), Is.True);

        store.SaveAlias(definition);
        bool resolved = store.TryResolveAlias("copy last five paths", out string command);

        Assert.That(resolved, Is.True);
        Assert.That(command, Is.EqualTo("Show the last five file paths without copying them"));
    }

    private static AssistantLocalMemoryStore CreateStore()
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new AssistantLocalMemoryStore(Path.Combine(directory, "history.db"));
    }
}
