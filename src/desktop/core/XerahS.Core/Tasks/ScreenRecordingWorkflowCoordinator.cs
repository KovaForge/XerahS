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

using XerahS.RegionCapture.ScreenRecording;
using XerahS.Services.Abstractions;

namespace XerahS.Core.Tasks;

/// <summary>
/// Small coordinator around the shared recording manager so worker tasks do not talk to the singleton directly.
/// </summary>
internal sealed class ScreenRecordingWorkflowCoordinator(IScreenRecordingManager recordingManager)
{
    public string? PlannedOutputPath => recordingManager.PlannedOutputPath;

    public Task StartRecordingAsync(RecordingOptions options) => recordingManager.StartRecordingAsync(options);

    public Task WaitForStopSignalAsync() => recordingManager.WaitForStopSignalAsync();

    public Task<string?> StopRecordingAsync() => recordingManager.StopRecordingAsync();

    public void SignalStop() => recordingManager.SignalStop();

    public Task AbortRecordingAsync() => recordingManager.AbortRecordingAsync();

    public Task TogglePauseResumeAsync() => recordingManager.TogglePauseResumeAsync();
}
