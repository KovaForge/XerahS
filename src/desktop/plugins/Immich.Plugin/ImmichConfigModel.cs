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

namespace ShareX.Immich.Plugin;

public enum ImmichAuthMode
{
    ApiKey,
    BootstrapApiKey
}

public enum ImmichShareMode
{
    None,
    Asset,
    Album
}

public class ImmichConfigModel
{
    public string SecretKey { get; set; } = Guid.NewGuid().ToString("N");
    public string ServerUrl { get; set; } = string.Empty;
    public ImmichAuthMode AuthMode { get; set; } = ImmichAuthMode.ApiKey;
    public string ApiKeyName { get; set; } = "XerahS Uploads";
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public string ExternalDomain { get; set; } = string.Empty;
    public bool PasswordLoginEnabled { get; set; } = true;
    public bool OAuthEnabled { get; set; }
    public bool SearchEnabled { get; set; }
    public bool DuplicateDetectionEnabled { get; set; } = true;
    public bool SidecarSupported { get; set; }
    public bool AddToAlbum { get; set; }
    public string AlbumId { get; set; } = string.Empty;
    public string AlbumName { get; set; } = "ShareX Uploads";
    public bool AutoCreateAlbum { get; set; } = true;
    public bool UseDuplicateCheck { get; set; } = true;
    public ImmichShareMode ShareMode { get; set; } = ImmichShareMode.None;
    public string ShareSlug { get; set; } = string.Empty;
    public bool UseShareExpiry { get; set; }
    public int ExpireAfterDays { get; set; } = 7;
    public bool AllowShareDownload { get; set; } = true;
    public bool AllowShareUpload { get; set; }
    public bool ShowMetadata { get; set; } = true;
}
