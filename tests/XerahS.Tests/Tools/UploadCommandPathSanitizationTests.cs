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
using System.Reflection;
using XerahS.Bootstrap;
using XerahS.CLI.Commands;
using XerahS.CLI.Services;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Tools;

[TestFixture]
public class UploadCommandPathSanitizationTests
{
    [SetUp]
    public void SetUp()
    {
        typeof(UploadCommand).GetField("_jsonOutput", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
        typeof(UploadCommand).GetField("_quiet", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);
        typeof(UploadCommand).GetField("_checkUploadReadiness", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<string, bool, UploadReadiness>)((_, uploadAsText) =>
                UploadReadiness.Ready(new BootstrapReport(), uploadAsText ? UploaderCategory.Text : UploaderCategory.File)));
        typeof(UploadCommand).GetField("_processUploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<TaskInfo, CancellationToken, Task>)((taskInfo, _) =>
            {
                taskInfo.Result = new XerahS.Uploaders.UploadResult("ok", "https://example.invalid/uploaded.txt") { IsSuccess = true };
                return Task.CompletedTask;
            }));
        UploadCommand.ResetTempDirectoryCount();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(".")]
    [TestCase("..")]
    [TestCase("folder/..")]
    [TestCase("folder/.")]
    public void SanitizeUploadFileName_WhenNameDoesNotResolveToARealFileName_UsesFallback(string? requestedName)
    {
        string result = UploadCommand.SanitizeUploadFileName(requestedName, "upload.txt");

        Assert.That(result, Is.EqualTo("upload.txt"));
    }

    [Test]
    public void SanitizeUploadFileName_WhenPathContainsDirectories_ReturnsLeafFileName()
    {
        string result = UploadCommand.SanitizeUploadFileName("nested/path/report.png", "upload.txt");

        Assert.That(result, Is.EqualTo("report.png"));
    }

    [Test]
    public void CreateTemporaryUploadFilePath_WhenNameResolvesToParentSegment_KeepsFileInsideUniqueTempDirectory()
    {
        string tempPath = UploadCommand.CreateTemporaryUploadFilePath("..", "upload.txt");

        try
        {
            string? directory = Path.GetDirectoryName(tempPath);

            Assert.Multiple(() =>
            {
                Assert.That(Path.GetFileName(tempPath), Is.EqualTo("upload.txt"));
                Assert.That(directory, Is.Not.Null.And.Contains(Path.Combine(Path.GetTempPath(), "xerahs-upload")));
            });
        }
        finally
        {
            UploadCommand.CleanupTemporaryUploadDirectories([Path.GetDirectoryName(tempPath)]);
        }
    }

    [Test]
    public void GetReadinessCategories_WhenTextFileIsUploadedAsFile_UsesFileUploader()
    {
        UploaderCategory[] categories = CliUploaderBootstrapper.GetReadinessCategories(uploadAsText: false);

        Assert.That(categories, Is.EqualTo(new[] { UploaderCategory.File }));
    }

    [Test]
    public void CleanupTemporaryUploadDirectories_WhenGivenMultipleDirectories_RemovesEachDirectoryOnce()
    {
        string firstDirectory = UploadCommand.CreateTemporaryUploadDirectory();
        string secondDirectory = UploadCommand.CreateTemporaryUploadDirectory();
        File.WriteAllText(Path.Combine(firstDirectory, "upload.txt"), "first");
        File.WriteAllText(Path.Combine(secondDirectory, "upload.txt"), "second");

        UploadCommand.CleanupTemporaryUploadDirectories([firstDirectory, secondDirectory, firstDirectory, null, string.Empty]);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(firstDirectory), Is.False);
            Assert.That(Directory.Exists(secondDirectory), Is.False);
        });
    }

    [Test]
    public async Task UploadAsync_TextContentWithName_DoesNotCreateRedundantNamedCopyDirectory()
    {
        // --text --name <x> should write the payload once into a single unique temp
        // directory named <x>; the redundant named-copy step that pre-existed created
        // a second temp directory with the same name and an extra File.Copy on disk.
        // Verified by counting CreateTemporaryUploadDirectory invocations during a
        // single UploadAsync call (the counter is reset in SetUp).
        TaskInfo? processedTaskInfo = null;
        typeof(UploadCommand).GetField("_processUploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<TaskInfo, CancellationToken, Task>)((taskInfo, _) =>
            {
                processedTaskInfo = taskInfo;
                taskInfo.Result = new XerahS.Uploaders.UploadResult("ok", "https://example.invalid/uploaded.txt") { IsSuccess = true };
                return Task.CompletedTask;
            }));

        int exitCode = await InvokeUploadAsync(new SequencedDesktopTaskManager((_, _) => { }), filePath: null, text: "payload", pipe: false, name: "greeting.txt", asFile: false);

        int created = UploadCommand.GetTempDirectoryCount();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(processedTaskInfo, Is.Not.Null);
            Assert.That(processedTaskInfo!.Job, Is.EqualTo(TaskJob.TextUpload));
            Assert.That(processedTaskInfo.FileName, Is.EqualTo("greeting.txt"));
            Assert.That(processedTaskInfo.TextContent, Is.EqualTo("payload"));
            // --text --name <x> --no-randomize should produce exactly one temp directory
            // (the write at line 165). The named-copy at line 192-200 must be skipped.
            Assert.That(created, Is.EqualTo(1), "Expected exactly one temp directory for --text --name with no randomize; the named-copy must be skipped.");
        });
    }

    [Test]
    public async Task UploadAsync_TextContentWithNameAndRandomize_CreatesOnlyTwoTempDirectories()
    {
        // --text --name <x> with the default randomize=true should create two temp
        // directories (one for the write, one for the suffixed copy); the named-copy
        // step is still redundant and must be skipped.
        TaskInfo? processedTaskInfo = null;
        typeof(UploadCommand).GetField("_processUploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<TaskInfo, CancellationToken, Task>)((taskInfo, _) =>
            {
                processedTaskInfo = taskInfo;
                taskInfo.Result = new XerahS.Uploaders.UploadResult("ok", "https://example.invalid/uploaded.txt") { IsSuccess = true };
                return Task.CompletedTask;
            }));

        // Direct invocation with randomize=true (the InvokeUploadAsync test helper
        // passes randomize=false).
        MethodInfo method = typeof(UploadCommand).GetMethod("UploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task<int>)method.Invoke(null, [new SequencedDesktopTaskManager((_, _) => { }), null, "payload", false, "greeting.txt", false, true])!;
        int exitCode = await task;

        int created = UploadCommand.GetTempDirectoryCount();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(processedTaskInfo, Is.Not.Null);
            Assert.That(processedTaskInfo!.Job, Is.EqualTo(TaskJob.TextUpload));
            Assert.That(processedTaskInfo.TextContent, Is.EqualTo("payload"));
            // --text --name <x> --randomize should produce exactly two temp directories
            // (the write at line 165, plus the suffixed copy at line 216-220).
            Assert.That(created, Is.EqualTo(2), "Expected exactly two temp directories for --text --name with randomize; the named-copy must be skipped but the randomize copy must still run.");
        });
    }

    [Test]
    public async Task UploadAsync_FilePathWithName_StillPerformsNamedCopy()
    {
        // For file-path input the source file is NOT a temp file already named with
        // the requested name, so the named-copy step must still run. Regression
        // coverage for the !sourceIsTemporaryFromTextOrPipe guard.
        string sourceFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        string requestedName = "user-supplied-name.bin";

        try
        {
            await File.WriteAllTextAsync(sourceFile, "payload");

            int exitCode = await InvokeUploadAsync(new SequencedDesktopTaskManager((_, _) => { }), sourceFile, text: null, pipe: false, name: requestedName, asFile: false);

            int created = UploadCommand.GetTempDirectoryCount();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                // For a file path with --name, the named-copy at line 192-200 still
                // runs (and is necessary), so the test expects exactly one temp
                // directory from the named-copy. No randomize, so no suffixed copy.
                Assert.That(created, Is.EqualTo(1), "Expected exactly one temp directory for --file --name with no randomize; the named-copy must still run for file paths.");
            });
        }
        finally
        {
            File.Delete(sourceFile);
        }
    }

    [Test]
    public async Task UploadAsync_TextContentWithExtensionlessName_RequiresTextUploader()
    {
        bool? checkedAsText = null;
        TaskInfo? processedTaskInfo = null;
        typeof(UploadCommand).GetField("_checkUploadReadiness", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<string, bool, UploadReadiness>)((_, uploadAsText) =>
            {
                checkedAsText = uploadAsText;
                return UploadReadiness.Ready(new BootstrapReport(), uploadAsText ? UploaderCategory.Text : UploaderCategory.File);
            }));
        typeof(UploadCommand).GetField("_processUploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<TaskInfo, CancellationToken, Task>)((taskInfo, _) =>
            {
                processedTaskInfo = taskInfo;
                taskInfo.Result = new XerahS.Uploaders.UploadResult("ok", "https://example.invalid/uploaded.txt") { IsSuccess = true };
                return Task.CompletedTask;
            }));

        int exitCode = await InvokeUploadAsync(new SequencedDesktopTaskManager((_, _) => { }), filePath: null, text: "payload", pipe: false, name: "note");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(checkedAsText, Is.True);
            Assert.That(processedTaskInfo, Is.Not.Null);
            Assert.That(processedTaskInfo!.Job, Is.EqualTo(TaskJob.TextUpload));
            Assert.That(processedTaskInfo.FileName, Is.EqualTo("note"));
        });
    }

    [Test]
    public async Task UploadAsync_AsFileWithTextExtension_RequiresFileUploader()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        bool? checkedAsText = null;
        TaskInfo? processedTaskInfo = null;
        typeof(UploadCommand).GetField("_checkUploadReadiness", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<string, bool, UploadReadiness>)((_, uploadAsText) =>
            {
                checkedAsText = uploadAsText;
                return UploadReadiness.Ready(new BootstrapReport(), uploadAsText ? UploaderCategory.Text : UploaderCategory.File);
            }));
        typeof(UploadCommand).GetField("_processUploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<TaskInfo, CancellationToken, Task>)((taskInfo, _) =>
            {
                processedTaskInfo = taskInfo;
                taskInfo.Result = new XerahS.Uploaders.UploadResult("ok", "https://example.invalid/uploaded.txt") { IsSuccess = true };
                return Task.CompletedTask;
            }));

        try
        {
            await File.WriteAllTextAsync(tempFile, "payload");

            int exitCode = await InvokeUploadAsync(new SequencedDesktopTaskManager((_, _) => { }), tempFile, text: null, pipe: false, name: null, asFile: true);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(checkedAsText, Is.False);
                Assert.That(processedTaskInfo, Is.Not.Null);
                Assert.That(processedTaskInfo!.Job, Is.EqualTo(TaskJob.FileUpload));
                Assert.That(processedTaskInfo.FilePath, Is.EqualTo(tempFile));
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task UploadAsync_UsesDirectUploadProcessorAndDoesNotWaitOnUnrelatedTaskEvents()
    {
        string tempFile = Path.GetTempFileName();
        TaskInfo? processedTaskInfo = null;
        typeof(UploadCommand).GetField("_processUploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, (Func<TaskInfo, CancellationToken, Task>)((taskInfo, _) =>
            {
                processedTaskInfo = taskInfo;
                taskInfo.Result = new XerahS.Uploaders.UploadResult("ok", "https://example.invalid/uploaded.txt") { IsSuccess = true };
                return Task.CompletedTask;
            }));

        try
        {
            await File.WriteAllTextAsync(tempFile, "payload");

            var taskManager = new SequencedDesktopTaskManager((_, raiseCompleted) =>
            {
                raiseCompleted(CreateCompletedTask(Path.Combine(Path.GetTempPath(), "other-file.txt"), url: null, status: XerahS.Core.TaskStatus.Failed, errorMessage: "Unrelated failure"));
            });

            int exitCode = await InvokeUploadAsync(taskManager, tempFile, text: null, pipe: false, name: null);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(taskManager.StartedFilePaths, Is.Empty);
                Assert.That(processedTaskInfo, Is.Not.Null);
                Assert.That(processedTaskInfo!.FilePath, Is.EqualTo(tempFile));
                Assert.That(processedTaskInfo.Job, Is.EqualTo(TaskJob.FileUpload));
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static async Task<int> InvokeUploadAsync(IDesktopTaskManager taskManager, string? filePath, string? text, bool pipe, string? name, bool asFile = false)
    {
        MethodInfo method = typeof(UploadCommand).GetMethod("UploadAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task<int>)method.Invoke(null, [taskManager, filePath, text, pipe, name, asFile, false])!;
        return await task;
    }

    private static WorkerTask CreateCompletedTask(string filePath, string? url, XerahS.Core.TaskStatus status = XerahS.Core.TaskStatus.Completed, string? errorMessage = null)
    {
        var task = WorkerTask.Create(new TaskSettings());
        task.Info.FilePath = filePath;
        task.Info.Metadata.UploadURL = url ?? string.Empty;

        typeof(WorkerTask).GetProperty(nameof(WorkerTask.Status), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(task, status);
        typeof(WorkerTask).GetProperty(nameof(WorkerTask.Error), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(task,
            errorMessage is null ? null : new InvalidOperationException(errorMessage));

        return task;
    }

    private sealed class SequencedDesktopTaskManager(Action<string, Action<WorkerTask>> onStartFileTask) : IDesktopTaskManager
    {
        private EventHandler<WorkerTask>? _taskCompleted;

        public event EventHandler<WorkerTask>? TaskCompleted
        {
            add => _taskCompleted += value;
            remove => _taskCompleted -= value;
        }

        public event EventHandler<WorkerTask>? TaskStarted
        {
            add { }
            remove { }
        }

        public IEnumerable<WorkerTask> Tasks => Array.Empty<WorkerTask>();

        public List<string> StartedFilePaths { get; } = [];

        public Task StartTask(TaskSettings? taskSettings, SKBitmap? inputImage = null) => Task.CompletedTask;

        public Task StartFileTask(TaskSettings? taskSettings, string filePath)
        {
            StartedFilePaths.Add(filePath);
            Task.Run(() => onStartFileTask(filePath, task => _taskCompleted?.Invoke(this, task)));
            return Task.CompletedTask;
        }

        public Task StartImageUploadTask(TaskSettings? taskSettings, SKBitmap image) => Task.CompletedTask;

        public Task StartTextTask(TaskSettings? taskSettings, string text) => Task.CompletedTask;

        public void StopAllTasks()
        {
        }
    }
}
