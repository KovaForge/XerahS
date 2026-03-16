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

namespace ShareX.Nextcloud.Plugin;

public sealed class NextcloudServerProfile
{
    public string ServerUrl { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public string ServerProductName { get; set; } = "Nextcloud";
    public string ThemingName { get; set; } = string.Empty;
    public bool SupportsPublicShares { get; set; }
    public bool SupportsSharePasswords { get; set; }
    public bool SupportsExpireDate { get; set; }
    public bool SupportsChunking { get; set; }
    public bool SupportsSearch { get; set; }
}
