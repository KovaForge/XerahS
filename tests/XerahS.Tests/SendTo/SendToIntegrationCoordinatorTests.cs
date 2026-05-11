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
using SkiaSharp;
using XerahS.App;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Tests.SendTo;

[TestFixture]
public class SendToIntegrationCoordinatorTests
{
    [Test]
    public async Task HandleAsync_WhenCancelled_DoesNothing()
    {
        FakeUiService uiService = new()
        {
            PromptResult = new SendToPromptResult { Action = SendToAction.Cancel }
        };
        FakeTaskManager taskManager = new();
        SendToIntegrationCoordinator coordinator = CreateCoordinator(uiService, taskManager);
        SendToSelection selection = new()
        {
            FilePaths = ["C:\\captures\\image.png"],
            Kind = SendToSelectionKind.AllFiles,
            AllFilesAreImages = true
        };

        await coordinator.HandleAsync(selection, "test");

        Assert.Multiple(() =>
        {
            Assert.That(taskManager.StartFileTaskCalls, Is.EqualTo(0));
            Assert.That(uiService.ExecutedActions, Is.Empty);
        });
    }

    [Test]
    public async Task HandleAsync_WhenNonUploadActionSelected_DelegatesToUi()
    {
        FakeUiService uiService = new()
        {
            PromptResult = new SendToPromptResult { Action = SendToAction.OpenUploadContent }
        };
        FakeTaskManager taskManager = new();
        SendToIntegrationCoordinator coordinator = CreateCoordinator(uiService, taskManager);
        SendToSelection selection = new()
        {
            FilePaths = ["C:\\captures\\image.png"],
            Kind = SendToSelectionKind.AllFiles,
            AllFilesAreImages = true
        };

        await coordinator.HandleAsync(selection, "test");

        Assert.Multiple(() =>
        {
            Assert.That(taskManager.StartFileTaskCalls, Is.EqualTo(0));
            Assert.That(uiService.ExecutedActions, Is.EqualTo(new[] { SendToAction.OpenUploadContent }));
        });
    }

    [Test]
    public async Task HandleAsync_WhenUploadSelected_UploadsFilesAndTopLevelFolderFiles()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"xerahs-sendto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            string directFile = Path.Combine(rootPath, "direct.txt");
            string folderPath = Path.Combine(rootPath, "folder");
            string nestedFolderPath = Path.Combine(folderPath, "nested");
            string folderFile = Path.Combine(folderPath, "from-folder.txt");
            string nestedFile = Path.Combine(nestedFolderPath, "nested.txt");

            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(nestedFolderPath);
            await File.WriteAllTextAsync(directFile, "direct");
            await File.WriteAllTextAsync(folderFile, "folder");
            await File.WriteAllTextAsync(nestedFile, "nested");

            FakeUiService uiService = new()
            {
                PromptResult = new SendToPromptResult { Action = SendToAction.UploadNow }
            };
            FakeTaskManager taskManager = new();
            SendToIntegrationCoordinator coordinator = CreateCoordinator(uiService, taskManager);
            SendToSelection selection = new()
            {
                FilePaths = [directFile],
                FolderPaths = [folderPath],
                Kind = SendToSelectionKind.Mixed
            };

            await coordinator.HandleAsync(selection, "test");

            Assert.Multiple(() =>
            {
                Assert.That(taskManager.StartFileTaskCalls, Is.EqualTo(2));
                Assert.That(taskManager.StartedFilePaths, Does.Contain(directFile));
                Assert.That(taskManager.StartedFilePaths, Does.Contain(folderFile));
                Assert.That(taskManager.StartedFilePaths, Does.Not.Contain(nestedFile));
                Assert.That(taskManager.StartedJobs, Is.All.EqualTo(WorkflowType.FileUpload));
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
    public async Task UploadSelectionAsync_WhenFolderPolicyDoesNotExpand_UploadsDirectFilesOnly()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"xerahs-sendto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            string directFile = Path.Combine(rootPath, "direct.txt");
            string folderPath = Path.Combine(rootPath, "folder");
            string folderFile = Path.Combine(folderPath, "from-folder.txt");

            Directory.CreateDirectory(folderPath);
            await File.WriteAllTextAsync(directFile, "direct");
            await File.WriteAllTextAsync(folderFile, "folder");

            FakeUiService uiService = new();
            FakeTaskManager taskManager = new();
            SendToIntegrationCoordinator coordinator = CreateCoordinator(uiService, taskManager);
            SendToSelection selection = new()
            {
                FilePaths = [directFile],
                FolderPaths = [folderPath],
                Kind = SendToSelectionKind.Mixed
            };

            await coordinator.UploadSelectionAsync(
                selection,
                "test",
                new SendToPromptResult
                {
                    Action = SendToAction.UploadNow,
                    FolderPolicy = SendToFolderPolicy.DoNotExpandFolders
                });

            Assert.Multiple(() =>
            {
                Assert.That(taskManager.StartFileTaskCalls, Is.EqualTo(1));
                Assert.That(taskManager.StartedFilePaths, Is.EqualTo(new[] { directFile }));
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
    public async Task UploadSelectionAsync_WhenFolderPolicyIsRecursive_IncludesNestedFiles()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"xerahs-sendto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            string folderPath = Path.Combine(rootPath, "folder");
            string nestedFolderPath = Path.Combine(folderPath, "nested");
            string folderFile = Path.Combine(folderPath, "from-folder.txt");
            string nestedFile = Path.Combine(nestedFolderPath, "nested.txt");

            Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(nestedFolderPath);
            await File.WriteAllTextAsync(folderFile, "folder");
            await File.WriteAllTextAsync(nestedFile, "nested");

            FakeUiService uiService = new();
            FakeTaskManager taskManager = new();
            SendToIntegrationCoordinator coordinator = CreateCoordinator(uiService, taskManager);
            SendToSelection selection = new()
            {
                FolderPaths = [folderPath],
                Kind = SendToSelectionKind.AllFolders
            };

            await coordinator.UploadSelectionAsync(
                selection,
                "test",
                new SendToPromptResult
                {
                    Action = SendToAction.UploadNow,
                    FolderPolicy = SendToFolderPolicy.IncludeFilesRecursively
                });

            Assert.Multiple(() =>
            {
                Assert.That(taskManager.StartFileTaskCalls, Is.EqualTo(2));
                Assert.That(taskManager.StartedFilePaths, Does.Contain(folderFile));
                Assert.That(taskManager.StartedFilePaths, Does.Contain(nestedFile));
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

    private static SendToIntegrationCoordinator CreateCoordinator(FakeUiService uiService, FakeTaskManager taskManager)
    {
        return new SendToIntegrationCoordinator(
            uiService,
            taskManager,
            () => new TaskSettings());
    }

    private sealed class FakeTaskManager : ITaskManager
    {
        public int StartFileTaskCalls { get; private set; }

        public List<string> StartedFilePaths { get; } = [];

        public List<WorkflowType> StartedJobs { get; } = [];

        public Task StartTask(object? taskSettings, SKBitmap? inputImage = null) => Task.CompletedTask;

        public Task StartFileTask(object? taskSettings, string filePath)
        {
            StartFileTaskCalls++;
            StartedFilePaths.Add(filePath);

            if (taskSettings is TaskSettings settings)
            {
                StartedJobs.Add(settings.Job);
            }

            return Task.CompletedTask;
        }

        public Task StartImageUploadTask(object? taskSettings, SKBitmap image) => Task.CompletedTask;

        public Task StartTextTask(object? taskSettings, string text) => Task.CompletedTask;

        public void StopAllTasks()
        {
        }
    }

    private sealed class FakeUiService : IUIService
    {
        public SendToPromptResult PromptResult { get; set; } = new();

        public List<SendToAction> ExecutedActions { get; } = [];

        public Task HideMainWindowAsync() => Task.CompletedTask;

        public Task RestoreMainWindowAsync() => Task.CompletedTask;

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false) => Task.FromResult<SKBitmap?>(image);

        public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath) => Task.FromResult<string?>(null);

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload)
        {
            return Task.FromResult((afterCapture, afterUpload, false));
        }

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection) => Task.FromResult(PromptResult);

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection, SendToPromptResult? decision = null)
        {
            ExecutedActions.Add(action);
            return Task.CompletedTask;
        }

        public Task ShowOcrWindowAsync(SKBitmap image) => Task.CompletedTask;

        public Task ShowAnalyzerWindowAsync(SKBitmap image) => Task.CompletedTask;
    }
}
