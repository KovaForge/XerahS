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

using System.Security.Cryptography;
using XerahS.Common;
using XerahS.Uploaders;

namespace ShareX.Immich.Plugin;

public sealed class ImmichUploader : FileUploader
{
    private readonly ImmichConfigModel _config;
    private readonly string _apiKey;
    private readonly string _sharePassword;

    public ImmichUploader(ImmichConfigModel config, string apiKey, string sharePassword)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _apiKey = apiKey ?? string.Empty;
        _sharePassword = sharePassword ?? string.Empty;
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        UploadResult result = new();

        if (string.IsNullOrWhiteSpace(_config.ServerUrl))
        {
            Errors.Add("Immich server URL is required.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Errors.Add("Immich API key is required.");
            return result;
        }

        try
        {
            MemoryStream? ownedCopy = null;
            try
            {
                Stream workingStream = stream;
                if (!stream.CanSeek)
                {
                    ownedCopy = new MemoryStream();
                    stream.CopyTo(ownedCopy);
                    ownedCopy.Position = 0;
                    workingStream = ownedCopy;
                }

                if (workingStream.CanSeek)
                {
                    workingStream.Position = 0;
                }

                string checksum = ComputeSha1Hex(workingStream);
                DateTimeOffset createdAt = ResolveCreatedAt(stream);
                DateTimeOffset modifiedAt = ResolveModifiedAt(stream);

                if (workingStream.CanSeek)
                {
                    workingStream.Position = 0;
                }

                ProgressManager progress = new(workingStream.Length);
                ImmichClient client = new(_config.ServerUrl, _apiKey);

                string assetId = UploadAssetWithDuplicateCheck(client, workingStream, fileName, checksum, createdAt, modifiedAt, progress);

                string? albumId = null;
                if (_config.AddToAlbum)
                {
                    albumId = ResolveTargetAlbumId(client, assetId);
                }

                result.IsSuccess = true;
                result.Response = "Immich upload completed.";

                if (_config.ShareMode == ImmichShareMode.None)
                {
                    result.IsURLExpected = false;
                    return result;
                }

                ImmichSharedLink sharedLink = _config.ShareMode == ImmichShareMode.Album
                    ? CreateOrReuseAlbumShare(client, albumId)
                    : client.CreateSharedLinkAsync(
                        _config.ShareMode,
                        new[] { assetId },
                        null,
                        _config.ShareSlug,
                        _sharePassword,
                        _config.UseShareExpiry,
                        _config.ExpireAfterDays,
                        _config.AllowShareDownload,
                        _config.AllowShareUpload,
                        _config.ShowMetadata).GetAwaiter().GetResult();

                result.URL = client.BuildSharedLinkUrl(sharedLink, _config.ExternalDomain);
                result.IsSuccess = !string.IsNullOrWhiteSpace(result.URL);

                if (string.IsNullOrWhiteSpace(result.URL))
                {
                    Errors.Add("Immich upload succeeded but no shared link URL was returned.");
                }

                return result;
            }
            finally
            {
                ownedCopy?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            return result;
        }
    }

    private string UploadAssetWithDuplicateCheck(
        ImmichClient client,
        Stream stream,
        string fileName,
        string checksum,
        DateTimeOffset createdAt,
        DateTimeOffset modifiedAt,
        ProgressManager progress)
    {
        if (_config.UseDuplicateCheck && _config.DuplicateDetectionEnabled)
        {
            string requestId = $"{Path.GetFileName(fileName)}-{checksum[..Math.Min(12, checksum.Length)]}";
            ImmichDuplicateCheckResult duplicate = client.CheckDuplicateAsync(checksum, requestId).GetAwaiter().GetResult();
            if (duplicate.IsDuplicate && !string.IsNullOrWhiteSpace(duplicate.AssetId))
            {
                if (AllowReportProgress && progress.UpdateProgress(stream.Length))
                {
                    OnProgressChanged(progress);
                }

                return duplicate.AssetId!;
            }
        }

        ImmichAssetUploadResult upload = client.UploadAssetAsync(
            stream,
            fileName,
            checksum,
            createdAt,
            modifiedAt,
            bytesTransferred =>
            {
                if (AllowReportProgress && progress.UpdateProgress(bytesTransferred))
                {
                    OnProgressChanged(progress);
                }
            }).GetAwaiter().GetResult();

        return upload.AssetId;
    }

    private string ResolveTargetAlbumId(ImmichClient client, string assetId)
    {
        string? albumId = !string.IsNullOrWhiteSpace(_config.AlbumId) ? _config.AlbumId.Trim() : null;
        string parsedAlbumName = string.IsNullOrWhiteSpace(_config.AlbumName)
            ? string.Empty
            : NameParser.Parse(NameParserType.Default, _config.AlbumName.Trim());

        if (string.IsNullOrWhiteSpace(albumId) && !string.IsNullOrWhiteSpace(parsedAlbumName))
        {
            IReadOnlyList<ImmichAlbum> albums = client.GetAlbumsAsync().GetAwaiter().GetResult();
            albumId = albums.FirstOrDefault(album => string.Equals(album.AlbumName, parsedAlbumName, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        if (string.IsNullOrWhiteSpace(albumId))
        {
            if (!_config.AutoCreateAlbum || string.IsNullOrWhiteSpace(parsedAlbumName))
            {
                throw new InvalidOperationException("Immich album selection is required when album mode is enabled.");
            }

            albumId = client.CreateAlbumAsync(parsedAlbumName).GetAwaiter().GetResult().Id;
        }

        client.AddAssetsToAlbumAsync(albumId, new[] { assetId }).GetAwaiter().GetResult();
        return albumId;
    }

    private ImmichSharedLink CreateOrReuseAlbumShare(ImmichClient client, string? albumId)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            throw new InvalidOperationException("Album sharing requires an Immich album destination.");
        }

        ImmichSharedLink? existing = client.GetSharedLinksAsync(albumId).GetAwaiter().GetResult()
            .FirstOrDefault(link => string.Equals(link.AlbumId, albumId, StringComparison.OrdinalIgnoreCase));

        if (existing != null && SecurityMatches(existing))
        {
            return existing;
        }

        return client.CreateSharedLinkAsync(
            ImmichShareMode.Album,
            null,
            albumId,
            _config.ShareSlug,
            _sharePassword,
            _config.UseShareExpiry,
            _config.ExpireAfterDays,
            _config.AllowShareDownload,
            _config.AllowShareUpload,
            _config.ShowMetadata).GetAwaiter().GetResult();
    }

    internal bool SecurityMatches(ImmichSharedLink link)
    {
        // Slug must match the current configured value.
        if (!string.Equals(link.Slug ?? string.Empty, _config.ShareSlug ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Password: if link is password-protected, it must match _sharePassword.
        // If link is not protected but we have a password configured, they differ.
        if (string.IsNullOrEmpty(_sharePassword))
        {
            if (!string.IsNullOrEmpty(link.Password))
            {
                return false;
            }
        }
        else
        {
            if (!string.Equals(link.Password, _sharePassword, StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Expiry: configured expiry must be active when link has expiry, and vice versa.
        if (_config.UseShareExpiry && _config.ExpireAfterDays > 0)
        {
            if (!link.ExpiresAt.HasValue)
            {
                return false;
            }
        }
        else
        {
            if (link.ExpiresAt.HasValue)
            {
                return false;
            }
        }

        // Download/upload/metadata flags must match.
        if (link.AllowDownload != _config.AllowShareDownload)
        {
            return false;
        }

        if (link.AllowUpload != _config.AllowShareUpload)
        {
            return false;
        }

        if (link.ShowMetadata != _config.ShowMetadata)
        {
            return false;
        }

        return true;
    }

    private static string ComputeSha1Hex(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        byte[] hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static DateTimeOffset ResolveCreatedAt(Stream stream)
    {
        if (stream is FileStream fileStream)
        {
            try
            {
                return File.GetCreationTimeUtc(fileStream.Name);
            }
            catch
            {
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset ResolveModifiedAt(Stream stream)
    {
        if (stream is FileStream fileStream)
        {
            try
            {
                return File.GetLastWriteTimeUtc(fileStream.Name);
            }
            catch
            {
            }
        }

        return DateTimeOffset.UtcNow;
    }
}
