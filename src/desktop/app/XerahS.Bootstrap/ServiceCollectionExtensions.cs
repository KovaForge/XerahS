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
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Extension methods for registering XerahS platform services into an IServiceCollection.
    /// Bridges the existing static PlatformServices locator with the M.E.DI container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all platform services that are already initialized
        /// in the static <see cref="PlatformServices"/> locator as singletons.
        /// Call this AFTER <c>PlatformServices.Initialize()</c> has completed.
        /// </summary>
        public static IServiceCollection AddXerahSPlatformServices(this IServiceCollection services)
        {
            // Core platform services (always present after Initialize)
            services.AddSingleton(_ => PlatformServices.PlatformInfo);
            services.AddSingleton(_ => PlatformServices.Screen);
            services.AddSingleton(_ => PlatformServices.Clipboard);
            services.AddSingleton(_ => PlatformServices.Window);
            services.AddSingleton(_ => PlatformServices.Input);
            services.AddSingleton(_ => PlatformServices.Fonts);
            services.AddSingleton(_ => PlatformServices.Hotkey);
            services.AddSingleton(_ => PlatformServices.ScreenCapture);
            services.AddSingleton(_ => PlatformServices.Startup);
            services.AddSingleton(_ => PlatformServices.System);
            services.AddSingleton(_ => PlatformServices.Diagnostic);
            services.AddSingleton(_ => PlatformServices.WatchFolderDaemon);

            // Optional services — registered only when available
            var shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
            if (shellIntegration != null)
            {
                services.AddSingleton(_ => shellIntegration);
            }

            var notification = PlatformServices.GetNotificationIfAvailable();
            if (notification != null)
            {
                services.AddSingleton(_ => notification);
            }

            // Core managers
            services.AddSingleton(_ => Core.Managers.ScreenRecordingManager.Instance);

            return services;
        }
    }
}
