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
/// OmaSnap as a provider in the Linux capture waterfall (stage <see cref="LinuxCaptureStage.OmaSnap"/>),
/// so interactive region captures show up in the decision trace as
/// <c>stage=OmaSnap, provider=omasnap, outcome=…</c>. The policy only puts this stage first when
/// <see cref="IOmaSnapService.ShouldHandle"/> agrees; a failure falls through to the existing
/// chain and a user cancel stays a cancel.
/// </summary>
internal sealed class OmaSnapCaptureStrategy : ILinuxCaptureProvider
{
    public const string Id = "omasnap";

    private readonly Func<IOmaSnapService?> _service;

    public OmaSnapCaptureStrategy()
        : this(() => PlatformServices.OmaSnap)
    {
    }

    internal OmaSnapCaptureStrategy(Func<IOmaSnapService?> service)
    {
        _service = service;
    }

    public string ProviderId => Id;

    public LinuxCaptureStage Stage => LinuxCaptureStage.OmaSnap;

    public bool CanHandle(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        // Full-screen and window requests also serve internal crop flows across all monitors,
        // which OmaSnap (focused monitor only) must not answer.
        return request.Kind == LinuxCaptureKind.Region &&
               !context.IsSandboxed &&
               _service()?.ShouldHandle(request.SelectorPreference) == true;
    }

    public async Task<LinuxCaptureResult> TryCaptureAsync(LinuxCaptureRequest request, ILinuxCaptureContext context, CancellationToken cancellationToken = default)
    {
        IOmaSnapService? service = _service();
        if (service == null)
        {
            return LinuxCaptureResult.Failure(Id);
        }

        OmaSnapCaptureResult result = await service.CaptureAsync(new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart), cancellationToken).ConfigureAwait(false);
        try
        {
            switch (result.Outcome)
            {
                case OmaSnapOutcome.Cancelled:
                    return LinuxCaptureResult.Cancelled(Id);
                case OmaSnapOutcome.Succeeded when result.ImagePath != null:
                    SKBitmap? bitmap = SKBitmap.Decode(result.ImagePath);
                    if (bitmap != null)
                    {
                        return LinuxCaptureResult.Success(Id, bitmap);
                    }

                    DebugHelper.WriteLine($"OmaSnap: could not decode {result.ImagePath}.");
                    return LinuxCaptureResult.Failure(Id);
                default:
                    return LinuxCaptureResult.Failure(Id);
            }
        }
        finally
        {
            service.Release(result);
        }
    }
}
