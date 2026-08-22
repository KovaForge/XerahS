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

using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Services.Abstractions;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Services;

public interface IUiViewModelFactory
{
    IViewDialogService ViewDialogService { get; }
    IDialogService CoreDialogService { get; }
    IDesktopTaskManager TaskManager { get; }
    IScreenRecordingCoordinator ScreenRecordingCoordinator { get; }

    ViewModels.CustomUploaderEditorViewModel CreateCustomUploaderEditorViewModel();
    ViewModels.DestinationSettingsViewModel CreateDestinationSettingsViewModel();
    ViewModels.HistoryViewModel CreateHistoryViewModel(bool autoLoadHistory = true);
    ViewModels.IndexFolderViewModel CreateIndexFolderViewModel(TaskSettings? taskSettings = null, bool isWorkflowConfigMode = false);
    ViewModels.PluginInstallerViewModel CreatePluginInstallerViewModel();
    ViewModels.ProviderExplorerViewModel CreateProviderExplorerViewModel(UploaderInstance instance, IUploaderExplorer explorer);
    ViewModels.QrCodeGeneratorViewModel CreateQrCodeGeneratorViewModel();
    ViewModels.WorkflowsViewModel CreateWorkflowsViewModel();
    ViewModels.WorkflowEditorViewModel CreateWorkflowEditorViewModel(WorkflowSettings model, bool loadUploaderCategories = true);
    ViewModels.RecordingViewModel CreateRecordingViewModel();
    ViewModels.AutoCaptureViewModel CreateAutoCaptureViewModel();
    ViewModels.UploadContentViewModel CreateUploadContentViewModel();
    ViewModels.TaskSettingsViewModel CreateTaskSettingsViewModel(TaskSettings settings);
}
