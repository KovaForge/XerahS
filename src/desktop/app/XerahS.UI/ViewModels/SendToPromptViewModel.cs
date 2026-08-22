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

using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Core.SendTo;

namespace XerahS.UI.ViewModels;

public partial class SendToPromptViewModel : ViewModelBase
{
    private readonly SendToSelection _selection;
    private bool _rememberChoice;
    private int _folderPolicySelectedIndex;
    private int _batchPolicySelectedIndex;
    private int _batchConfirmThreshold;

    public SendToPromptViewModel(SendToSelection selection, ApplicationConfig? settings = null)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        RememberScope = SendToPolicyResolver.GetRememberScope(selection);
        FolderPolicySelectedIndex = SendToPolicyResolver.ToFolderPolicyIndex(settings?.SendToFolderPolicy ?? SendToFolderPolicy.IncludeTopLevelFiles);
        BatchPolicySelectedIndex = SendToPolicyResolver.ToBatchPolicyIndex(settings?.SendToBatchExecutionPolicy ?? SendToBatchExecutionPolicy.ConfirmBeforeOpeningMoreThanThreshold);
        BatchConfirmThreshold = SendToPolicyResolver.NormalizeBatchThreshold(settings?.SendToBatchConfirmThreshold ?? 5);
    }

    public event Action? RequestClose;

    public SendToAction SelectedAction { get; private set; } = SendToAction.Cancel;

    public SendToPromptResult Result => new()
    {
        Action = SelectedAction,
        FolderPolicy = SelectedFolderPolicy,
        RememberChoice = RememberChoice,
        RememberScope = RememberScope,
        BatchExecutionPolicy = SelectedBatchExecutionPolicy,
        BatchConfirmThreshold = BatchConfirmThreshold
    };

    public string[] FolderPolicyOptions { get; } = SendToPolicyResolver.FolderPolicyOptions;

    public string[] BatchPolicyOptions { get; } = SendToPolicyResolver.BatchPolicyOptions;

    public bool RememberChoice
    {
        get => _rememberChoice;
        set => SetProperty(ref _rememberChoice, value);
    }

    public int FolderPolicySelectedIndex
    {
        get => _folderPolicySelectedIndex;
        set
        {
            if (SetProperty(ref _folderPolicySelectedIndex, value))
            {
                OnPropertyChanged(nameof(FolderPolicySummaryText));
                OnPropertyChanged(nameof(UploadNowDescription));
                OnPropertyChanged(nameof(UploadContentDescription));
            }
        }
    }

    public int BatchPolicySelectedIndex
    {
        get => _batchPolicySelectedIndex;
        set
        {
            if (SetProperty(ref _batchPolicySelectedIndex, value))
            {
                OnPropertyChanged(nameof(BatchPolicySummaryText));
                OnPropertyChanged(nameof(ShowBatchThreshold));
            }
        }
    }

    public int BatchConfirmThreshold
    {
        get => _batchConfirmThreshold;
        set
        {
            int normalized = SendToPolicyResolver.NormalizeBatchThreshold(value);
            if (SetProperty(ref _batchConfirmThreshold, normalized))
            {
                OnPropertyChanged(nameof(BatchPolicySummaryText));
            }
        }
    }

    public SendToRememberScope RememberScope { get; }

    public SendToFolderPolicy SelectedFolderPolicy => SendToPolicyResolver.FromFolderPolicyIndex(FolderPolicySelectedIndex);

    public SendToBatchExecutionPolicy SelectedBatchExecutionPolicy => SendToPolicyResolver.FromBatchPolicyIndex(BatchPolicySelectedIndex);

    public bool HasFolders => _selection.HasFolders;

    public bool ShowBatchPolicy => _selection.CanOpenImageEditor || _selection.CanPinToScreen;

    public bool ShowBatchThreshold => SelectedBatchExecutionPolicy == SendToBatchExecutionPolicy.ConfirmBeforeOpeningMoreThanThreshold;

    public string RememberChoiceText => $"Remember this choice for {SendToPolicyResolver.FormatRememberScope(RememberScope)}";

    public string FolderPolicySummaryText => _selection.HasFolders
        ? SelectedFolderPolicy switch
        {
            SendToFolderPolicy.DoNotExpandFolders =>
                "Folder items will be ignored for Upload now and Upload Content.",
            SendToFolderPolicy.IncludeFilesRecursively =>
                "Upload actions will include direct files and all files under sent folders.",
            _ =>
                "Upload actions will include direct files and top-level files from sent folders."
        }
        : "No folders were sent.";

    public string BatchPolicySummaryText => ShowBatchPolicy
        ? SendToPolicyResolver.FormatBatchPolicy(SelectedBatchExecutionPolicy, BatchConfirmThreshold)
        : "Batch image policy applies when every sent file is an image.";

    public string SelectionSummaryText => _selection.Kind switch
    {
        SendToSelectionKind.AllFiles => FormatCount(_selection.FilePaths.Count, "file"),
        SendToSelectionKind.AllFolders => FormatCount(_selection.FolderPaths.Count, "folder"),
        _ => $"{FormatCount(_selection.FilePaths.Count, "file")} and {FormatCount(_selection.FolderPaths.Count, "folder")}"
    };

    public string SelectionKindText => _selection.Kind switch
    {
        SendToSelectionKind.AllFiles when _selection.AllFilesAreImages => "Image files only",
        SendToSelectionKind.AllFiles => "Files only",
        SendToSelectionKind.AllFolders => "Folders only",
        _ => "Mixed files and folders"
    };

    public string SelectionDetailsText => _selection.Kind switch
    {
        SendToSelectionKind.AllFiles when _selection.AllFilesAreImages =>
            "All selected files are image-compatible, so upload, editor, pin, and queue actions are available.",
        SendToSelectionKind.AllFiles =>
            "Only file items were sent. Upload now and Upload Content will use those files directly.",
        SendToSelectionKind.AllFolders =>
            "Only folder items were sent. Upload and Upload Content use the selected folder policy; Index folder preserves folder intent.",
        _ =>
            "Mixed Send-to batches keep file and folder behavior separate. Index folders only applies to folder items."
    };

    public string ActionScopeText => _selection.Kind switch
    {
        SendToSelectionKind.AllFiles when _selection.AllFilesAreImages =>
            "This batch supports upload-first, queue-first, editor, and pin actions.",
        SendToSelectionKind.AllFiles =>
            "This batch supports upload-first and queue-first actions.",
        SendToSelectionKind.AllFolders =>
            "Upload actions use the selected folder policy. Index preserves folder intent.",
        _ =>
            "Upload actions use direct files plus files allowed by the folder policy. Index applies only to folders."
    };

    public string UploadNowDescription => _selection.HasFolders
        ? $"Run the upload workflow using the selected folder policy. {FolderPolicySummaryText}"
        : "Run the current file upload workflow immediately.";

    public string UploadContentDescription => _selection.HasFolders
        ? $"Open Upload Content using the selected folder policy. {FolderPolicySummaryText}"
        : "Open Upload Content and review the sent items before uploading.";

    public string ImageEditorDescription => "Open each sent image in the image editor.";

    public string PinToScreenDescription => "Pin each sent image directly to the desktop.";

    public string IndexActionDescription => _selection.Kind == SendToSelectionKind.Mixed
        ? "Run folder indexing for the sent folder items only."
        : "Run the folder indexing workflow for the sent folders.";

    public string IndexActionLabel => _selection.IndexActionLabel;

    public bool CanOpenImageEditor => _selection.CanOpenImageEditor;

    public bool CanPinToScreen => _selection.CanPinToScreen;

    public bool CanIndexFolders => _selection.CanIndexFolders;

    [RelayCommand]
    private void UploadNow() => CloseWith(SendToAction.UploadNow);

    [RelayCommand]
    private void OpenUploadContent() => CloseWith(SendToAction.OpenUploadContent);

    [RelayCommand]
    private void OpenImageEditor() => CloseWith(SendToAction.OpenImageEditor);

    [RelayCommand]
    private void PinToScreen() => CloseWith(SendToAction.PinToScreen);

    [RelayCommand]
    private void IndexFolders() => CloseWith(SendToAction.IndexFolders);

    [RelayCommand]
    private void Cancel() => CloseWith(SendToAction.Cancel);

    private void CloseWith(SendToAction action)
    {
        SelectedAction = action;
        RequestClose?.Invoke();
    }

    private static string FormatCount(int count, string singular) =>
        count == 1 ? $"1 {singular}" : $"{count} {singular}s";

}
