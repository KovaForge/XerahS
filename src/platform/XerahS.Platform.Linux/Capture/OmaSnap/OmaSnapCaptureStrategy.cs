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

using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>
/// Capture provider for the Linux capture coordinator: region captures through OmaSnap host mode.
/// Appears in the decision trace as <c>stage=OmaSnap, provider=omasnap</c>. The policy schedules it
/// only for the explicit OmaSnap selector; it declines when the probe has not accepted OmaSnap.
/// </summary>
internal sealed class OmaSnapCaptureStrategy : ILinuxCaptureProvider
{
    private readonly IHostedCaptureEngine _engine;

    public OmaSnapCaptureStrategy(IHostedCaptureEngine engine)
    {
        _engine = engine;
    }

    public string ProviderId => OmaSnapCaptureEngine.Id;

    public LinuxCaptureStage Stage => LinuxCaptureStage.OmaSnap;

    public bool CanHandle(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        return request.Kind == LinuxCaptureKind.Region &&
            context.IsWayland &&
            !context.IsSandboxed &&
            request.Options?.LinuxSkipHostedCaptureEngine != true;
    }

    public async Task<LinuxCaptureResult> TryCaptureAsync(
        LinuxCaptureRequest request,
        ILinuxCaptureContext context,
        CancellationToken cancellationToken = default)
    {
        HostedCaptureResult result = await _engine.CaptureAsync(new HostedCaptureRequest(HostedCaptureTarget.Smart), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            switch (result.Status)
            {
                case HostedCaptureStatus.Cancelled:
                    return LinuxCaptureResult.Cancelled(ProviderId);
                case HostedCaptureStatus.Ok when result.ImagePath != null:
                    SKBitmap? bitmap = SKBitmap.Decode(result.ImagePath);
                    return bitmap != null ? LinuxCaptureResult.Success(ProviderId, bitmap) : LinuxCaptureResult.Failure(ProviderId);
                default:
                    DebugHelper.WriteLine($"OmaSnapCaptureStrategy: {result.Status}: {result.Error}");
                    return LinuxCaptureResult.Failure(ProviderId);
            }
        }
        finally
        {
            _engine.Release(result);
        }
    }
}
