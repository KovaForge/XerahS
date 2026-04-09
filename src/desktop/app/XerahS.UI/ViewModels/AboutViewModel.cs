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
    public IReadOnlyList<LoadedAssemblyInfoViewModel> XerahSAssemblies { get; }

    public IReadOnlyList<LoadedAssemblyInfoViewModel> SystemAssemblies { get; }

    public IReadOnlyList<LoadedAssemblyInfoViewModel> ThirdPartyAssemblies { get; }

    public int LoadedAssemblyCount => XerahSAssemblyCount + SystemAssemblyCount + ThirdPartyAssemblyCount;

    public int XerahSAssemblyCount => XerahSAssemblies.Count;

    public int SystemAssemblyCount => SystemAssemblies.Count;

    public int ThirdPartyAssemblyCount => ThirdPartyAssemblies.Count;

    public AboutViewModel()
    {
        var groupedAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(CreateAssemblyInfo)
            .ToArray();

        XerahSAssemblies = groupedAssemblies
            .Where(info => info.Group == LoadedAssemblyGroup.XerahS)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SystemAssemblies = groupedAssemblies
            .Where(info => info.Group == LoadedAssemblyGroup.System)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ThirdPartyAssemblies = groupedAssemblies
            .Where(info => info.Group == LoadedAssemblyGroup.ThirdParty)
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LoadedAssemblyInfoViewModel CreateAssemblyInfo(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var name = assemblyName.Name ?? "(Unknown)";

        return new LoadedAssemblyInfoViewModel(
            name,
            ClassifyAssembly(name),
            FormatVersion(assemblyName.Version));
    }

    private static LoadedAssemblyGroup ClassifyAssembly(string assemblyName)
    {
        if (assemblyName.StartsWith("XerahS", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("ShareX.", StringComparison.OrdinalIgnoreCase))
        {
            return LoadedAssemblyGroup.XerahS;
        }

        if (assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase))
        {
            return LoadedAssemblyGroup.System;
        }

        return LoadedAssemblyGroup.ThirdParty;
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

public enum LoadedAssemblyGroup
{
    ThirdParty,
    XerahS,
    System
}

public sealed class LoadedAssemblyInfoViewModel
{
    public string Name { get; }

    public LoadedAssemblyGroup Group { get; }

    public string Version { get; }

    public LoadedAssemblyInfoViewModel(string name, LoadedAssemblyGroup group, string version)
    {
        Name = name;
        Group = group;
        Version = version;
    }
}
