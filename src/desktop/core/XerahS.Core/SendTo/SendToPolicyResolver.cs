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

using XerahS.Core;

namespace XerahS.Core.SendTo;

public static class SendToPolicyResolver
{
    public static SendToRememberScope GetRememberScope(SendToSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return selection.Kind switch
        {
            SendToSelectionKind.AllFiles when selection.AllFilesAreImages => SendToRememberScope.ImageOnlyFiles,
            SendToSelectionKind.AllFiles => SendToRememberScope.AllFiles,
            SendToSelectionKind.AllFolders => SendToRememberScope.AllFolders,
            _ => SendToRememberScope.MixedFilesAndFolders
        };
    }

    public static SendToPromptResult CreateDefaultDecision(
        SendToSelection selection,
        SendToFolderPolicy folderPolicy,
        SendToBatchExecutionPolicy batchExecutionPolicy,
        int batchConfirmThreshold)
    {
        return new SendToPromptResult
        {
            Action = SendToAction.Cancel,
            FolderPolicy = folderPolicy,
            RememberScope = GetRememberScope(selection),
            BatchExecutionPolicy = batchExecutionPolicy,
            BatchConfirmThreshold = NormalizeBatchThreshold(batchConfirmThreshold)
        };
    }

    public static SendToPromptResult? TryResolveRememberedDecision(
        SendToSelection selection,
        IEnumerable<SendToRememberedChoice>? rememberedChoices)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (rememberedChoices == null)
        {
            return null;
        }

        SendToRememberScope scope = GetRememberScope(selection);
        SendToRememberedChoice? match = rememberedChoices
            .Where(choice => choice.Action != SendToAction.Cancel)
            .OrderByDescending(choice => choice.SavedUtc)
            .FirstOrDefault(choice => choice.Scope == scope);

        if (match == null)
        {
            return null;
        }

        return new SendToPromptResult
        {
            Action = match.Action,
            FolderPolicy = match.FolderPolicy,
            RememberScope = scope,
            BatchExecutionPolicy = match.BatchExecutionPolicy,
            BatchConfirmThreshold = NormalizeBatchThreshold(match.BatchConfirmThreshold),
            IsRemembered = true,
            Reason = $"Remembered Send-to choice for {FormatRememberScope(scope)}."
        };
    }

    public static void SaveRememberedDecision(
        IList<SendToRememberedChoice> rememberedChoices,
        SendToPromptResult decision)
    {
        ArgumentNullException.ThrowIfNull(rememberedChoices);
        ArgumentNullException.ThrowIfNull(decision);

        if (!decision.RememberChoice || decision.Action == SendToAction.Cancel)
        {
            return;
        }

        for (int i = rememberedChoices.Count - 1; i >= 0; i--)
        {
            if (rememberedChoices[i].Scope == decision.RememberScope)
            {
                rememberedChoices.RemoveAt(i);
            }
        }

        rememberedChoices.Add(new SendToRememberedChoice
        {
            Scope = decision.RememberScope,
            Action = decision.Action,
            FolderPolicy = decision.FolderPolicy,
            BatchExecutionPolicy = decision.BatchExecutionPolicy,
            BatchConfirmThreshold = NormalizeBatchThreshold(decision.BatchConfirmThreshold),
            SavedUtc = DateTime.UtcNow
        });
    }

    public static SendToResolvedFiles ResolveFiles(
        SendToSelection selection,
        SendToFolderPolicy folderPolicy)
    {
        ArgumentNullException.ThrowIfNull(selection);

        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        HashSet<string> seen = new(comparer);
        List<string> files = [];
        int directFileCount = 0;
        int folderFileCount = 0;
        int failedFolderCount = 0;

        foreach (string filePath in selection.FilePaths)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) && seen.Add(filePath))
            {
                files.Add(filePath);
                directFileCount++;
            }
        }

        if (folderPolicy != SendToFolderPolicy.DoNotExpandFolders)
        {
            SearchOption searchOption = folderPolicy == SendToFolderPolicy.IncludeFilesRecursively
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foreach (string folderPath in selection.FolderPaths)
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    continue;
                }

                try
                {
                    foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", searchOption))
                    {
                        if (seen.Add(filePath))
                        {
                            files.Add(filePath);
                            folderFileCount++;
                        }
                    }
                }
                catch
                {
                    failedFolderCount++;
                }
            }
        }

        return new SendToResolvedFiles
        {
            FilePaths = files,
            FolderPolicy = folderPolicy,
            DirectFileCount = directFileCount,
            FolderFileCount = folderFileCount,
            FolderCount = selection.FolderPaths.Count,
            FailedFolderCount = failedFolderCount
        };
    }

    public static string FormatRememberScope(SendToRememberScope scope) => scope switch
    {
        SendToRememberScope.AllFiles => "all files",
        SendToRememberScope.AllFolders => "all folders",
        SendToRememberScope.MixedFilesAndFolders => "mixed files and folders",
        SendToRememberScope.ImageOnlyFiles => "image-only files",
        _ => "this selection"
    };

    public static string FormatFolderPolicy(SendToFolderPolicy policy) => policy switch
    {
        SendToFolderPolicy.DoNotExpandFolders => "Do not expand folders",
        SendToFolderPolicy.IncludeTopLevelFiles => "Include top-level files",
        SendToFolderPolicy.IncludeFilesRecursively => "Include files recursively",
        _ => policy.ToString()
    };

    public static string FormatBatchPolicy(SendToBatchExecutionPolicy policy, int threshold) => policy switch
    {
        SendToBatchExecutionPolicy.OpenAllImmediately => "Open all immediately",
        SendToBatchExecutionPolicy.OpenSequentially => "Open sequentially",
        SendToBatchExecutionPolicy.ConfirmBeforeOpeningMoreThanThreshold =>
            $"Confirm before opening more than {NormalizeBatchThreshold(threshold)} items",
        _ => policy.ToString()
    };

    public static int NormalizeBatchThreshold(int threshold) => Math.Clamp(threshold, 1, 100);
}
