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
            Assert.That(wallpaperService.WaitForLookupStarted(), Is.True);
            Assert.That(wallpaperService.WaitForCompletion(), Is.True);
            Assert.That(wallpaperService.LookupCount, Is.EqualTo(1));
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
            Assert.That(wallpaperService.WaitForLookupStarted(), Is.True);
            Assert.That(wallpaperService.WaitForCompletion(), Is.True);
            Assert.That(wallpaperService.LookupCount, Is.EqualTo(1));
        });

        _ = new MainViewModel(new ImageEditorOptions());

        Assert.That(
            SpinWait.SpinUntil(() => wallpaperService.LookupCount > 1, 200),
            Is.False,
            "Constructing another editor instance should reuse the completed wallpaper prewarm instead of starting another lookup.");
    }

    private sealed class TrackingDesktopWallpaperService : IDesktopWallpaperService, IDisposable
    {
        private readonly ManualResetEventSlim _lookupStarted = new(false);
        private readonly ManualResetEventSlim _allowCompletion;
        private readonly ManualResetEventSlim _lookupCompleted = new(false);
        private int _lookupCount;

        public TrackingDesktopWallpaperService(bool blockLookup = false)
        {
            _allowCompletion = new ManualResetEventSlim(!blockLookup);
        }

        public bool IsSupported => true;

        public int LookupCount => Volatile.Read(ref _lookupCount);

        public bool TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            Interlocked.Increment(ref _lookupCount);
            _lookupStarted.Set();
            _allowCompletion.Wait(TimeSpan.FromSeconds(2));

            wallpaper = new DesktopWallpaperInfo
            {
                Path = @"C:\temp\wallpaper.png",
                Layout = DesktopWallpaperLayout.Fill
            };

            _lookupCompleted.Set();
            return true;
        }

        public void Release()
        {
            _allowCompletion.Set();
        }

        public bool WaitForLookupStarted(int timeoutMilliseconds = 1000)
        {
            return _lookupStarted.Wait(timeoutMilliseconds);
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 1000)
        {
            return _lookupCompleted.Wait(timeoutMilliseconds);
        }

        public void Dispose()
        {
            _lookupStarted.Dispose();
            _allowCompletion.Dispose();
            _lookupCompleted.Dispose();
        }
    }
}
