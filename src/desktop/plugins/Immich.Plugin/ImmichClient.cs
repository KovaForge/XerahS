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
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using XerahS.Common;

namespace ShareX.Immich.Plugin;

public sealed class ImmichClient
{
    private static readonly HttpClient HttpClient = HttpClientFactory.Create();

    private static readonly string[] ScopedApiKeyPermissions =
    {
        "apiKey.create",
        "album.create",
        "album.delete",
        "album.read",
        "albumAsset.create",
        "asset.delete",
        "asset.download",
        "asset.read",
        "asset.share",
        "asset.upload",
        "asset.view",
        "server.about",
        "sharedLink.create",
        "sharedLink.read",
        "user.read"
    };

    private readonly string _serverUrl;
    private readonly string _apiKey;

    public ImmichClient(string serverUrl, string apiKey)
    {
        _serverUrl = NormalizeServerUrl(serverUrl);
        _apiKey = apiKey?.Trim() ?? string.Empty;
    }

    public static string NormalizeServerUrl(string? serverUrl)
    {
        string value = serverUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return value.TrimEnd('/');
        }

        string path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        UriBuilder builder = new(uri)
        {
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    public async Task<ImmichServerProfile> GetServerProfileAsync(CancellationToken cancellation = default)
    {
        EnsureServerUrl();

        Task<ImmichServerConfigResponse?> configTask = RequestJsonAsync<ImmichServerConfigResponse>(HttpMethod.Get, "/server/config", authenticated: false, cancellation: cancellation);
        Task<ImmichServerFeaturesResponse?> featuresTask = RequestJsonAsync<ImmichServerFeaturesResponse>(HttpMethod.Get, "/server/features", authenticated: false, cancellation: cancellation);
        Task<ImmichServerVersionResponse?> versionTask = RequestJsonAsync<ImmichServerVersionResponse>(HttpMethod.Get, "/server/version", authenticated: false, cancellation: cancellation);

        await Task.WhenAll(configTask, featuresTask, versionTask);

        ImmichServerConfigResponse? config = await configTask;
        ImmichServerFeaturesResponse? features = await featuresTask;
        ImmichServerVersionResponse? version = await versionTask;

        ImmichServerProfile profile = new()
        {
            ServerUrl = _serverUrl,
            ServerVersion = BuildVersionString(version),
            ExternalDomain = config?.ExternalDomain ?? string.Empty,
            PasswordLoginEnabled = features?.PasswordLogin ?? true,
            OAuthEnabled = features?.OAuth ?? false,
            SearchEnabled = features?.Search ?? false,
            DuplicateDetectionEnabled = features?.DuplicateDetection ?? true,
            SidecarSupported = features?.Sidecar ?? false
        };

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return profile;
        }

        try
        {
            ImmichApiKeyResponse? apiKey = await RequestJsonAsync<ImmichApiKeyResponse>(HttpMethod.Get, "/api-keys/me", cancellation: cancellation);
            profile.ApiKeyName = apiKey?.Name ?? string.Empty;
        }
        catch
        {
        }

        try
        {
            ImmichUserResponse? user = await RequestJsonAsync<ImmichUserResponse>(HttpMethod.Get, "/users/me", cancellation: cancellation);
            profile.UserId = user?.Id ?? string.Empty;
            profile.UserName = user?.Name ?? string.Empty;
            profile.UserEmail = user?.Email ?? string.Empty;
        }
        catch
        {
        }

        try
        {
            ImmichServerAboutResponse? about = await RequestJsonAsync<ImmichServerAboutResponse>(HttpMethod.Get, "/server/about", cancellation: cancellation);
            if (!string.IsNullOrWhiteSpace(about?.Version))
            {
                profile.ServerVersion = about.Version;
            }
        }
        catch
        {
        }

        return profile;
    }

    public async Task<string> CreateScopedApiKeyAsync(string email, string password, string apiKeyName, CancellationToken cancellation = default)
    {
        EnsureServerUrl();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Immich email and password are required to create a scoped API key.");
        }

        ImmichLoginResponse login = await RequestJsonAsync<ImmichLoginResponse>(
            HttpMethod.Post,
            "/auth/login",
            new
            {
                email = email.Trim(),
                password
            },
            bearerToken: null,
            authenticated: false,
            cancellation: cancellation) ?? throw new InvalidOperationException("Immich login did not return an access token.");

        ImmichApiKeyCreateResponse response = await RequestJsonAsync<ImmichApiKeyCreateResponse>(
            HttpMethod.Post,
            "/api-keys",
            new
            {
                name = string.IsNullOrWhiteSpace(apiKeyName) ? "XerahS Uploads" : apiKeyName.Trim(),
                permissions = ScopedApiKeyPermissions
            },
            login.AccessToken,
            authenticated: false,
            cancellation: cancellation) ?? throw new InvalidOperationException("Immich did not return a newly created API key.");

        if (string.IsNullOrWhiteSpace(response.Secret))
        {
            throw new InvalidOperationException("Immich returned an empty API key secret.");
        }

        return response.Secret;
    }

    public async Task<IReadOnlyList<ImmichAlbum>> GetAlbumsAsync(CancellationToken cancellation = default)
    {
        IReadOnlyList<ImmichAlbumResponse> response = await RequestJsonArrayAsync<ImmichAlbumResponse>(HttpMethod.Get, "/albums", cancellation);
        return response.Select(MapAlbum).ToList();
    }

    public async Task<ImmichAlbum> GetAlbumAsync(string albumId, bool includeAssets, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            throw new InvalidOperationException("Immich album ID is required.");
        }

        string path = $"/albums/{Uri.EscapeDataString(albumId)}?withoutAssets={(includeAssets ? "false" : "true")}";
        ImmichAlbumResponse album = await RequestJsonAsync<ImmichAlbumResponse>(HttpMethod.Get, path, cancellation: cancellation)
            ?? throw new InvalidOperationException("Immich album response was empty.");
        return MapAlbum(album);
    }

    public async Task<ImmichAlbum> CreateAlbumAsync(string albumName, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(albumName))
        {
            throw new InvalidOperationException("Immich album name is required.");
        }

        ImmichAlbumResponse album = await RequestJsonAsync<ImmichAlbumResponse>(
            HttpMethod.Post,
            "/albums",
            new
            {
                albumName = albumName.Trim()
            },
            cancellation: cancellation) ?? throw new InvalidOperationException("Immich album creation failed.");

        return MapAlbum(album);
    }

    public async Task<bool> DeleteAlbumAsync(string albumId, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            return false;
        }

        await SendAsync(HttpMethod.Delete, $"/albums/{Uri.EscapeDataString(albumId)}", cancellation: cancellation);
        return true;
    }

    public async Task AddAssetsToAlbumAsync(string albumId, IReadOnlyCollection<string> assetIds, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(albumId) || assetIds.Count == 0)
        {
            return;
        }

        await RequestJsonArrayAsync<ImmichBulkIdResponse>(
            HttpMethod.Put,
            $"/albums/{Uri.EscapeDataString(albumId)}/assets",
            cancellation,
            new
            {
                ids = assetIds.ToArray()
            });
    }

    public async Task<ImmichDuplicateCheckResult> CheckDuplicateAsync(string checksum, string requestId, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(checksum))
        {
            return ImmichDuplicateCheckResult.Accepted();
        }

        ImmichBulkUploadCheckResponse response = await RequestJsonAsync<ImmichBulkUploadCheckResponse>(
            HttpMethod.Post,
            "/assets/bulk-upload-check",
            new
            {
                assets = new[]
                {
                    new
                    {
                        id = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId,
                        checksum
                    }
                }
            },
            cancellation: cancellation) ?? new ImmichBulkUploadCheckResponse();

        ImmichBulkUploadCheckItemResponse? item = response.Results.FirstOrDefault();
        return item == null
            ? ImmichDuplicateCheckResult.Accepted()
            : new ImmichDuplicateCheckResult(item.Action, item.Reason, item.AssetId);
    }

    public async Task<ImmichAssetUploadResult> UploadAssetAsync(
        Stream stream,
        string fileName,
        string checksum,
        DateTimeOffset createdAt,
        DateTimeOffset modifiedAt,
        Action<int>? reportProgress = null,
        CancellationToken cancellation = default)
    {
        EnsureApiKey();

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        MultipartFormDataContent form = new();
        string safeFileName = Path.GetFileName(fileName);
        string deviceAssetId = $"{safeFileName}-{checksum[..Math.Min(12, checksum.Length)]}";

        form.Add(new StringContent(deviceAssetId), "deviceAssetId");
        form.Add(new StringContent("XerahS"), "deviceId");
        form.Add(new StringContent(createdAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)), "fileCreatedAt");
        form.Add(new StringContent(modifiedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)), "fileModifiedAt");
        form.Add(new StringContent(safeFileName), "filename");

        ProgressStreamContent content = new(stream, reportProgress);
        content.Headers.ContentType = new MediaTypeHeaderValue(MimeTypes.GetMimeTypeFromFileName(safeFileName));
        form.Add(content, "assetData", safeFileName);

        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/assets");
        request.Content = form;
        request.Headers.TryAddWithoutValidation("x-immich-checksum", checksum);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);
        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Immich asset upload", response, body));
        }

        ImmichAssetUploadResponse? payload = string.IsNullOrWhiteSpace(body)
            ? null
            : JsonConvert.DeserializeObject<ImmichAssetUploadResponse>(body);

        if (payload == null || string.IsNullOrWhiteSpace(payload.Id))
        {
            throw new InvalidOperationException("Immich asset upload did not return an asset ID.");
        }

        return new ImmichAssetUploadResult(payload.Id, payload.Status ?? string.Empty);
    }

    public async Task<IReadOnlyList<ImmichSharedLink>> GetSharedLinksAsync(string? albumId = null, CancellationToken cancellation = default)
    {
        string path = string.IsNullOrWhiteSpace(albumId)
            ? "/shared-links"
            : "/shared-links?albumId=" + Uri.EscapeDataString(albumId);

        IReadOnlyList<ImmichSharedLinkResponse> response = await RequestJsonArrayAsync<ImmichSharedLinkResponse>(HttpMethod.Get, path, cancellation);
        return response.Select(MapSharedLink).ToList();
    }

    public async Task<ImmichSharedLink> CreateSharedLinkAsync(
        ImmichShareMode shareMode,
        IReadOnlyCollection<string>? assetIds,
        string? albumId,
        string? slug,
        string? password,
        bool useExpiry,
        int expireAfterDays,
        bool allowDownload,
        bool allowUpload,
        bool showMetadata,
        CancellationToken cancellation = default)
    {
        object payload = shareMode == ImmichShareMode.Album
            ? new
            {
                type = "ALBUM",
                albumId,
                slug = NormalizeOptionalString(slug),
                password = NormalizeOptionalString(password),
                expiresAt = useExpiry && expireAfterDays > 0
                    ? DateTime.UtcNow.AddDays(expireAfterDays).ToString("O", CultureInfo.InvariantCulture)
                    : null,
                allowDownload,
                allowUpload,
                showMetadata
            }
            : new
            {
                type = "INDIVIDUAL",
                assetIds = assetIds?.ToArray(),
                slug = NormalizeOptionalString(slug),
                password = NormalizeOptionalString(password),
                expiresAt = useExpiry && expireAfterDays > 0
                    ? DateTime.UtcNow.AddDays(expireAfterDays).ToString("O", CultureInfo.InvariantCulture)
                    : null,
                allowDownload,
                allowUpload,
                showMetadata
            };

        ImmichSharedLinkResponse response = await RequestJsonAsync<ImmichSharedLinkResponse>(
            HttpMethod.Post,
            "/shared-links",
            payload,
            cancellation: cancellation) ?? throw new InvalidOperationException("Immich did not return a shared link.");

        return MapSharedLink(response);
    }

    public async Task<byte[]?> DownloadAssetAsync(string assetId, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return null;
        }

        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/assets/{Uri.EscapeDataString(assetId)}/original", cancellation: cancellation);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellation);
    }

    public async Task<byte[]?> DownloadThumbnailAsync(string assetId, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return null;
        }

        string size = maxWidthPx > 320 ? "preview" : "thumbnail";
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/assets/{Uri.EscapeDataString(assetId)}/thumbnail?size={size}", cancellation: cancellation);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellation);
    }

    public async Task<bool> DeleteAssetAsync(string assetId, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return false;
        }

        await SendAsync(
            HttpMethod.Delete,
            "/assets",
            new
            {
                ids = new[] { assetId },
                force = false
            },
            cancellation);

        return true;
    }

    public string BuildSharedLinkUrl(ImmichSharedLink sharedLink, string? externalDomain)
    {
        string baseUrl = !string.IsNullOrWhiteSpace(externalDomain)
            ? NormalizeServerUrl(externalDomain)
            : _serverUrl;

        if (!string.IsNullOrWhiteSpace(sharedLink.Slug))
        {
            return baseUrl + "/s/" + Uri.EscapeDataString(sharedLink.Slug);
        }

        if (!string.IsNullOrWhiteSpace(sharedLink.Key))
        {
            return baseUrl + "/share/" + Uri.EscapeDataString(sharedLink.Key);
        }

        return string.Empty;
    }

    private async Task<IReadOnlyList<T>> RequestJsonArrayAsync<T>(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellation,
        object? payload = null)
    {
        using HttpResponseMessage response = await SendAsync(method, relativePath, payload, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);
        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<T>();
        }

        return JsonConvert.DeserializeObject<List<T>>(body) ?? new List<T>();
    }

    private async Task<T?> RequestJsonAsync<T>(
        HttpMethod method,
        string relativePath,
        object? payload = null,
        string? bearerToken = null,
        bool authenticated = true,
        CancellationToken cancellation = default)
    {
        using HttpResponseMessage response = await SendAsync(method, relativePath, payload, bearerToken, authenticated, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonConvert.DeserializeObject<T>(body);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? payload = null,
        CancellationToken cancellation = default)
    {
        return await SendAsync(method, relativePath, payload, null, true, cancellation);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? payload,
        string? bearerToken,
        bool authenticated,
        CancellationToken cancellation)
    {
        using HttpRequestMessage request = CreateRequest(method, relativePath, bearerToken, authenticated);
        if (payload != null)
        {
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        string body = await response.Content.ReadAsStringAsync(cancellation);
        string errorMessage = BuildHttpErrorMessage("Immich API request", response, body);
        response.Dispose();
        throw new InvalidOperationException(errorMessage);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string? bearerToken = null, bool authenticated = true)
    {
        EnsureServerUrl();

        HttpRequestMessage request = new(method, BuildApiUrl(relativePath));

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        else if (authenticated)
        {
            EnsureApiKey();
            request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        }

        return request;
    }

    private string BuildApiUrl(string relativePath)
    {
        string path = relativePath.StartsWith("/", StringComparison.Ordinal) ? relativePath : "/" + relativePath;
        return _serverUrl + "/api" + path;
    }

    private static string? NormalizeOptionalString(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? null! : trimmed;
    }

    private static string BuildVersionString(ImmichServerVersionResponse? version)
    {
        return version == null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Patch}";
    }

    private static ImmichAlbum MapAlbum(ImmichAlbumResponse response)
    {
        string? sharedLinkUrl = response.SharedLinks?.FirstOrDefault() is ImmichSharedLinkResponse link
            ? (!string.IsNullOrWhiteSpace(link.Slug)
                ? "/s/" + Uri.EscapeDataString(link.Slug)
                : !string.IsNullOrWhiteSpace(link.Key)
                    ? "/share/" + Uri.EscapeDataString(link.Key)
                    : null)
            : null;

        return new ImmichAlbum
        {
            Id = response.Id ?? string.Empty,
            AlbumName = response.AlbumName ?? string.Empty,
            ThumbnailAssetId = response.AlbumThumbnailAssetId,
            AssetCount = response.AssetCount,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
            Shared = response.Shared,
            HasSharedLink = response.HasSharedLink,
            SharedLinkUrl = sharedLinkUrl,
            Assets = response.Assets?.Select(MapAsset).ToList() ?? new List<ImmichAsset>()
        };
    }

    private static ImmichAsset MapAsset(ImmichAssetResponse response)
    {
        return new ImmichAsset
        {
            Id = response.Id ?? string.Empty,
            FileName = response.OriginalFileName ?? string.Empty,
            MimeType = response.OriginalMimeType,
            CreatedAt = response.FileCreatedAt == default ? response.CreatedAt : response.FileCreatedAt,
            ModifiedAt = response.UpdatedAt == default ? response.CreatedAt : response.UpdatedAt,
            SizeBytes = 0
        };
    }

    private static ImmichSharedLink MapSharedLink(ImmichSharedLinkResponse response)
    {
        return new ImmichSharedLink
        {
            Id = response.Id ?? string.Empty,
            Key = response.Key ?? string.Empty,
            Slug = response.Slug ?? string.Empty,
            AlbumId = response.Album?.Id ?? string.Empty,
            CreatedAt = response.CreatedAt,
            ExpiresAt = response.ExpiresAt
        };
    }

    private void EnsureServerUrl()
    {
        if (string.IsNullOrWhiteSpace(_serverUrl))
        {
            throw new InvalidOperationException("Immich server URL is required.");
        }
    }

    private void EnsureApiKey()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Immich API key is required.");
        }
    }

    private static string BuildHttpErrorMessage(string operation, HttpResponseMessage response, string body)
    {
        string baseMessage = $"{operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}.";
        return string.IsNullOrWhiteSpace(body) ? baseMessage : baseMessage + " " + body.Trim();
    }

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly Action<int>? _reportProgress;

        public ProgressStreamContent(Stream source, Action<int>? reportProgress)
        {
            _source = source;
            _reportProgress = reportProgress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            byte[] buffer = new byte[81920];

            if (_source.CanSeek)
            {
                _source.Position = 0;
            }

            while (true)
            {
                int read = await _source.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read <= 0)
                {
                    break;
                }

                await stream.WriteAsync(buffer.AsMemory(0, read));
                _reportProgress?.Invoke(read);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            if (_source.CanSeek)
            {
                length = _source.Length;
                return true;
            }

            length = 0;
            return false;
        }
    }
}
