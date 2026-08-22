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

using Microsoft.Extensions.DependencyInjection;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Shared composition entry point for non-Avalonia desktop hosts.
    /// </summary>
    public static class DesktopHostComposition
    {
        /// <summary>
        /// Creates the canonical service collection shared by all desktop host modes.
        /// </summary>
        public static IServiceCollection CreateServiceCollection(
            DesktopHostServices hostServices,
            Action<IServiceCollection>? configureServices = null)
        {
            ArgumentNullException.ThrowIfNull(hostServices);
            return ComposeServiceCollection(hostServices, configureServices);
        }

        /// <summary>
        /// Builds a provider from explicit host-owned service instances.
        /// </summary>
        public static IServiceProvider CreateServiceProvider(
            DesktopHostServices hostServices,
            Action<IServiceCollection>? configureServices = null)
        {
            ArgumentNullException.ThrowIfNull(hostServices);
            return BuildServiceProvider(hostServices, configureServices);
        }

        /// <summary>
        /// Compatibility shim for callers that still use the process-wide registries.
        /// </summary>
        public static IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configureServices = null)
            => BuildServiceProvider(null, configureServices);

        private static IServiceProvider BuildServiceProvider(
            DesktopHostServices? hostServices,
            Action<IServiceCollection>? configureServices) =>
            ComposeServiceCollection(hostServices, configureServices).BuildServiceProvider();

        private static IServiceCollection ComposeServiceCollection(
            DesktopHostServices? hostServices,
            Action<IServiceCollection>? configureServices)
        {
            var services = new ServiceCollection();

            if (hostServices != null)
            {
                services.AddXerahSDesktopHostServices(hostServices);
            }
            else
            {
                services.AddXerahSDesktopHostServices();
            }

            configureServices?.Invoke(services);
            return services;
        }
    }
}
