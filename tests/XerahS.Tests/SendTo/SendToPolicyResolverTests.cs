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

using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.SendTo;

namespace XerahS.Tests.SendTo;

[TestFixture]
public class SendToPolicyResolverTests
{
    [Test]
    public void TryResolveRememberedDecision_AppliesOnlyMatchingScope()
    {
        SendToSelection selection = new()
        {
            FilePaths = ["capture.png"],
            Kind = SendToSelectionKind.AllFiles,
            AllFilesAreImages = true
        };

        SendToRememberedChoice[] choices =
        [
            new()
            {
                Scope = SendToRememberScope.AllFiles,
                Action = SendToAction.UploadNow
            },
            new()
            {
                Scope = SendToRememberScope.ImageOnlyFiles,
                Action = SendToAction.OpenImageEditor,
                FolderPolicy = SendToFolderPolicy.DoNotExpandFolders,
                BatchExecutionPolicy = SendToBatchExecutionPolicy.OpenSequentially,
                BatchConfirmThreshold = 9
            }
        ];

        SendToPromptResult? result = SendToPolicyResolver.TryResolveRememberedDecision(selection, choices);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Action, Is.EqualTo(SendToAction.OpenImageEditor));
            Assert.That(result.IsRemembered, Is.True);
            Assert.That(result.RememberScope, Is.EqualTo(SendToRememberScope.ImageOnlyFiles));
            Assert.That(result.FolderPolicy, Is.EqualTo(SendToFolderPolicy.DoNotExpandFolders));
            Assert.That(result.BatchExecutionPolicy, Is.EqualTo(SendToBatchExecutionPolicy.OpenSequentially));
            Assert.That(result.BatchConfirmThreshold, Is.EqualTo(9));
        });
    }

    [Test]
    public void SaveRememberedDecision_ReplacesExistingScope()
    {
        List<SendToRememberedChoice> choices =
        [
            new()
            {
                Scope = SendToRememberScope.AllFolders,
                Action = SendToAction.IndexFolders
            }
        ];

        SendToPromptResult decision = new()
        {
            Action = SendToAction.OpenUploadContent,
            RememberChoice = true,
            RememberScope = SendToRememberScope.AllFolders,
            FolderPolicy = SendToFolderPolicy.IncludeFilesRecursively
        };

        SendToPolicyResolver.SaveRememberedDecision(choices, decision);

        Assert.Multiple(() =>
        {
            Assert.That(choices, Has.Count.EqualTo(1));
            Assert.That(choices[0].Action, Is.EqualTo(SendToAction.OpenUploadContent));
            Assert.That(choices[0].FolderPolicy, Is.EqualTo(SendToFolderPolicy.IncludeFilesRecursively));
        });
    }

    [Test]
    public async Task ResolveFiles_RespectsFolderPolicy()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"xerahs-sendto-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            string directFile = Path.Combine(rootPath, "direct.txt");
            string folderPath = Path.Combine(rootPath, "folder");
            string nestedFolderPath = Path.Combine(folderPath, "nested");
            string folderFile = Path.Combine(folderPath, "folder.txt");
            string nestedFile = Path.Combine(nestedFolderPath, "nested.txt");

            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(nestedFolderPath);
            await File.WriteAllTextAsync(directFile, "direct");
            await File.WriteAllTextAsync(folderFile, "folder");
            await File.WriteAllTextAsync(nestedFile, "nested");

            SendToSelection selection = new()
            {
                FilePaths = [directFile],
                FolderPaths = [folderPath],
                Kind = SendToSelectionKind.Mixed
            };

            SendToResolvedFiles none = SendToPolicyResolver.ResolveFiles(selection, SendToFolderPolicy.DoNotExpandFolders);
            SendToResolvedFiles topLevel = SendToPolicyResolver.ResolveFiles(selection, SendToFolderPolicy.IncludeTopLevelFiles);
            SendToResolvedFiles recursive = SendToPolicyResolver.ResolveFiles(selection, SendToFolderPolicy.IncludeFilesRecursively);

            Assert.Multiple(() =>
            {
                Assert.That(none.FilePaths, Is.EqualTo(new[] { directFile }));
                Assert.That(topLevel.FilePaths, Does.Contain(folderFile));
                Assert.That(topLevel.FilePaths, Does.Not.Contain(nestedFile));
                Assert.That(recursive.FilePaths, Does.Contain(folderFile));
                Assert.That(recursive.FilePaths, Does.Contain(nestedFile));
            });
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Test]
    public void RequiresBatchConfirmation_OnlyForRememberedThresholdPolicy()
    {
        SendToPromptResult rememberedOverThreshold = new()
        {
            IsRemembered = true,
            BatchExecutionPolicy = SendToBatchExecutionPolicy.ConfirmBeforeOpeningMoreThanThreshold,
            BatchConfirmThreshold = 5
        };
        SendToPromptResult promptOverThreshold = new()
        {
            IsRemembered = false,
            BatchExecutionPolicy = SendToBatchExecutionPolicy.ConfirmBeforeOpeningMoreThanThreshold,
            BatchConfirmThreshold = 5
        };
        SendToPromptResult rememberedImmediate = new()
        {
            IsRemembered = true,
            BatchExecutionPolicy = SendToBatchExecutionPolicy.OpenAllImmediately,
            BatchConfirmThreshold = 5
        };

        Assert.Multiple(() =>
        {
            Assert.That(SendToPolicyResolver.RequiresBatchConfirmation(rememberedOverThreshold, 6), Is.True);
            Assert.That(SendToPolicyResolver.RequiresBatchConfirmation(rememberedOverThreshold, 5), Is.False);
            Assert.That(SendToPolicyResolver.RequiresBatchConfirmation(promptOverThreshold, 6), Is.False);
            Assert.That(SendToPolicyResolver.RequiresBatchConfirmation(rememberedImmediate, 6), Is.False);
        });
    }

    [Test]
    public void FolderAndBatchPolicyIndexes_RoundTrip()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SendToPolicyResolver.FromFolderPolicyIndex(SendToPolicyResolver.ToFolderPolicyIndex(SendToFolderPolicy.DoNotExpandFolders)),
                Is.EqualTo(SendToFolderPolicy.DoNotExpandFolders));
            Assert.That(
                SendToPolicyResolver.FromFolderPolicyIndex(SendToPolicyResolver.ToFolderPolicyIndex(SendToFolderPolicy.IncludeFilesRecursively)),
                Is.EqualTo(SendToFolderPolicy.IncludeFilesRecursively));
            Assert.That(
                SendToPolicyResolver.FromBatchPolicyIndex(SendToPolicyResolver.ToBatchPolicyIndex(SendToBatchExecutionPolicy.OpenSequentially)),
                Is.EqualTo(SendToBatchExecutionPolicy.OpenSequentially));
        });
    }
}
