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

using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Complete set of explicit dependencies required by desktop, CLI, daemon, and MCP hosts.
    /// </summary>
    public sealed class DesktopHostServices
    {
        public DesktopHostServices(
            DesktopPlatformServices platform,
            DesktopApplicationServices application)
        {
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            Application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public DesktopPlatformServices Platform { get; }
        public DesktopApplicationServices Application { get; }

        /// <summary>
        /// Captures the current legacy registries for callers that have not yet adopted explicit ownership.
        /// </summary>
        public static DesktopHostServices FromCurrentProcess(
            IUIService? uiService = null,
            IToastService? toastService = null,
            IImageEncoderService? imageEncoderService = null) => new(
                DesktopPlatformServices.FromCurrentProcess(uiService, toastService, imageEncoderService),
                DesktopApplicationServices.FromCurrentProcess());
    }
}
