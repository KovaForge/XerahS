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

using Newtonsoft.Json;

namespace ShareX.Immich.Plugin;

internal sealed class ImmichLoginResponse
{
    [JsonProperty("accessToken")]
    public string AccessToken { get; set; } = string.Empty;
}

internal sealed class ImmichApiKeyCreateResponse
{
    [JsonProperty("secret")]
    public string Secret { get; set; } = string.Empty;
}

internal sealed class ImmichApiKeyResponse
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ImmichServerConfigResponse
{
    [JsonProperty("externalDomain")]
    public string ExternalDomain { get; set; } = string.Empty;
}

internal sealed class ImmichServerFeaturesResponse
{
    [JsonProperty("passwordLogin")]
    public bool PasswordLogin { get; set; }

    [JsonProperty("oauth")]
    public bool OAuth { get; set; }

    [JsonProperty("search")]
    public bool Search { get; set; }

    [JsonProperty("duplicateDetection")]
    public bool DuplicateDetection { get; set; }

    [JsonProperty("sidecar")]
    public bool Sidecar { get; set; }
}

internal sealed class ImmichServerVersionResponse
{
    [JsonProperty("major")]
    public int Major { get; set; }

    [JsonProperty("minor")]
    public int Minor { get; set; }

    [JsonProperty("patch")]
    public int Patch { get; set; }
}

internal sealed class ImmichServerAboutResponse
{
    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;
}

internal sealed class ImmichUserResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;
}

internal sealed class ImmichBulkUploadCheckResponse
{
    [JsonProperty("results")]
    public List<ImmichBulkUploadCheckItemResponse> Results { get; set; } = new();
}

internal sealed class ImmichBulkUploadCheckItemResponse
{
    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("reason")]
    public string? Reason { get; set; }

    [JsonProperty("assetId")]
    public string? AssetId { get; set; }
}

internal sealed class ImmichAssetUploadResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string? Status { get; set; }
}

internal sealed class ImmichAlbumResponse
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("albumName")]
    public string? AlbumName { get; set; }

    [JsonProperty("albumThumbnailAssetId")]
    public string? AlbumThumbnailAssetId { get; set; }

    [JsonProperty("assetCount")]
    public int AssetCount { get; set; }

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("shared")]
    public bool Shared { get; set; }

    [JsonProperty("hasSharedLink")]
    public bool HasSharedLink { get; set; }

    [JsonProperty("assets")]
    public List<ImmichAssetResponse>? Assets { get; set; }

    [JsonProperty("sharedLinks")]
    public List<ImmichSharedLinkResponse>? SharedLinks { get; set; }
}

internal sealed class ImmichAssetResponse
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("originalFileName")]
    public string? OriginalFileName { get; set; }

    [JsonProperty("originalMimeType")]
    public string? OriginalMimeType { get; set; }

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("fileCreatedAt")]
    public DateTimeOffset FileCreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ImmichSharedLinkResponse
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("key")]
    public string? Key { get; set; }

    [JsonProperty("slug")]
    public string? Slug { get; set; }

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonProperty("album")]
    public ImmichSharedLinkAlbumResponse? Album { get; set; }

    [JsonProperty("password")]
    public bool HasPassword { get; set; }

    [JsonProperty("allowDownload")]
    public bool AllowDownload { get; set; }

    [JsonProperty("allowUpload")]
    public bool AllowUpload { get; set; }

    [JsonProperty("showMetadata")]
    public bool ShowMetadata { get; set; }
}

internal sealed class ImmichSharedLinkAlbumResponse
{
    [JsonProperty("id")]
    public string? Id { get; set; }
}

internal sealed class ImmichBulkIdResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("success")]
    public bool Success { get; set; }
}
