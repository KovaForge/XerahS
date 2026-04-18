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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace XerahS.UI.Services;

internal static class StorageProviderResolver
{
    public static IStorageProvider? Resolve(Window? preferredWindow = null, Window? fallbackOwner = null)
    {
        if (preferredWindow?.StorageProvider != null)
        {
            return preferredWindow.StorageProvider;
        }

        var preferredTopLevel = fallbackOwner != null ? TopLevel.GetTopLevel(fallbackOwner) : null;
        if (preferredTopLevel?.StorageProvider != null)
        {
            return preferredTopLevel.StorageProvider;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Windows.FirstOrDefault(window => window.IsVisible && window.IsActive)?.StorageProvider is { } activeStorageProvider)
            {
                return activeStorageProvider;
            }

            if (desktop.Windows.LastOrDefault(window => window.IsVisible)?.StorageProvider is { } visibleStorageProvider)
            {
                return visibleStorageProvider;
            }

            return desktop.MainWindow?.StorageProvider;
        }

        return null;
    }
}
