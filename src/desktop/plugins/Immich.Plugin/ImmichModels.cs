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

public sealed class ImmichAlbum
{
    public string Id { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public string? ThumbnailAssetId { get; set; }
    public int AssetCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Shared { get; set; }
    public bool HasSharedLink { get; set; }
    public string? SharedLinkUrl { get; set; }
    public IReadOnlyList<ImmichAsset> Assets { get; set; } = Array.Empty<ImmichAsset>();
}

public sealed class ImmichAsset
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
}

public sealed class ImmichSharedLink
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class ImmichDuplicateCheckResult
{
    public ImmichDuplicateCheckResult(string action, string? reason, string? assetId)
    {
        Action = action ?? string.Empty;
        Reason = reason;
        AssetId = assetId;
    }

    public string Action { get; }
    public string? Reason { get; }
    public string? AssetId { get; }
    public bool IsDuplicate => string.Equals(Action, "reject", StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(Reason, "duplicate", StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrWhiteSpace(AssetId);

    public static ImmichDuplicateCheckResult Accepted()
    {
        return new ImmichDuplicateCheckResult("accept", null, null);
    }
}

public sealed class ImmichAssetUploadResult
{
    public ImmichAssetUploadResult(string assetId, string status)
    {
        AssetId = assetId;
        Status = status;
    }

    public string AssetId { get; }
    public string Status { get; }
}
