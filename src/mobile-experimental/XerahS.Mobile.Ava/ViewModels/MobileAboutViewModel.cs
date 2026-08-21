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

namespace Ava.ViewModels;

public class MobileAboutViewModel
{
    public string Version { get; } = GetCleanVersion();

    public string Build { get; } = GetMetadata<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";

    public string VersionText => $"Version {Version}";

    public string PackageId => MobileApp.RuntimePackageId;

    public string PlatformLabel => OperatingSystem.IsIOS() ? "iOS" : OperatingSystem.IsAndroid() ? "Android" : "Platform";

    public string PlatformVersion => Environment.OSVersion.VersionString;

    private static T? GetMetadata<T>() where T : Attribute
        => typeof(MobileAboutViewModel).Assembly.GetCustomAttribute<T>();

    private static string GetCleanVersion()
    {
        var version = GetMetadata<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? GetMetadata<AssemblyFileVersionAttribute>()?.Version;

        return string.IsNullOrWhiteSpace(version) ? "Unknown" : version.Split('+')[0];
    }
}
