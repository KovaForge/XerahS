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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XerahS.Uploaders.CustomUploader;

public static class CustomUploaderSettingsSerializer
{
    private static readonly JsonSerializerSettings InstanceSettings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore
    };

    public static string SerializeForInstance(CustomUploaderItem item, string? fallbackName = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var token = JObject.FromObject(item, JsonSerializer.Create(InstanceSettings));
        var resolvedName = string.IsNullOrWhiteSpace(item.Name) ? fallbackName : item.Name;

        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            token[nameof(CustomUploaderItem.Name)] = resolvedName;
        }

        return token.ToString(Formatting.Indented);
    }
}
