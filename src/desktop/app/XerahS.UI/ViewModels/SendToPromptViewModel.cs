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

namespace XerahS.UI.ViewModels;

public partial class SendToPromptViewModel : ViewModelBase
{
    private readonly SendToSelection _selection;

    public SendToPromptViewModel(SendToSelection selection)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    public event Action? RequestClose;

    public SendToAction SelectedAction { get; private set; } = SendToAction.Cancel;

    public SendToPromptResult Result => new()
    {
        Action = SelectedAction
    };

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
            "Only folder items were sent. Upload and Upload Content use top-level files from those folders; Index folder preserves folder intent.",
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
            "Upload actions use top-level files from each folder. Index preserves folder intent.",
        _ =>
            "Upload actions use files plus top-level folder files. Index applies only to folders."
    };

    public string UploadNowDescription => _selection.HasFolders
        ? "Run the upload workflow for direct files and top-level files resolved from sent folders."
        : "Run the current file upload workflow immediately.";

    public string UploadContentDescription => _selection.HasFolders
        ? "Open Upload Content with direct files and top-level files resolved from sent folders."
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
