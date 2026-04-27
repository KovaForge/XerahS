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

using Avalonia.Input;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

public class LinuxHotkeyServiceTests
{
    [Test]
    public void CandidateKeysymNames_PrintScreen_IncludePrintAliases()
    {
        var names = LinuxHotkeyService.GetCandidateKeysymNames(Key.PrintScreen);

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Print"));
            Assert.That(names, Does.Contain("Sys_Req"));
        });
    }

    [Test]
    public void CandidateKeysymNames_Snapshot_IncludePrintAliases()
    {
        var names = LinuxHotkeyService.GetCandidateKeysymNames(Key.Snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Print"));
            Assert.That(names, Does.Contain("Sys_Req"));
        });
    }

    [Test]
    public void CandidateKeysymNames_LetterKey_MapsToSingleLiteral()
    {
        var names = LinuxHotkeyService.GetCandidateKeysymNames(Key.A);

        Assert.That(names, Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void WaylandPortalHotkeyService_BuildShortcutSnapshotMap_ToleratesDuplicatePortalIds()
    {
        var first = new Dictionary<string, object>
        {
            ["trigger_description"] = "First binding"
        };
        var second = new Dictionary<string, object>
        {
            ["trigger_description"] = "Updated binding"
        };

        var map = WaylandPortalHotkeyService.BuildShortcutSnapshotMap(
        [
            ValueTuple.Create("1", (IDictionary<string, object>)first),
            ValueTuple.Create("", (IDictionary<string, object>)new Dictionary<string, object>()),
            ValueTuple.Create("1", (IDictionary<string, object>)second)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(map, Has.Count.EqualTo(1));
            Assert.That(map["1"], Is.SameAs(second));
        });
    }

    [Test]
    public async Task WaylandPortalHotkeyService_DisposeWaitsForInFlightRebind()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new WaylandPortalHotkeyService(async () =>
        {
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
        }, skipPortalInitialization: true);

        service.ScheduleRebindForTesting();
        await started.Task.ConfigureAwait(false);

        var disposeTask = Task.Run(service.Dispose);
        await Task.Delay(150).ConfigureAwait(false);

        Assert.That(disposeTask.IsCompleted, Is.False);

        release.TrySetResult();
        await disposeTask.ConfigureAwait(false);
    }

    [Test]
    public async Task WaylandPortalHotkeyService_DisposeCancelsPendingDebounce()
    {
        int rebindCalls = 0;
        var service = new WaylandPortalHotkeyService(() =>
        {
            Interlocked.Increment(ref rebindCalls);
            return Task.CompletedTask;
        }, skipPortalInitialization: true);

        service.ScheduleRebindForTesting();
        service.Dispose();

        await Task.Delay(250).ConfigureAwait(false);

        Assert.That(rebindCalls, Is.EqualTo(0));
    }
}

