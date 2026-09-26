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
using System.Collections.Generic;
using System.Linq;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.UI.Views.Dialogs;

namespace XerahS.UI;

public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// Explicit VM→View maps for names that do not follow ViewModel→View,
    /// including ModalContent dialogs hosted via <c>ModalDialogHost</c>.
    /// </summary>
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
            [typeof(WorkflowsViewModel)] = static () => new WorkflowsView(),

            // ModalDialogHost / ModalContent (non-conventional view names)
            [typeof(CustomUploaderEditorViewModel)] = static () => new CustomUploaderEditorDialog(),
            [typeof(PluginInstallerViewModel)] = static () => new PluginInstallerDialog(),
            [typeof(ImageEffectsViewModel)] = static () => new ImageEffectsBrowserDialog(),
            [typeof(FFmpegOptionsViewModel)] = static () => new FFmpegOptionsWindow(),
            [typeof(QrCodeGeneratorViewModel)] = static () => new QrCodeGeneratorDialog(),
            [typeof(WatchFolderEditViewModel)] = static () => new WatchFolderDialog(),
            [typeof(OpenImageChoiceViewModel)] = static () => new OpenImageChoiceDialog(),
            [typeof(WindowSelectorViewModel)] = static () => new WindowSelectorDialog(),
            [typeof(UpdateMessageBoxViewModel)] = static () => new UpdateMessageBox(),
            [typeof(AfterCaptureViewModel)] = static () => new AfterCaptureWindow(),
            [typeof(SendToPromptViewModel)] = static () => new SendToPromptWindow(),
            [typeof(SimplePromptViewModel)] = static () => new SimplePromptView(),
        };

    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        Type vmType = data.GetType();
        if (TryCreateControl(vmType, out Control? mapped) && mapped != null)
        {
            mapped.DataContext = data;
            return mapped;
        }

        var name = GetConventionViewTypeName(vmType);
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        // Only claim ObservableObjects we can actually resolve. Application.DataTemplates
        // lists ViewLocator first; a blanket Match stole typed DataTemplates in App.axaml
        // and produced "Not Found: …View" for ModalContent dialogs with non-conventional names.
        if (data is not ObservableObject)
        {
            return false;
        }

        return CanResolve(data.GetType());
    }

    private static bool CanResolve(Type vmType)
    {
        if (KnownMappings.ContainsKey(vmType))
        {
            return true;
        }

        return ResolveViewType(GetConventionViewTypeName(vmType)) != null;
    }

    private static bool TryCreateControl(Type vmType, out Control? control)
    {
        if (KnownMappings.TryGetValue(vmType, out var createKnownControl))
        {
            control = createKnownControl();
            return true;
        }

        var type = ResolveViewType(GetConventionViewTypeName(vmType));
        if (type != null)
        {
            control = (Control)Activator.CreateInstance(type)!;
            return true;
        }

        control = null;
        return false;
    }

    private static string GetConventionViewTypeName(Type vmType)
    {
        return vmType.FullName!.Replace("ViewModel", "View").Replace("ViewModels", "Views");
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
}
