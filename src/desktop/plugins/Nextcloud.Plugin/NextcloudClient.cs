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
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using XerahS.Common;
using XerahS.Uploaders;
using SysHttpMethod = System.Net.Http.HttpMethod;

namespace ShareX.Nextcloud.Plugin;

public sealed class NextcloudClient
{
    private const string OcsHeaderName = "OCS-APIRequest";
    private const string OcsHeaderValue = "true";
    private const int DefaultChunkSizeMiB = 10;
    private const int MinimumChunkSizeMiB = 5;
    private static HttpClient HttpClient => XerahS.Common.HttpClientFactory.Create(allowAutoRedirect: true, infiniteTimeout: true);

    private readonly string _serverUrl;
    private readonly string _loginName;
    private readonly string _appPassword;

    public NextcloudClient(string serverUrl, string loginName, string appPassword)
    {
        _serverUrl = NormalizeServerUrl(serverUrl);
        _loginName = loginName?.Trim() ?? string.Empty;
        _appPassword = appPassword?.Trim() ?? string.Empty;
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

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            value = uri.GetLeftPart(UriPartial.Path);
        }

        return value.TrimEnd('/');
    }

    public static string NormalizeRelativePath(string? path)
    {
        string value = path?.Trim().Replace('\\', '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join('/', value
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment != "." && segment != ".."));
    }

    public static string CombineRelativePath(string? folderPath, string? name)
    {
        string safeFolderPath = NormalizeRelativePath(folderPath);
        string safeName = NormalizeRelativePath(name);

        if (string.IsNullOrWhiteSpace(safeFolderPath))
        {
            return safeName;
        }

        if (string.IsNullOrWhiteSpace(safeName))
        {
            return safeFolderPath;
        }

        return URLHelpers.CombineURL(safeFolderPath, safeName).Trim('/');
    }

    public static async Task<NextcloudLoginFlowState> StartLoginFlowAsync(string serverUrl, CancellationToken cancellation = default)
    {
        string normalizedServerUrl = NormalizeServerUrl(serverUrl);
        if (string.IsNullOrWhiteSpace(normalizedServerUrl))
        {
            throw new InvalidOperationException("Nextcloud server URL is required.");
        }

        using HttpRequestMessage request = new(SysHttpMethod.Post, normalizedServerUrl + "/index.php/login/v2");
        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud Login Flow v2 start", response, body));
        }

        NextcloudLoginFlowEnvelope? envelope = JsonConvert.DeserializeObject<NextcloudLoginFlowEnvelope>(body);
        if (envelope == null ||
            string.IsNullOrWhiteSpace(envelope.Login) ||
            string.IsNullOrWhiteSpace(envelope.Poll?.Endpoint) ||
            string.IsNullOrWhiteSpace(envelope.Poll.Token))
        {
            throw new InvalidOperationException("Nextcloud Login Flow v2 returned an invalid response.");
        }

        return new NextcloudLoginFlowState
        {
            LoginUrl = envelope.Login,
            PollEndpoint = envelope.Poll.Endpoint,
            PollToken = envelope.Poll.Token
        };
    }

    public static async Task<NextcloudLoginResult?> PollLoginFlowAsync(
        string pollEndpoint,
        string pollToken,
        CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(pollEndpoint) || string.IsNullOrWhiteSpace(pollToken))
        {
            return null;
        }

        using HttpRequestMessage request = new(SysHttpMethod.Post, pollEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = pollToken
            })
        };

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);

        if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.NoContent or HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud Login Flow v2 poll", response, body));
        }

        NextcloudLoginFlowResult? result = JsonConvert.DeserializeObject<NextcloudLoginFlowResult>(body);
        if (result == null ||
            string.IsNullOrWhiteSpace(result.Server) ||
            string.IsNullOrWhiteSpace(result.LoginName) ||
            string.IsNullOrWhiteSpace(result.AppPassword))
        {
            return null;
        }

        return new NextcloudLoginResult
        {
            ServerUrl = NormalizeServerUrl(result.Server),
            LoginName = result.LoginName,
            AppPassword = result.AppPassword
        };
    }

    public async Task<NextcloudServerProfile> GetServerProfileAsync(CancellationToken cancellation = default)
    {
        EnsureCredentials();

        JObject capabilitiesPayload = await GetOcsJsonAsync("/ocs/v2.php/cloud/capabilities?format=json", cancellation);
        JObject userPayload = await GetOcsJsonAsync("/ocs/v1.php/cloud/user?format=json", cancellation);

        JToken? capabilitiesData = capabilitiesPayload["ocs"]?["data"];
        JToken? userData = userPayload["ocs"]?["data"];
        JToken? capabilities = capabilitiesData?["capabilities"];

        return new NextcloudServerProfile
        {
            ServerUrl = _serverUrl,
            LoginName = _loginName,
            UserId = userData?["id"]?.Value<string>() ?? _loginName,
            DisplayName = userData?["display-name"]?.Value<string>() ?? userData?["id"]?.Value<string>() ?? _loginName,
            ServerVersion = capabilitiesData?["version"]?["string"]?.Value<string>() ?? string.Empty,
            ServerProductName = capabilities?["theming"]?["name"]?.Value<string>() ?? "Nextcloud",
            ThemingName = capabilities?["theming"]?["name"]?.Value<string>() ?? string.Empty,
            SupportsPublicShares = GetBoolean(capabilities?["files_sharing"]?["public"]?["enabled"]),
            SupportsSharePasswords = GetBoolean(capabilities?["files_sharing"]?["public"]?["password"]?["enforced"]) ||
                                     capabilities?["files_sharing"]?["public"]?["password"] != null,
            SupportsExpireDate = GetBoolean(capabilities?["files_sharing"]?["public"]?["expire_date"]?["enabled"]),
            SupportsChunking = !string.IsNullOrWhiteSpace(capabilities?["dav"]?["chunking"]?.Value<string>()),
            SupportsSearch = capabilities?["files"]?["search"] != null || capabilities?["dav"]?["search"] != null
        };
    }

    public async Task UploadFileAsync(
        Stream stream,
        string userId,
        string relativeFolderPath,
        string fileName,
        bool useChunkedUpload,
        int chunkSizeMiB,
        Action<int>? reportProgress,
        CancellationToken cancellation = default)
    {
        EnsureCredentials();

        string safeUserId = ResolveUserId(userId);
        string safeFolderPath = NormalizeRelativePath(relativeFolderPath);
        string relativeFilePath = CombineRelativePath(safeFolderPath, fileName);
        string destinationUrl = BuildDavItemUrl(safeUserId, relativeFilePath);

        if (useChunkedUpload && stream.CanSeek && stream.Length >= ClampChunkSizeMiB(chunkSizeMiB) * 1024L * 1024L * 2L)
        {
            await UploadFileChunkedAsync(stream, safeUserId, safeFolderPath, destinationUrl, chunkSizeMiB, reportProgress, cancellation);
            return;
        }

        await EnsureFolderAsync(safeUserId, safeFolderPath, cancellation);

        using HttpRequestMessage request = CreateDavRequest(SysHttpMethod.Put, destinationUrl);
        request.Headers.TryAddWithoutValidation("X-NC-WebDAV-AutoMkcol", "1");
        request.Content = new ProgressStreamContent(stream, reportProgress);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(MimeTypes.GetMimeTypeFromFileName(fileName));

        using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud WebDAV upload", response, body));
        }
    }

    public async Task<NextcloudShareInfo?> CreatePublicShareAsync(
        string sharePath,
        bool autoExpireShare,
        int expireAfterDays,
        string? sharePassword,
        CancellationToken cancellation = default)
    {
        EnsureCredentials();

        Dictionary<string, string> formFields = new()
        {
            ["path"] = EnsureLeadingSlash(sharePath),
            ["shareType"] = "3",
            ["permissions"] = "1"
        };

        if (autoExpireShare && expireAfterDays > 0)
        {
            formFields["expireDate"] = DateTime.UtcNow
                .AddDays(expireAfterDays)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(sharePassword))
        {
            formFields["password"] = sharePassword.Trim();
        }

        using HttpRequestMessage request = CreateOcsRequest(SysHttpMethod.Post, "/ocs/v2.php/apps/files_sharing/api/v1/shares?format=json");
        request.Content = new FormUrlEncodedContent(formFields);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud OCS share creation", response, body));
        }

        JObject payload = ParseOcsPayload(body);
        JToken? meta = payload["ocs"]?["meta"];
        if (meta?["statuscode"]?.Value<int?>() != 100)
        {
            string message = meta?["message"]?.Value<string>() ?? "Unknown Nextcloud share creation error.";
            throw new InvalidOperationException(message);
        }

        JToken? data = payload["ocs"]?["data"];
        string? url = data?["url"]?.Value<string>();
        string? token = data?["token"]?.Value<string>();

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new NextcloudShareInfo
        {
            Url = url,
            Token = token ?? string.Empty
        };
    }

    public async Task<IReadOnlyList<NextcloudFileEntry>> ListFolderAsync(
        string userId,
        string relativeFolderPath,
        CancellationToken cancellation = default)
    {
        EnsureCredentials();

        string safeUserId = ResolveUserId(userId);
        string safeFolderPath = NormalizeRelativePath(relativeFolderPath);
        string folderUrl = string.IsNullOrWhiteSpace(safeFolderPath)
            ? BuildDavRootUrl(safeUserId)
            : BuildDavItemUrl(safeUserId, safeFolderPath);

        using HttpRequestMessage request = CreateDavRequest(new SysHttpMethod("PROPFIND"), folderUrl);
        request.Headers.TryAddWithoutValidation("Depth", "1");
        request.Content = new StringContent(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <d:propfind xmlns:d="DAV:">
              <d:prop>
                <d:displayname/>
                <d:getcontentlength/>
                <d:getcontenttype/>
                <d:getlastmodified/>
                <d:resourcetype/>
              </d:prop>
            </d:propfind>
            """,
            Encoding.UTF8,
            "application/xml");

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud WebDAV listing", response, body));
        }

        return ParsePropFindResponse(body, safeUserId, safeFolderPath);
    }

    public async Task<byte[]?> DownloadFileAsync(string userId, string relativePath, CancellationToken cancellation = default)
    {
        EnsureCredentials();

        string safeUserId = ResolveUserId(userId);
        string safeRelativePath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(safeRelativePath))
        {
            return null;
        }

        using HttpRequestMessage request = CreateDavRequest(SysHttpMethod.Get, BuildDavItemUrl(safeUserId, safeRelativePath));
        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellation);
    }

    public async Task<bool> DeleteFileAsync(string userId, string relativePath, CancellationToken cancellation = default)
    {
        EnsureCredentials();

        string safeUserId = ResolveUserId(userId);
        string safeRelativePath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(safeRelativePath))
        {
            return false;
        }

        using HttpRequestMessage request = CreateDavRequest(SysHttpMethod.Delete, BuildDavItemUrl(safeUserId, safeRelativePath));
        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<bool> CreateFolderAsync(string userId, string relativePath, CancellationToken cancellation = default)
    {
        EnsureCredentials();

        string safeUserId = ResolveUserId(userId);
        string safeRelativePath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(safeRelativePath))
        {
            return true;
        }

        await EnsureFolderAsync(safeUserId, safeRelativePath, cancellation);
        return true;
    }

    private async Task UploadFileChunkedAsync(
        Stream stream,
        string userId,
        string relativeFolderPath,
        string destinationUrl,
        int chunkSizeMiB,
        Action<int>? reportProgress,
        CancellationToken cancellation)
    {
        await EnsureFolderAsync(userId, relativeFolderPath, cancellation);

        string uploadId = Guid.NewGuid().ToString("N");
        string uploadFolderUrl = BuildUploadsFolderUrl(userId, uploadId);
        using (HttpRequestMessage mkcolRequest = CreateDavRequest(new SysHttpMethod("MKCOL"), uploadFolderUrl))
        using (HttpResponseMessage mkcolResponse = await HttpClient.SendAsync(mkcolRequest, cancellation))
        {
            string mkcolBody = await mkcolResponse.Content.ReadAsStringAsync(cancellation);
            if (!mkcolResponse.IsSuccessStatusCode && mkcolResponse.StatusCode != HttpStatusCode.MethodNotAllowed)
            {
                throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud chunked upload folder creation", mkcolResponse, mkcolBody));
            }
        }

        int safeChunkSize = ClampChunkSizeMiB(chunkSizeMiB) * 1024 * 1024;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        byte[] buffer = new byte[safeChunkSize];
        int chunkIndex = 0;

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, safeChunkSize), cancellation);
                if (read <= 0)
                {
                    break;
                }

                chunkIndex++;
                byte[] chunkBytes = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunkBytes, 0, read);

                using HttpRequestMessage chunkRequest = CreateDavRequest(SysHttpMethod.Put, uploadFolderUrl + "/" + chunkIndex.ToString("D10", CultureInfo.InvariantCulture));
                chunkRequest.Content = new ByteArrayContent(chunkBytes);
                chunkRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using HttpResponseMessage chunkResponse = await HttpClient.SendAsync(chunkRequest, cancellation);
                string chunkBody = await chunkResponse.Content.ReadAsStringAsync(cancellation);
                if (!chunkResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(BuildHttpErrorMessage($"Nextcloud chunk upload part {chunkIndex}", chunkResponse, chunkBody));
                }

                reportProgress?.Invoke(read);
            }

            using HttpRequestMessage moveRequest = CreateDavRequest(new SysHttpMethod("MOVE"), uploadFolderUrl + "/.file");
            moveRequest.Headers.TryAddWithoutValidation("Destination", destinationUrl);
            moveRequest.Headers.TryAddWithoutValidation("OC-Total-Length", stream.Length.ToString(CultureInfo.InvariantCulture));

            using HttpResponseMessage moveResponse = await HttpClient.SendAsync(moveRequest, cancellation);
            string moveBody = await moveResponse.Content.ReadAsStringAsync(cancellation);
            if (!moveResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud chunked upload finalization", moveResponse, moveBody));
            }
        }
        finally
        {
            using HttpRequestMessage cleanupRequest = CreateDavRequest(SysHttpMethod.Delete, uploadFolderUrl);
            using HttpResponseMessage _ = await HttpClient.SendAsync(cleanupRequest, cancellation);
        }
    }

    private async Task EnsureFolderAsync(string userId, string relativeFolderPath, CancellationToken cancellation)
    {
        string safeRelativeFolderPath = NormalizeRelativePath(relativeFolderPath);
        if (string.IsNullOrWhiteSpace(safeRelativeFolderPath))
        {
            return;
        }

        string current = string.Empty;
        foreach (string segment in safeRelativeFolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = CombineRelativePath(current, segment);
            using HttpRequestMessage request = CreateDavRequest(new SysHttpMethod("MKCOL"), BuildDavItemUrl(userId, current));
            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
            string body = await response.Content.ReadAsStringAsync(cancellation);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                continue;
            }

            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud folder creation", response, body));
        }
    }

    private async Task<JObject> GetOcsJsonAsync(string relativeUrl, CancellationToken cancellation)
    {
        using HttpRequestMessage request = CreateOcsRequest(SysHttpMethod.Get, relativeUrl);
        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage("Nextcloud OCS request", response, body));
        }

        return ParseOcsPayload(body);
    }

    private HttpRequestMessage CreateDavRequest(SysHttpMethod method, string absoluteUrl)
    {
        HttpRequestMessage request = new(method, absoluteUrl);
        request.Headers.Authorization = CreateAuthHeader();
        return request;
    }

    private HttpRequestMessage CreateOcsRequest(SysHttpMethod method, string relativeUrl)
    {
        HttpRequestMessage request = new(method, _serverUrl + relativeUrl);
        request.Headers.Authorization = CreateAuthHeader();
        request.Headers.TryAddWithoutValidation(OcsHeaderName, OcsHeaderValue);
        return request;
    }

    private AuthenticationHeaderValue CreateAuthHeader()
    {
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes(_loginName + ":" + _appPassword));
        return new AuthenticationHeaderValue("Basic", token);
    }

    private string BuildDavRootUrl(string userId)
    {
        return _serverUrl + "/remote.php/dav/files/" + Uri.EscapeDataString(userId);
    }

    private string BuildDavItemUrl(string userId, string relativePath)
    {
        string safeRelativePath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(safeRelativePath))
        {
            return BuildDavRootUrl(userId);
        }

        return BuildDavRootUrl(userId) + "/" + EncodePathSegments(safeRelativePath);
    }

    private string BuildUploadsFolderUrl(string userId, string uploadId)
    {
        return _serverUrl + "/remote.php/dav/uploads/" + Uri.EscapeDataString(userId) + "/" + Uri.EscapeDataString(uploadId);
    }

    private static string EncodePathSegments(string relativePath)
    {
        return string.Join("/", NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
    }

    private static IReadOnlyList<NextcloudFileEntry> ParsePropFindResponse(string xml, string userId, string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Array.Empty<NextcloudFileEntry>();
        }

        XDocument document = XDocument.Parse(xml);
        XNamespace dav = "DAV:";
        List<NextcloudFileEntry> results = new();
        string normalizedRequestedPath = NormalizeRelativePath(requestedPath);
        string hrefPrefix = "/remote.php/dav/files/" + userId + "/";

        foreach (XElement response in document.Descendants(dav + "response"))
        {
            string? href = response.Element(dav + "href")?.Value;
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            string relativePath = ExtractRelativePath(href, hrefPrefix, userId);
            if (relativePath == normalizedRequestedPath)
            {
                continue;
            }

            XElement? prop = GetSuccessfulProp(response, dav);
            if (prop == null || string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            bool isFolder = prop.Descendants(dav + "collection").Any();
            string name = Uri.UnescapeDataString(relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? relativePath);
            long size = long.TryParse(prop.Element(dav + "getcontentlength")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedSize)
                ? parsedSize
                : 0;

            DateTime? modified = DateTime.TryParse(
                prop.Element(dav + "getlastmodified")?.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal,
                out DateTime parsedDate)
                ? parsedDate
                : null;

            results.Add(new NextcloudFileEntry
            {
                Id = relativePath,
                Name = name,
                RelativePath = relativePath,
                IsFolder = isFolder,
                SizeBytes = isFolder ? 0 : size,
                MimeType = isFolder ? null : prop.Element(dav + "getcontenttype")?.Value,
                ModifiedAt = modified
            });
        }

        return results;
    }

    private static XElement? GetSuccessfulProp(XElement response, XNamespace dav)
    {
        foreach (XElement propstat in response.Elements(dav + "propstat"))
        {
            string status = propstat.Element(dav + "status")?.Value ?? string.Empty;
            if (status.Contains(" 200 ", StringComparison.Ordinal))
            {
                return propstat.Element(dav + "prop");
            }
        }

        return response.Descendants(dav + "prop").FirstOrDefault();
    }

    private static string ExtractRelativePath(string href, string hrefPrefix, string userId)
    {
        string rawHref = Uri.UnescapeDataString(href);
        if (Uri.TryCreate(rawHref, UriKind.Absolute, out Uri? absoluteUri))
        {
            rawHref = Uri.UnescapeDataString(absoluteUri.AbsolutePath);
        }

        if (TryExtractPathAfterPrefix(rawHref, hrefPrefix, out string relativePath))
        {
            return NormalizeRelativePath(relativePath);
        }

        string alternatePrefix = "/remote.php/dav/files/" + Uri.EscapeDataString(userId);
        if (TryExtractPathAfterPrefix(rawHref, alternatePrefix, out relativePath))
        {
            return NormalizeRelativePath(Uri.UnescapeDataString(relativePath));
        }

        return NormalizeRelativePath(rawHref.Trim('/'));
    }

    private static bool TryExtractPathAfterPrefix(string rawHref, string prefix, out string relativePath)
    {
        relativePath = string.Empty;

        if (string.IsNullOrWhiteSpace(rawHref) || string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        int prefixIndex = rawHref.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
        {
            return false;
        }

        int startIndex = prefixIndex + prefix.Length;
        if (startIndex < rawHref.Length && rawHref[startIndex] == '/')
        {
            startIndex++;
        }

        relativePath = startIndex <= rawHref.Length ? rawHref[startIndex..] : string.Empty;
        return true;
    }

    private static JObject ParseOcsPayload(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new JObject();
        }

        return JObject.Parse(body);
    }

    private static bool GetBoolean(JToken? token)
    {
        return token?.Type switch
        {
            JTokenType.Boolean => token.Value<bool>(),
            JTokenType.Integer => token.Value<int>() != 0,
            JTokenType.String => bool.TryParse(token.Value<string>(), out bool parsed) ? parsed : token.Value<string>() == "1",
            _ => false
        };
    }

    private static int ClampChunkSizeMiB(int chunkSizeMiB)
    {
        if (chunkSizeMiB <= 0)
        {
            return DefaultChunkSizeMiB;
        }

        return Math.Clamp(chunkSizeMiB, MinimumChunkSizeMiB, 512);
    }

    private string ResolveUserId(string? userId)
    {
        string safeUserId = userId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(safeUserId))
        {
            return safeUserId;
        }

        return _loginName;
    }

    private static string EnsureLeadingSlash(string path)
    {
        string safePath = NormalizeRelativePath(path);
        return "/" + safePath;
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_serverUrl))
        {
            throw new InvalidOperationException("Nextcloud server URL is required.");
        }

        if (string.IsNullOrWhiteSpace(_loginName) || string.IsNullOrWhiteSpace(_appPassword))
        {
            throw new InvalidOperationException("Nextcloud credentials are required.");
        }
    }

    private static string BuildHttpErrorMessage(string operation, HttpResponseMessage response, string body)
    {
        string baseMessage = $"{operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}.";
        if (string.IsNullOrWhiteSpace(body))
        {
            return baseMessage;
        }

        return baseMessage + " " + body.Trim();
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

    private sealed class NextcloudLoginFlowEnvelope
    {
        [JsonProperty("login")]
        public string Login { get; set; } = string.Empty;

        [JsonProperty("poll")]
        public NextcloudLoginFlowPoll? Poll { get; set; }
    }

    private sealed class NextcloudLoginFlowPoll
    {
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class NextcloudLoginFlowResult
    {
        [JsonProperty("server")]
        public string Server { get; set; } = string.Empty;

        [JsonProperty("loginName")]
        public string LoginName { get; set; } = string.Empty;

        [JsonProperty("appPassword")]
        public string AppPassword { get; set; } = string.Empty;
    }
}

public sealed class NextcloudLoginFlowState
{
    public string LoginUrl { get; set; } = string.Empty;
    public string PollEndpoint { get; set; } = string.Empty;
    public string PollToken { get; set; } = string.Empty;
}

public sealed class NextcloudLoginResult
{
    public string ServerUrl { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}

public sealed class NextcloudShareInfo
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public sealed class NextcloudFileEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public long SizeBytes { get; set; }
    public string? MimeType { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
