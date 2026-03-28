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

using System.Collections.Concurrent;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Linux.Services;

namespace XerahS.Platform.Linux.Capture.Detection;

internal static class DesktopCaptureInterfaceChecker
{
    private const string GnomeScreenshotBusName = "org.gnome.Shell.Screenshot";
    private const string GnomeScreenshotInterfaceName = "org.gnome.Shell.Screenshot";
    private const string GnomeScreenshotObjectPath = "/org/gnome/Shell/Screenshot";

    private const string KdeScreenshotBusName = "org.kde.KWin.ScreenShot2";
    private const string KdeScreenshotInterfaceName = "org.kde.KWin.ScreenShot2";
    private const string KdeScreenshotObjectPath = "/org/kde/KWin/ScreenShot2";

    private static readonly ConcurrentDictionary<string, bool> Cache = new(StringComparer.Ordinal);

    public static bool HasGnomeShellScreenshotInterface()
    {
        return HasInterface(GnomeScreenshotBusName, GnomeScreenshotObjectPath, GnomeScreenshotInterfaceName);
    }

    public static bool HasKdeScreenShot2Interface()
    {
        return HasInterface(KdeScreenshotBusName, KdeScreenshotObjectPath, KdeScreenshotInterfaceName);
    }

    public static bool HasInterface(string busName, string objectPath, string interfaceName)
    {
        string cacheKey = $"{busName}|{objectPath}|{interfaceName}";
        return Cache.GetOrAdd(cacheKey, _ => CheckInterface(busName, objectPath, interfaceName));
    }

    private static bool CheckInterface(string busName, string objectPath, string interfaceName)
    {
        try
        {
            using var connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();
            var introspectable = connection.CreateProxy<IIntrospectable>(busName, new ObjectPath(objectPath));
            string xml = introspectable.IntrospectAsync().GetAwaiter().GetResult();
            bool found = !string.IsNullOrWhiteSpace(xml) &&
                         (xml.Contains($"interface name=\"{interfaceName}\"", StringComparison.Ordinal) ||
                          xml.Contains(interfaceName, StringComparison.Ordinal));

            DebugHelper.WriteLine(
                $"DesktopCaptureInterfaceChecker: Interface '{interfaceName}' found={found} on '{busName}'.");

            return found;
        }
        catch (DBusException ex) when (
            ex.ErrorName == "org.freedesktop.DBus.Error.ServiceUnknown" ||
            ex.ErrorName == "org.freedesktop.DBus.Error.NameHasNoOwner" ||
            ex.ErrorName == "org.freedesktop.DBus.Error.UnknownObject")
        {
            DebugHelper.WriteLine(
                $"DesktopCaptureInterfaceChecker: Interface '{interfaceName}' unavailable on '{busName}': {ex.ErrorName}");
            return false;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine(
                $"DesktopCaptureInterfaceChecker: Interface probe failed for '{interfaceName}' on '{busName}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
