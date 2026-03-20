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
using XerahS.Bootstrap;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.UI.Services
{
    /// <summary>
    /// Single composition root: builds the DI container from the shared desktop host path
    /// and layers UI-specific services on top.
    /// </summary>
    public static class CompositionRoot
    {
        /// <summary>
        /// Builds the service provider from current <see cref="PlatformServices"/> state
        /// and sets it as the root provider. No-op if platform services are not initialized.
        /// </summary>
        public static void BuildAndSetRootProvider()
        {
            if (!PlatformServices.IsInitialized)
            {
                return;
            }

            var services = new ServiceCollection();
            services.AddXerahSDesktopHostServices();

            if (PlatformServices.IsThemeServiceInitialized)
            {
                services.AddSingleton(_ => PlatformServices.Theme);
            }

            if (PlatformServices.ScrollingCapture is { } scrollingCapture)
            {
                services.AddSingleton(_ => scrollingCapture);
            }

            if (PlatformServices.Ocr is { } ocr)
            {
                services.AddSingleton(_ => ocr);
            }

            // App services (registered in OnFrameworkInitializationCompleted before this is called)
            if (PlatformServices.IsToastServiceInitialized)
            {
                services.AddSingleton(_ => PlatformServices.Toast);
            }

            try
            {
                services.AddSingleton(_ => PlatformServices.UI);
            }
            catch (InvalidOperationException)
            {
                // UI not registered (e.g. headless/bootstrap)
            }

            try
            {
                services.AddSingleton(_ => PlatformServices.ImageEncoder);
            }
            catch (InvalidOperationException)
            {
                // ImageEncoder not registered
            }

            services.AddSingleton<IViewDialogService, AvaloniaDialogService>();
            services.AddSingleton<IDialogService, AvaloniaDialogServiceAdapter>();
            services.AddSingleton<ILifecycleService, AvaloniaLifecycleService>();
            services.AddSingleton<IUiViewModelFactory, UiViewModelFactory>();

            IServiceProvider provider = services.BuildServiceProvider();
            PlatformServices.SetRootProvider(provider);
        }
    }
}
