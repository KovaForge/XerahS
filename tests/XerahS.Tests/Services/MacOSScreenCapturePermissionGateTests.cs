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
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;

namespace XerahS.Tests.Services;

public class MacOSScreenCapturePermissionGateTests
{
    [Test]
    public void NonMacOSCaptureDoesNotRunPermissionPreflight()
    {
        var captureService = new PermissionAwareCaptureService(false);
        int notificationCount = 0;

        bool result = ScreenCaptureService.EnsurePlatformCaptureAccess(
            captureService,
            false,
            () => notificationCount++);

        Assert.That(result, Is.True);
        Assert.That(captureService.PreflightCallCount, Is.Zero);
        Assert.That(notificationCount, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MacOSCaptureUsesPlatformPermissionResult(bool permissionGranted)
    {
        var captureService = new PermissionAwareCaptureService(permissionGranted);
        int notificationCount = 0;

        bool result = ScreenCaptureService.EnsurePlatformCaptureAccess(
            captureService,
            true,
            () => notificationCount++);

        Assert.That(result, Is.EqualTo(permissionGranted));
        Assert.That(captureService.PreflightCallCount, Is.EqualTo(1));
        Assert.That(notificationCount, Is.EqualTo(permissionGranted ? 0 : 1));
    }

    [Test]
    public void CaptureServiceWithoutPermissionPreflightRemainsSupported()
    {
        int notificationCount = 0;

        bool result = ScreenCaptureService.EnsurePlatformCaptureAccess(
            new CaptureService(),
            true,
            () => notificationCount++);

        Assert.That(result, Is.True);
        Assert.That(notificationCount, Is.Zero);
    }

    [Test]
    public void PermissionDeniedToastIsCriticalAndActionable()
    {
        ToastConfig config = ScreenCaptureService.CreateMacOSCapturePermissionDeniedToastConfig();

        Assert.Multiple(() =>
        {
            Assert.That(config.Title, Is.EqualTo("Screen Recording permission required"));
            Assert.That(config.Text, Does.Contain("System Settings > Privacy & Security > Screen Recording"));
            Assert.That(config.Text, Does.Contain("restart XerahS"));
            Assert.That(config.IgnoreGlobalDisable, Is.True);
            Assert.That(config.LeftClickAction, Is.EqualTo(ToastClickAction.CloseNotification));
            Assert.That(AvaloniaToastService.ShouldSuppressToast(config, true), Is.False);
            Assert.That(AvaloniaToastService.ShouldSuppressToast(new ToastConfig(), true), Is.True);
        });
    }

    private class CaptureService : IScreenCaptureService
    {
        public Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null) => throw new NotSupportedException();

        public Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null) => throw new NotSupportedException();

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null) => throw new NotSupportedException();

        public Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null) => throw new NotSupportedException();

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null) => throw new NotSupportedException();

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null) => throw new NotSupportedException();

        public Task<CursorInfo?> CaptureCursorAsync() => throw new NotSupportedException();
    }

    private sealed class PermissionAwareCaptureService(bool permissionGranted) : CaptureService, IScreenCapturePermissionService
    {
        public int PreflightCallCount { get; private set; }

        public bool EnsureScreenCaptureAccess()
        {
            PreflightCallCount++;
            return permissionGranted;
        }
    }
}
