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

namespace XerahS.Common.NetworkMonitor;

public static class NetworkMonitorStore
{
    private const string FileName = "NetworkMonitor.json";

    public static string GetDefaultPath()
    {
        return Path.Combine(PathsManager.SettingsFolder, FileName);
    }

    public static NetworkMonitorHistory Load(string? path = null)
    {
        NetworkMonitorHistory history = new();
        string filePath = path ?? GetDefaultPath();
        try
        {
            if (!File.Exists(filePath))
            {
                return history;
            }

            string json = File.ReadAllText(filePath);
            List<NetworkStatusEvent>? events = JsonConvert.DeserializeObject<List<NetworkStatusEvent>>(json);
            if (events != null)
            {
                history.ReplaceEvents(events);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to load network monitor history");
        }

        return history;
    }

    public static void Save(NetworkMonitorHistory history, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        string filePath = path ?? GetDefaultPath();
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(history.Events, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to save network monitor history");
        }
    }
}
