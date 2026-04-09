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

namespace XerahS.UI.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public IReadOnlyList<LoadedAssemblyInfoViewModel> LoadedAssemblies { get; }

    public int LoadedAssemblyCount => LoadedAssemblies.Count;

    public AboutViewModel()
    {
        LoadedAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(CreateAssemblyInfo)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LoadedAssemblyInfoViewModel CreateAssemblyInfo(Assembly assembly)
    {
        var assemblyName = assembly.GetName();

        return new LoadedAssemblyInfoViewModel(
            assemblyName.Name ?? "(Unknown)",
            FormatVersion(assemblyName.Version));
    }

    private static string FormatVersion(Version? version)
    {
        if (version == null)
        {
            return "Unknown";
        }

        return version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}.{version.Revision}"
            : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}

public sealed class LoadedAssemblyInfoViewModel
{
    public string Name { get; }

    public string Version { get; }

    public LoadedAssemblyInfoViewModel(string name, string version)
    {
        Name = name;
        Version = version;
    }
}
