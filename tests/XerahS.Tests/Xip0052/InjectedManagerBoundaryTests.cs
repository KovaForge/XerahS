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
using XerahS.RegionCapture.ScreenRecording;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Xip0052;

[TestFixture]
[NonParallelizable]
public class InjectedManagerBoundaryTests
{
    [Test]
    public async Task RecordingViewModel_PauseResumeCommand_UsesInjectedCoordinator()
    {
        var coordinator = new FakeScreenRecordingCoordinator();
        using var viewModel = new RecordingViewModel(coordinator)
        {
            CanPauseResume = true
        };

        await viewModel.PauseResumeCommand.ExecuteAsync(null);

        Assert.That(coordinator.TogglePauseResumeCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task RecordingViewModel_PauseResumeCommand_DoesNotToggleWhenCoordinatorDisablesPause()
    {
        var coordinator = new FakeScreenRecordingCoordinator
        {
            CurrentCapabilities = RecordingRuntimeCapabilities.None
        };

        using var viewModel = new RecordingViewModel(coordinator)
        {
            CanPauseResume = true
        };

        await viewModel.PauseResumeCommand.ExecuteAsync(null);

        Assert.That(coordinator.TogglePauseResumeCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task RecordingViewModel_AbortCommand_UsesInjectedCoordinator()
    {
        var coordinator = new FakeScreenRecordingCoordinator();
        using var viewModel = new RecordingViewModel(coordinator);

        viewModel.CanAbort = true;
        await viewModel.AbortRecordingCommand.ExecuteAsync(null);

        Assert.That(coordinator.AbortCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task UploadContentViewModel_UploadAll_UsesInjectedTaskManager()
    {
        var taskManager = new FakeDesktopTaskManager();
        using var viewModel = new UploadContentViewModel(taskManager);

        viewModel.AddTextItem("hello from xip0052");
        await viewModel.UploadAllCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(taskManager.StartTextTaskCalls, Is.EqualTo(1));
            Assert.That(taskManager.LastText, Is.EqualTo("hello from xip0052"));
        });
    }
}
