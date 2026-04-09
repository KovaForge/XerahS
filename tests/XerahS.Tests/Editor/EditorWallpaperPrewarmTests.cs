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
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.ViewModels;
using System.Threading;

namespace XerahS.Tests.Editor;

[TestFixture]
[NonParallelizable]
public class EditorWallpaperPrewarmTests
{
    [TearDown]
    public void TearDown()
    {
        EditorServices.DesktopWallpaper = null;
        EditorServices.Diagnostics = null;
    }

    [Test]
    public void Constructor_StartsWallpaperPrewarm_DuringInitialization()
    {
        using var wallpaperService = new TrackingDesktopWallpaperService();
        EditorServices.DesktopWallpaper = wallpaperService;

        _ = new MainViewModel(new ImageEditorOptions());

        Assert.Multiple(() =>
        {
            Assert.That(wallpaperService.WaitForPrewarmStarted(), Is.True);
            Assert.That(wallpaperService.WaitForCompletion(), Is.True);
            Assert.That(wallpaperService.PrewarmCount, Is.EqualTo(1));
            Assert.That(wallpaperService.LookupCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void SecondEditorInstance_DoesNotStartSecondPrewarm_AfterFirstPrewarmCompletes()
    {
        using var wallpaperService = new TrackingDesktopWallpaperService();
        EditorServices.DesktopWallpaper = wallpaperService;

        _ = new MainViewModel(new ImageEditorOptions());

        Assert.Multiple(() =>
        {
            Assert.That(wallpaperService.WaitForPrewarmStarted(), Is.True);
            Assert.That(wallpaperService.WaitForCompletion(), Is.True);
            Assert.That(wallpaperService.PrewarmCount, Is.EqualTo(1));
            Assert.That(wallpaperService.LookupCount, Is.EqualTo(0));
        });

        _ = new MainViewModel(new ImageEditorOptions());

        Assert.That(
            SpinWait.SpinUntil(() => wallpaperService.PrewarmCount > 1, 200),
            Is.False,
            "Constructing another editor instance should reuse the completed wallpaper prewarm instead of starting another prewarm.");
    }

    [Test]
    public void Constructor_DoesNotStartWallpaperPrewarm_WhenServiceDoesNotRequireIt()
    {
        using var wallpaperService = new TrackingDesktopWallpaperService(requiresPrewarm: false);
        EditorServices.DesktopWallpaper = wallpaperService;

        _ = new MainViewModel(new ImageEditorOptions());

        Assert.That(
            SpinWait.SpinUntil(() => wallpaperService.PrewarmCount > 0, 200),
            Is.False,
            "Services that do not require wallpaper prewarm should not be scheduled by the editor.");
    }

    private sealed class TrackingDesktopWallpaperService : IDesktopWallpaperService, IDisposable
    {
        private readonly ManualResetEventSlim _prewarmStarted = new(false);
        private readonly ManualResetEventSlim _allowCompletion;
        private readonly ManualResetEventSlim _prewarmCompleted = new(false);
        private readonly bool _requiresPrewarm;
        private int _prewarmCount;
        private int _lookupCount;

        public TrackingDesktopWallpaperService(bool blockPrewarm = false, bool requiresPrewarm = true)
        {
            _allowCompletion = new ManualResetEventSlim(!blockPrewarm);
            _requiresPrewarm = requiresPrewarm;
        }

        public bool IsSupported => true;
        public bool RequiresDesktopWallpaperPrewarm => _requiresPrewarm;

        public int LookupCount => Volatile.Read(ref _lookupCount);
        public int PrewarmCount => Volatile.Read(ref _prewarmCount);

        public bool TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            Interlocked.Increment(ref _lookupCount);
            wallpaper = null;
            return true;
        }

        public void PrewarmDesktopWallpaper()
        {
            Interlocked.Increment(ref _prewarmCount);
            _prewarmStarted.Set();
            _allowCompletion.Wait(TimeSpan.FromSeconds(2));
            _prewarmCompleted.Set();
        }

        public void Release()
        {
            _allowCompletion.Set();
        }

        public bool WaitForPrewarmStarted(int timeoutMilliseconds = 1000)
        {
            return _prewarmStarted.Wait(timeoutMilliseconds);
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 1000)
        {
            return _prewarmCompleted.Wait(timeoutMilliseconds);
        }

        public void Dispose()
        {
            _prewarmStarted.Dispose();
            _allowCompletion.Dispose();
            _prewarmCompleted.Dispose();
        }
    }
}
