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

using XerahS.Common;

namespace XerahS.Core.SendTo;

public static class SendToSelectionClassifier
{
    public static SendToSelection Create(IEnumerable<string> filePaths, IEnumerable<string> folderPaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(folderPaths);

        string[] files = DistinctPaths(filePaths);
        string[] folders = DistinctPaths(folderPaths);

        SendToSelectionKind kind = folders.Length == 0
            ? SendToSelectionKind.AllFiles
            : files.Length == 0
                ? SendToSelectionKind.AllFolders
                : SendToSelectionKind.Mixed;

        bool allFilesAreImages = files.Length > 0 && files.All(FileHelpers.IsImageFile);

        return new SendToSelection
        {
            FilePaths = files,
            FolderPaths = folders,
            Kind = kind,
            AllFilesAreImages = allFilesAreImages
        };
    }

    private static string[] DistinctPaths(IEnumerable<string> paths)
    {
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        List<string> orderedPaths = [];
        HashSet<string> seen = new(comparer);

        foreach (string rawPath in paths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            string normalizedPath = rawPath.Trim();
            if (seen.Add(normalizedPath))
            {
                orderedPaths.Add(normalizedPath);
            }
        }

        return orderedPaths.ToArray();
    }
}
