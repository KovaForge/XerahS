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

using ShareX.ImageEditor.Core.Editor;
using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Services.Abstractions;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Services;

public sealed class UiViewModelFactory(
    IViewDialogService viewDialogService,
    IDialogService coreDialogService,
    IDesktopTaskManager taskManager,
    IScreenRecordingCoordinator screenRecordingCoordinator) : IUiViewModelFactory
{
    public IViewDialogService ViewDialogService => viewDialogService;
    public IDialogService CoreDialogService => coreDialogService;
    public IDesktopTaskManager TaskManager => taskManager;
    public IScreenRecordingCoordinator ScreenRecordingCoordinator => screenRecordingCoordinator;

    public ViewModels.CustomUploaderEditorViewModel CreateCustomUploaderEditorViewModel() =>
        new();

    public ViewModels.DestinationSettingsViewModel CreateDestinationSettingsViewModel() =>
        new(this);

    public ViewModels.HistoryViewModel CreateHistoryViewModel() =>
        new(taskManager, coreDialogService);

    public ViewModels.IndexFolderViewModel CreateIndexFolderViewModel(TaskSettings? taskSettings = null, bool isWorkflowConfigMode = false) =>
        new(taskSettings, isWorkflowConfigMode, viewDialogService, taskManager);

    public ViewModels.PluginInstallerViewModel CreatePluginInstallerViewModel() =>
        new(viewDialogService);

    public ViewModels.ProviderExplorerViewModel CreateProviderExplorerViewModel(UploaderInstance instance, IUploaderExplorer explorer) =>
        new(instance, explorer, coreDialogService);

    public ViewModels.QrCodeGeneratorViewModel CreateQrCodeGeneratorViewModel() =>
        new(viewDialogService);

    public ViewModels.WorkflowsViewModel CreateWorkflowsViewModel() =>
        new(this);

    public ViewModels.WorkflowEditorViewModel CreateWorkflowEditorViewModel(WorkflowSettings model, bool loadUploaderCategories = true) =>
        new(model, this, loadUploaderCategories);

    public ViewModels.RecordingViewModel CreateRecordingViewModel() =>
        new(screenRecordingCoordinator);

    public ViewModels.AutoCaptureViewModel CreateAutoCaptureViewModel() =>
        new(taskManager);

    public ViewModels.UploadContentViewModel CreateUploadContentViewModel() =>
        new(taskManager);

    public ViewModels.TaskSettingsViewModel CreateTaskSettingsViewModel(TaskSettings settings) =>
        new(settings, viewDialogService, new EditorCore());
}
