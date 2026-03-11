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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services
{
    public class WindowsSystemService : ISystemService
    {
        private const int SpiGetDesktopWallpaper = 0x0073;
        private const int MaxWallpaperPath = 260;

        public bool ShowFileInExplorer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string args = $"/select,\"{filePath.Replace('/', '\\')}\"";

                Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return false;
        }

        public bool OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return false;
        }

        public bool OpenFile(string filePath)
        {
             if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath))) return false;

             try
             {
                 Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                 return true;
             }
             catch (Exception ex)
             {
                 Debug.WriteLine(ex);
             }
             return false;
        }

        public bool TryGetDesktopWallpaperPath(out string? path)
        {
            path = null;

            StringBuilder buffer = new StringBuilder(MaxWallpaperPath);
            if (!SystemParametersInfo(SpiGetDesktopWallpaper, buffer.Capacity, buffer, 0))
            {
                return false;
            }

            string wallpaperPath = buffer.ToString().TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
            {
                return false;
            }

            path = wallpaperPath;
            return true;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);
    }
}
