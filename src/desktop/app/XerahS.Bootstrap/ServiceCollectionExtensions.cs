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
using Microsoft.Extensions.DependencyInjection.Extensions;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Extension methods for registering XerahS desktop host services into an IServiceCollection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the shared desktop host composition used by bootstrap, CLI, and daemon hosts.
        /// </summary>
        public static IServiceCollection AddXerahSDesktopHostServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Platform services are resolved lazily from the static locator so hosts can share
            // one registration path without requiring Avalonia startup.
            services.TryAddSingleton(_ => PlatformServices.PlatformInfo);
            services.TryAddSingleton(_ => PlatformServices.Screen);
            services.TryAddSingleton(_ => PlatformServices.Clipboard);
            services.TryAddSingleton(_ => PlatformServices.Window);
            services.TryAddSingleton(_ => PlatformServices.Input);
            services.TryAddSingleton(_ => PlatformServices.Fonts);
            services.TryAddSingleton(_ => PlatformServices.Hotkey);
            services.TryAddSingleton(_ => PlatformServices.ScreenCapture);
            services.TryAddSingleton(_ => PlatformServices.Startup);
            services.TryAddSingleton(_ => PlatformServices.System);
            services.TryAddSingleton(_ => PlatformServices.Diagnostic);
            services.TryAddSingleton(_ => PlatformServices.WatchFolderDaemon);

            var shellIntegration = PlatformServices.GetShellIntegrationIfAvailable();
            if (shellIntegration != null)
            {
                services.TryAddSingleton(_ => shellIntegration);
            }

            var notification = PlatformServices.GetNotificationIfAvailable();
            if (notification != null)
            {
                services.TryAddSingleton(_ => notification);
            }

            services.TryAddSingleton(_ => TaskManager.Instance);
            services.TryAddSingleton<ITaskManager>(_ => TaskManager.Instance);
            services.TryAddSingleton<IDesktopTaskManager, DesktopTaskManagerAdapter>();

            services.TryAddSingleton(_ => ScreenRecordingManager.Instance);
            services.TryAddSingleton<IScreenRecordingManager>(_ => ScreenRecordingManager.Instance);
            services.TryAddSingleton<IScreenRecordingCoordinator, ScreenRecordingCoordinatorAdapter>();

            services.TryAddSingleton(_ => WatchFolderManager.Instance);
            services.TryAddSingleton<IWatchFolderDaemonController, WatchFolderDaemonControllerAdapter>();

            return services;
        }

        /// <summary>
        /// Compatibility shim for existing callers. Prefer <see cref="AddXerahSDesktopHostServices"/>.
        /// </summary>
        public static IServiceCollection AddXerahSPlatformServices(this IServiceCollection services) =>
            services.AddXerahSDesktopHostServices();
    }
}
