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

using ShareX.ImageEditor.Hosting;
using XerahS.Platform.Abstractions;
using EditorWallpaperInfo = ShareX.ImageEditor.Hosting.DesktopWallpaperInfo;
using EditorWallpaperLayout = ShareX.ImageEditor.Hosting.DesktopWallpaperLayout;
using PlatformWallpaperInfo = XerahS.Platform.Abstractions.DesktopWallpaperInfo;
using PlatformWallpaperLayout = XerahS.Platform.Abstractions.DesktopWallpaperLayout;

namespace XerahS.UI.Services;

/// <summary>
/// Adapts the editor wallpaper contract to XerahS platform services.
/// </summary>
public sealed class EditorDesktopWallpaperAdapter : IDesktopWallpaperService
{
    public bool IsSupported => PlatformServices.IsInitialized && PlatformServices.System.IsDesktopWallpaperSupported;

    public bool TryGetDesktopWallpaper(out EditorWallpaperInfo? wallpaper)
    {
        if (!PlatformServices.IsInitialized)
        {
            wallpaper = null;
            return false;
        }

        if (!PlatformServices.System.TryGetDesktopWallpaper(out PlatformWallpaperInfo? platformWallpaper) ||
            platformWallpaper == null)
        {
            wallpaper = null;
            return false;
        }

        wallpaper = new EditorWallpaperInfo
        {
            Path = platformWallpaper.Path,
            Layout = MapLayout(platformWallpaper.Layout)
        };

        return true;
    }

    private static EditorWallpaperLayout MapLayout(PlatformWallpaperLayout layout)
    {
        return layout switch
        {
            PlatformWallpaperLayout.Fill => EditorWallpaperLayout.Fill,
            PlatformWallpaperLayout.Fit => EditorWallpaperLayout.Fit,
            PlatformWallpaperLayout.Stretch => EditorWallpaperLayout.Stretch,
            PlatformWallpaperLayout.Center => EditorWallpaperLayout.Center,
            PlatformWallpaperLayout.Tile => EditorWallpaperLayout.Tile,
            PlatformWallpaperLayout.Span => EditorWallpaperLayout.Span,
            _ => EditorWallpaperLayout.Fill
        };
    }
}
