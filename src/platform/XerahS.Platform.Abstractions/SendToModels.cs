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

namespace XerahS.Core;

public enum SendToSelectionKind
{
    AllFiles,
    AllFolders,
    Mixed
}

public enum SendToAction
{
    UploadNow,
    OpenUploadContent,
    OpenImageEditor,
    PinToScreen,
    IndexFolders,
    Cancel
}

public sealed class SendToSelection
{
    public IReadOnlyList<string> FilePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> FolderPaths { get; init; } = Array.Empty<string>();

    public SendToSelectionKind Kind { get; init; }

    public bool AllFilesAreImages { get; init; }

    public int ItemCount => FilePaths.Count + FolderPaths.Count;

    public bool HasFiles => FilePaths.Count > 0;

    public bool HasFolders => FolderPaths.Count > 0;

    public bool CanOpenImageEditor => HasFiles && !HasFolders && AllFilesAreImages;

    public bool CanPinToScreen => CanOpenImageEditor;

    public bool CanIndexFolders => HasFolders;

    public string IndexActionLabel => Kind == SendToSelectionKind.Mixed
        ? "Index folders only"
        : FolderPaths.Count == 1
            ? "Index folder"
            : "Index folders";

    public string ClassificationLabel => Kind switch
    {
        SendToSelectionKind.AllFiles => "allFiles",
        SendToSelectionKind.AllFolders => "allFolders",
        _ => "mixed"
    };
}

public sealed class SendToPromptResult
{
    public SendToAction Action { get; init; } = SendToAction.Cancel;

    public bool IsFallback { get; init; }

    public string? Reason { get; init; }
}
