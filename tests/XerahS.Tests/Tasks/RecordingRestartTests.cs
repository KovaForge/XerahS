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
using XerahS.Core.Tasks;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.Services.Abstractions;

namespace XerahS.Tests.Tasks;

[TestFixture]
public sealed class RecordingRestartTests
{
    private sealed class FakeRecordingManager : IScreenRecordingManager
    {
        private readonly Queue<bool> _restartAnswers;

        public FakeRecordingManager(params bool[] restartAnswers) => _restartAnswers = new Queue<bool>(restartAnswers);

        public List<string?> StartedOutputPaths { get; } = new();
        public int AbortCalls { get; private set; }
        public bool IsRecording { get; private set; }
        public bool IsPaused => false;
        public bool IsUsingFallback => false;
        public string? PlannedOutputPath => "/tmp/planned-" + StartedOutputPaths.Count + ".mp4";

        public void SignalStop() { }
        public Task WaitForStopSignalAsync() => Task.CompletedTask;

        public Task StartRecordingAsync(object options)
        {
            StartedOutputPaths.Add(((RecordingOptions)options).OutputPath);
            IsRecording = true;
            return Task.CompletedTask;
        }

        public Task<string?> StopRecordingAsync() => Task.FromResult<string?>(null);

        public Task AbortRecordingAsync()
        {
            AbortCalls++;
            IsRecording = false;
            return Task.CompletedTask;
        }

        public Task TogglePauseResumeAsync() => Task.CompletedTask;
        public bool ConsumeRestartRequest() => _restartAnswers.Count > 0 && _restartAnswers.Dequeue();
    }

    [Test]
    public async Task Restart_DiscardsTakeAndStartsAgainWithTheSameRequest()
    {
        var manager = new FakeRecordingManager(true, false);
        var options = new RecordingOptions { OutputPath = "/tmp/requested.mp4" };
        var started = new List<string?>();

        await WorkerTask.RecordUntilStoppedAsync(new ScreenRecordingWorkflowCoordinator(manager), options, started.Add);

        Assert.That(manager.AbortCalls, Is.EqualTo(1));
        Assert.That(manager.StartedOutputPaths, Is.EqualTo(new[] { "/tmp/requested.mp4", "/tmp/requested.mp4" }),
            "each take starts from the originally requested path, not the previous planned one");
        Assert.That(started, Has.Count.EqualTo(2));
        Assert.That(options.OutputPath, Is.EqualTo("/tmp/planned-2.mp4"), "the final take's planned path is kept");
    }

    [Test]
    public async Task Stop_WithoutRestart_RecordsOnce()
    {
        var manager = new FakeRecordingManager(false);

        await WorkerTask.RecordUntilStoppedAsync(new ScreenRecordingWorkflowCoordinator(manager), new RecordingOptions(), _ => { });

        Assert.That(manager.StartedOutputPaths, Has.Count.EqualTo(1));
        Assert.That(manager.AbortCalls, Is.Zero);
    }
}
