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
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Immich.Plugin;

public sealed class ImmichUploader : FileUploader, IUploadHandler
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
        return UploadAsync(new UploadRequest
        {
            Content = stream,
            FileName = fileName,
            Category = UploaderCategory.Image
        }, CancellationToken.None).GetAwaiter().GetResult().ToUploadResult();
    }

    public async Task<UploadOutcome> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ServerUrl))
        {
            return UploadOutcome.Failed("Immich server URL is required.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return UploadOutcome.Failed("Immich API key is required.");
        }

        MemoryStream? ownedCopy = null;
        try
        {
            Stream workingStream = request.Content;
            if (!request.Content.CanSeek)
            {
                ownedCopy = new MemoryStream();
                await request.Content.CopyToAsync(ownedCopy, cancellationToken).ConfigureAwait(false);
                ownedCopy.Position = 0;
                workingStream = ownedCopy;
            }

            if (workingStream.CanSeek)
            {
                workingStream.Position = 0;
            }

            string checksum = ComputeSha1Hex(workingStream);
            DateTimeOffset createdAt = ResolveCreatedAt(request.Content);
            DateTimeOffset modifiedAt = ResolveModifiedAt(request.Content);

            if (workingStream.CanSeek)
            {
                workingStream.Position = 0;
            }

            ImmichClient client = new(_config.ServerUrl, _apiKey);
            string assetId = await UploadAssetWithDuplicateCheckAsync(
                client, workingStream, request.FileName, checksum, createdAt, modifiedAt, request.Progress, cancellationToken).ConfigureAwait(false);

            string? albumId = null;
            if (_config.AddToAlbum)
            {
                albumId = await ResolveTargetAlbumIdAsync(client, assetId, cancellationToken).ConfigureAwait(false);
            }

            if (_config.ShareMode == ImmichShareMode.None)
            {
                return UploadOutcome.Success(url: null, "Immich upload completed.", urlExpected: false);
            }

            ImmichSharedLink sharedLink = _config.ShareMode == ImmichShareMode.Album
                ? await CreateOrReuseAlbumShareAsync(client, albumId, cancellationToken).ConfigureAwait(false)
                : await client.CreateSharedLinkAsync(
                    _config.ShareMode,
                    new[] { assetId },
                    null,
                    _config.ShareSlug,
                    _sharePassword,
                    _config.UseShareExpiry,
                    _config.ExpireAfterDays,
                    _config.AllowShareDownload,
                    _config.AllowShareUpload,
                    _config.ShowMetadata,
                    cancellationToken).ConfigureAwait(false);

            string? url = client.BuildSharedLinkUrl(sharedLink, _config.ExternalDomain);
            if (string.IsNullOrWhiteSpace(url))
            {
                return UploadOutcome.Failed("Immich upload succeeded but no shared link URL was returned.");
            }

            return UploadOutcome.Success(url, "Immich upload completed.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UploadOutcome.Failed(ex.Message);
        }
        finally
        {
            ownedCopy?.Dispose();
        }
    }

    private async Task<string> UploadAssetWithDuplicateCheckAsync(
        ImmichClient client,
        Stream stream,
        string fileName,
        string checksum,
        DateTimeOffset createdAt,
        DateTimeOffset modifiedAt,
        IProgress<UploadProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (_config.UseDuplicateCheck && _config.DuplicateDetectionEnabled)
        {
            string requestId = $"{Path.GetFileName(fileName)}-{checksum[..Math.Min(12, checksum.Length)]}";
            ImmichDuplicateCheckResult duplicate = await client.CheckDuplicateAsync(checksum, requestId, cancellationToken).ConfigureAwait(false);
            if (duplicate.IsDuplicate && !string.IsNullOrWhiteSpace(duplicate.AssetId))
            {
                progress?.Report(new UploadProgressReport(stream.CanSeek ? stream.Length : 0, stream.CanSeek ? stream.Length : null));
                return duplicate.AssetId!;
            }
        }

        ImmichAssetUploadResult upload = await client.UploadAssetAsync(
            stream,
            fileName,
            checksum,
            createdAt,
            modifiedAt,
            bytesTransferred => progress?.Report(new UploadProgressReport(bytesTransferred, stream.CanSeek ? stream.Length : null)),
            cancellationToken).ConfigureAwait(false);

        return upload.AssetId;
    }

    private async Task<string> ResolveTargetAlbumIdAsync(ImmichClient client, string assetId, CancellationToken cancellationToken)
    {
        string? albumId = !string.IsNullOrWhiteSpace(_config.AlbumId) ? _config.AlbumId.Trim() : null;
        string parsedAlbumName = string.IsNullOrWhiteSpace(_config.AlbumName)
            ? string.Empty
            : NameParser.Parse(NameParserType.Default, _config.AlbumName.Trim());

        if (string.IsNullOrWhiteSpace(albumId) && !string.IsNullOrWhiteSpace(parsedAlbumName))
        {
            IReadOnlyList<ImmichAlbum> albums = await client.GetAlbumsAsync(cancellationToken).ConfigureAwait(false);
            albumId = albums.FirstOrDefault(album => string.Equals(album.AlbumName, parsedAlbumName, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        if (string.IsNullOrWhiteSpace(albumId))
        {
            if (!_config.AutoCreateAlbum || string.IsNullOrWhiteSpace(parsedAlbumName))
            {
                throw new InvalidOperationException("Immich album selection is required when album mode is enabled.");
            }

            albumId = (await client.CreateAlbumAsync(parsedAlbumName, cancellationToken).ConfigureAwait(false)).Id;
        }

        await client.AddAssetsToAlbumAsync(albumId, new[] { assetId }, cancellationToken).ConfigureAwait(false);
        return albumId;
    }

    private async Task<ImmichSharedLink> CreateOrReuseAlbumShareAsync(ImmichClient client, string? albumId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            throw new InvalidOperationException("Album sharing requires an Immich album destination.");
        }

        ImmichSharedLink? existing = (await client.GetSharedLinksAsync(albumId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(link => string.Equals(link.AlbumId, albumId, StringComparison.OrdinalIgnoreCase));

        if (existing != null && SecurityMatches(existing))
        {
            return existing;
        }

        return await client.CreateSharedLinkAsync(
            ImmichShareMode.Album,
            null,
            albumId,
            _config.ShareSlug,
            _sharePassword,
            _config.UseShareExpiry,
            _config.ExpireAfterDays,
            _config.AllowShareDownload,
            _config.AllowShareUpload,
            _config.ShowMetadata,
            cancellationToken).ConfigureAwait(false);
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
