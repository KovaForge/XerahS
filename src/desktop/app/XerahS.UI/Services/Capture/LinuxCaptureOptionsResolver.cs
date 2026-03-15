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

using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services.Capture
{
    /// <summary>
    /// Centralises Linux-specific capture option normalisation logic.
    /// Extracted from <see cref="ScreenCaptureService"/> to reduce its size
    /// and make the strategy independently testable.
    /// </summary>
    public static class LinuxCaptureOptionsResolver
    {
        /// <summary>
        /// When falling back to the XerahS overlay path on Linux, force the follow-up
        /// capture to stay on the legacy capture pipeline.
        /// </summary>
        public static CaptureOptions? NormalizeLinuxOverlayCaptureOptions(
            CaptureOptions? options,
            LinuxRegionCaptureCapability? linuxCapability,
            string logPrefix)
        {
            if (!OperatingSystem.IsLinux())
            {
                return options;
            }

            if (linuxCapability is not { SupportsLegacyOverlayCapture: true } capability)
            {
                return options;
            }

            bool alreadyOnLegacyLinuxPath = options?.LinuxForceLegacyCapturePath == true &&
                GetLinuxRegionSelectorPreference(options) == LinuxInteractiveRegionSelectorPreference.XerahSOverlay;

            if (alreadyOnLegacyLinuxPath)
            {
                return options;
            }

            DebugHelper.WriteLine($"{logPrefix}: forcing Linux overlay follow-up capture to stay on the legacy path. Reason={capability.Reason}");

            return CloneCaptureOptions(
                options,
                useModernCapture: options?.UseModernCapture ?? true,
                linuxRegionSelectorPreference: LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
                linuxForceLegacyCapturePath: true);
        }

        /// <summary>
        /// For native region capture on Linux, ensure the options reflect the effective preference
        /// and disable the legacy capture path flag.
        /// </summary>
        public static CaptureOptions? NormalizeLinuxNativeCaptureOptions(
            CaptureOptions? options,
            LinuxInteractiveRegionSelectorPreference effectivePreference,
            string logPrefix)
        {
            if (!OperatingSystem.IsLinux())
            {
                return options;
            }

            bool needsClone = options?.LinuxForceLegacyCapturePath == true ||
                GetLinuxRegionSelectorPreference(options) != effectivePreference;

            if (!needsClone)
            {
                return options;
            }

            DebugHelper.WriteLine($"{logPrefix}: selecting Linux native selector '{effectivePreference}'.");
            return CloneCaptureOptions(
                options,
                useModernCapture: options?.UseModernCapture ?? true,
                linuxRegionSelectorPreference: effectivePreference,
                linuxForceLegacyCapturePath: false);
        }

        /// <summary>
        /// Determines whether we should attempt native (portal/slurp/etc.) region capture on Linux
        /// based on the resolved preference and platform capability.
        /// </summary>
        public static bool ShouldTryLinuxNativeRegionCapture(
            LinuxInteractiveRegionSelectorPreference effectivePreference,
            LinuxRegionCaptureCapability? linuxCapability)
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            bool canUseLinuxOverlayFallback = linuxCapability?.SupportsLegacyOverlayCapture == true;
            bool supportsNativeRegionCapture = linuxCapability?.SupportsNativeRegionCapture ?? true;

            return effectivePreference switch
            {
                LinuxInteractiveRegionSelectorPreference.XerahSOverlay => !canUseLinuxOverlayFallback && supportsNativeRegionCapture,
                LinuxInteractiveRegionSelectorPreference.PortalDialog or
                    LinuxInteractiveRegionSelectorPreference.DesktopNative or
                    LinuxInteractiveRegionSelectorPreference.Slurp => true,
                _ => supportsNativeRegionCapture
            };
        }

        /// <summary>
        /// Gets the user-configured Linux region selector preference from CaptureOptions.
        /// </summary>
        public static LinuxInteractiveRegionSelectorPreference GetLinuxRegionSelectorPreference(CaptureOptions? options)
        {
            return options?.LinuxRegionSelectorPreference ?? LinuxInteractiveRegionSelectorPreference.Automatic;
        }

        /// <summary>
        /// Creates a deep clone of CaptureOptions with optional overrides.
        /// </summary>
        public static CaptureOptions CloneCaptureOptions(
            CaptureOptions? options,
            bool useModernCapture,
            LinuxInteractiveRegionSelectorPreference? linuxRegionSelectorPreference = null,
            bool? linuxForceLegacyCapturePath = null)
        {
            return new CaptureOptions
            {
                UseModernCapture = useModernCapture,
                LinuxRegionSelectorPreference =
                    linuxRegionSelectorPreference ?? options?.LinuxRegionSelectorPreference ??
                    LinuxInteractiveRegionSelectorPreference.Automatic,
                LinuxForceLegacyCapturePath = linuxForceLegacyCapturePath ?? options?.LinuxForceLegacyCapturePath ?? false,
                ShowCursor = options?.ShowCursor ?? true,
                CaptureTransparent = options?.CaptureTransparent ?? false,
                UseTransparentOverlay = options?.UseTransparentOverlay ?? false,
                CaptureShadow = options?.CaptureShadow ?? true,
                CaptureClientArea = options?.CaptureClientArea ?? false,
                WorkflowId = options?.WorkflowId,
                WorkflowCategory = options?.WorkflowCategory,
                CaptureStartDelaySeconds = options?.CaptureStartDelaySeconds ?? 0,
                CaptureStartDelayCancellationToken = options?.CaptureStartDelayCancellationToken ?? default,
                VirtualScreenBoundsForCrop = options?.VirtualScreenBoundsForCrop,
                PhysicalVirtualScreenBoundsForCrop = options?.PhysicalVirtualScreenBoundsForCrop,
                PhysicalRectForCrop = options?.PhysicalRectForCrop
            };
        }
    }
}
