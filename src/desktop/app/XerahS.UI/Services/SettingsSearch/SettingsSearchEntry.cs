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

namespace XerahS.UI.Services.SettingsSearch;

public enum SettingsSearchSource
{
    Catalog,
    VisualTree
}

public enum SettingsSearchArea
{
    Application,
    Destination
}

/// <summary>
/// Immutable search hit. Safe to share across threads as a cached index snapshot.
/// </summary>
public sealed class SettingsSearchEntry
{
    public SettingsSearchEntry(
        string id,
        string title,
        SettingsSearchArea area,
        string pathLabel,
        string navigationTag,
        SettingsSearchSource source,
        string? appTab = null,
        string? destinationCategory = null,
        string? destinationInstance = null,
        IReadOnlyList<string>? keywords = null)
    {
        Id = id;
        Title = title;
        Area = area;
        PathLabel = pathLabel;
        NavigationTag = navigationTag;
        Source = source;
        AppTab = appTab;
        DestinationCategory = destinationCategory;
        DestinationInstance = destinationInstance;
        Keywords = keywords ?? Array.Empty<string>();
        SearchBlob = BuildSearchBlob(title, pathLabel, Keywords);
    }

    public string Id { get; }
    public string Title { get; }
    public SettingsSearchArea Area { get; }
    public string PathLabel { get; }
    public string NavigationTag { get; }
    public SettingsSearchSource Source { get; }
    public string? AppTab { get; }
    public string? DestinationCategory { get; }
    public string? DestinationInstance { get; }
    public IReadOnlyList<string> Keywords { get; }

    /// <summary>Precomputed lower-invariant blob used for fast substring matching.</summary>
    public string SearchBlob { get; }

    public string AreaLabel => Area == SettingsSearchArea.Application ? "Application" : "Destination";

    private static string BuildSearchBlob(string title, string pathLabel, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return string.Concat(title, " ", pathLabel).ToLowerInvariant();
        }

        return string.Concat(title, " ", pathLabel, " ", string.Join(' ', keywords)).ToLowerInvariant();
    }
}
