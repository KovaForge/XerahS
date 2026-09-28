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

using System.Collections.Generic;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.Orchestration;

internal sealed class WaterfallCapturePolicy : ILinuxCapturePolicy
{
    private static readonly LinuxCaptureStage[] SandboxedOrder =
    {
        LinuxCaptureStage.Portal
    };

    private static readonly LinuxCaptureStage[] DefaultOrder =
    {
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.WaylandProtocol,
        LinuxCaptureStage.X11
    };

    private static readonly LinuxCaptureStage[] X11RegionOrder =
    {
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.X11,
        LinuxCaptureStage.Portal
    };

    private static readonly LinuxCaptureStage[] X11PortalPreferredRegionOrder =
    {
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.X11
    };

    private static readonly LinuxCaptureStage[] PortalFirstRegionOrder =
    {
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.WaylandProtocol,
        LinuxCaptureStage.X11
    };

    private static readonly LinuxCaptureStage[] PortalOnlyRegionOrder =
    {
        LinuxCaptureStage.Portal
    };

    private static readonly LinuxCaptureStage[] DesktopNativeOnlyRegionOrder =
    {
        LinuxCaptureStage.DesktopDbus
    };

    private static readonly LinuxCaptureStage[] SlurpOnlyRegionOrder =
    {
        LinuxCaptureStage.WaylandProtocol
    };

    private readonly Func<LinuxInteractiveRegionSelectorPreference, bool>? _omaSnapEligible;

    public WaterfallCapturePolicy()
        : this(null)
    {
    }

    /// <param name="omaSnapEligible">
    /// XIP0088: true when OmaSnap should front interactive region captures for a preference
    /// (probe passed, and the user chose OmaSnap or Automatic on an Omarchy-like session). Null or
    /// false keeps every order below exactly as before.
    /// </param>
    public WaterfallCapturePolicy(Func<LinuxInteractiveRegionSelectorPreference, bool>? omaSnapEligible)
    {
        _omaSnapEligible = omaSnapEligible;
    }

    public IReadOnlyList<LinuxCaptureStage> GetStageOrder(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        IReadOnlyList<LinuxCaptureStage> order = GetExistingStageOrder(request, context);
        if (!context.IsSandboxed &&
            request.Kind == LinuxCaptureKind.Region &&
            _omaSnapEligible?.Invoke(request.SelectorPreference) == true)
        {
            var withOmaSnap = new List<LinuxCaptureStage>(order.Count + 1) { LinuxCaptureStage.OmaSnap };
            withOmaSnap.AddRange(order);
            return withOmaSnap;
        }

        return order;
    }

    private static IReadOnlyList<LinuxCaptureStage> GetExistingStageOrder(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        if (context.IsSandboxed)
        {
            return SandboxedOrder;
        }

        if (!context.IsWayland && request.Kind == LinuxCaptureKind.Region)
        {
            switch (request.SelectorPreference)
            {
                case LinuxInteractiveRegionSelectorPreference.PortalDialog:
                    return PortalOnlyRegionOrder;
                case LinuxInteractiveRegionSelectorPreference.DesktopNative:
                    return DesktopNativeOnlyRegionOrder;
            }

            if (context.PrefersPortalForRegionCaptureOnX11)
            {
                return X11PortalPreferredRegionOrder;
            }

            return X11RegionOrder;
        }

        if (context.IsWayland && request.Kind == LinuxCaptureKind.Region)
        {
            return request.SelectorPreference switch
            {
                LinuxInteractiveRegionSelectorPreference.Slurp => SlurpOnlyRegionOrder,
                LinuxInteractiveRegionSelectorPreference.PortalDialog => PortalOnlyRegionOrder,
                _ => DefaultOrder
            };
        }

        return DefaultOrder;
    }
}
