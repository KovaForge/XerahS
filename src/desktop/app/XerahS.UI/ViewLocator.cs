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
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI;

public class ViewLocator : IDataTemplate
{
    private static readonly IReadOnlyDictionary<Type, Func<Control>> KnownMappings =
        new Dictionary<Type, Func<Control>>
        {
            [typeof(DebugViewModel)] = static () => new DebugView(),
            [typeof(DestinationSettingsViewModel)] = static () => new DestinationSettingsView(),
            [typeof(HistoryViewModel)] = static () => new HistoryView(),
            [typeof(HotkeySettingsViewModel)] = static () => new HotkeySettingsView(),
            [typeof(IndexFolderViewModel)] = static () => new IndexFolderPanel(),
            [typeof(ProviderCatalogViewModel)] = static () => new ProviderCatalogView(),
            [typeof(ProviderExplorerViewModel)] = static () => new ProviderExplorerView(),
            [typeof(SettingsViewModel)] = static () => new ApplicationSettingsView(),
            [typeof(TaskSettingsViewModel)] = static () => new TaskSettingsPanel(),
            [typeof(WorkflowEditorViewModel)] = static () => new WorkflowEditorView(),
            [typeof(WorkflowsViewModel)] = static () => new WorkflowsView()
        };

    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        Type vmType = data.GetType();
        if (KnownMappings.TryGetValue(vmType, out var createKnownControl))
        {
            Control mapped = createKnownControl();
            mapped.DataContext = data;
            return mapped;
        }

        var name = vmType.FullName!.Replace("ViewModel", "View").Replace("ViewModels", "Views");
        var type = ResolveViewType(name);

        if (type != null)
        {
            var control = (Control)Activator.CreateInstance(type)!;
            control.DataContext = data;
            return control;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    private static Type? ResolveViewType(string fullName)
    {
        Type? type = Type.GetType(fullName, throwOnError: false);
        if (type != null)
        {
            return type;
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(candidate => candidate != null);
    }

    public bool Match(object? data)
    {
        return data is ObservableObject;
    }
}
