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
using System.Net;
using System.Net.Http.Headers;
using XerahS.Common;

namespace ShareX.XBackBone.Plugin;

public sealed class XBackBoneClient
{
    private const int MaximumResponseLength = 8192;
    private const int MaximumServerMessageLength = 512;
    private readonly string _serverUrl;
    private readonly string _apiToken;
    private readonly HttpClient? _httpClient;

    public XBackBoneClient(string serverUrl, string apiToken)
        : this(serverUrl, apiToken, httpClient: null)
    {
    }

    internal XBackBoneClient(string serverUrl, string apiToken, HttpMessageHandler handler)
        : this(serverUrl, apiToken, new HttpClient(handler, disposeHandler: true))
    {
    }

    private XBackBoneClient(string serverUrl, string apiToken, HttpClient? httpClient)
    {
        _serverUrl = NormalizeServerUrl(serverUrl);
        _apiToken = apiToken ?? string.Empty;
        _httpClient = httpClient;
    }

    private HttpClient Http => _httpClient ?? HttpClientFactory.Create(allowAutoRedirect: true, infiniteTimeout: true);

    public static string NormalizeServerUrl(string? serverUrl)
    {
        string value = serverUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return value.TrimEnd('/');
        }

        string path = uri.AbsolutePath.TrimEnd('/');
        UriBuilder builder = new(uri)
        {
            Path = string.IsNullOrEmpty(path) ? "/" : path,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    public async Task<XBackBoneUploadResponse> UploadAsync(
        Stream stream,
        string fileName,
        XBackBoneApiGeneration apiGeneration,
        Action<int>? reportProgress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        EnsureConfigured(apiGeneration);

        string safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "upload";
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using MultipartFormDataContent form = new();
        ProgressStreamContent fileContent = new(stream, reportProgress);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeTypes.GetMimeTypeFromFileName(safeFileName));

        using HttpRequestMessage request = new(HttpMethod.Post, BuildUploadUrl(apiGeneration));
        if (apiGeneration == XBackBoneApiGeneration.Stable3)
        {
            form.Add(fileContent, "upload", safeFileName);
            form.Add(new StringContent(_apiToken), "token");
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            form.Add(fileContent, "file", safeFileName);
            form.Add(new StringContent(safeFileName), "name");
        }

        request.Content = form;
        using HttpResponseMessage response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellation);

        string body = await ReadBodyBoundedAsync(response.Content, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage(response, body));
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("XBackBone returned malformed JSON after the upload.");
        }

        string? canonicalUrl;
        string? rawUrl;
        string? deletionUrl = null;

        if (apiGeneration == XBackBoneApiGeneration.Stable3)
        {
            canonicalUrl = payload.Value<string>("url");
            rawUrl = payload.Value<string>("raw_url");
        }
        else
        {
            JObject? data = payload["data"] as JObject;
            canonicalUrl = data?.Value<string>("preview_ext_url");
            rawUrl = data?.Value<string>("raw_url");
            deletionUrl = data?.Value<string>("deletion_url");
        }

        if (!TryGetAbsoluteHttpUrl(canonicalUrl, out string? validatedCanonicalUrl))
        {
            throw new InvalidOperationException("XBackBone upload succeeded but the response did not contain a valid canonical URL.");
        }

        return new XBackBoneUploadResponse(
            validatedCanonicalUrl!,
            GetOptionalAbsoluteHttpUrl(rawUrl),
            GetOptionalAbsoluteHttpUrl(deletionUrl));
    }

    internal string BuildUploadUrl(XBackBoneApiGeneration apiGeneration)
    {
        string relativePath = apiGeneration switch
        {
            XBackBoneApiGeneration.Stable3 => "/upload",
            XBackBoneApiGeneration.ApiV1 => "/api/v1/upload",
            _ => throw new InvalidOperationException("The selected XBackBone API generation is not supported.")
        };

        return _serverUrl + relativePath;
    }

    private void EnsureConfigured(XBackBoneApiGeneration apiGeneration)
    {
        if (!Uri.TryCreate(_serverUrl, UriKind.Absolute, out Uri? uri) || !IsHttpScheme(uri))
        {
            throw new InvalidOperationException("XBackBone instance URL must be a valid http:// or https:// URL.");
        }

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            throw new InvalidOperationException("XBackBone API token is required.");
        }

        if (!Enum.IsDefined(apiGeneration))
        {
            throw new InvalidOperationException("The selected XBackBone API generation is not supported.");
        }
    }

    private string BuildHttpErrorMessage(HttpResponseMessage response, string body)
    {
        string baseMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"XBackBone authentication or permission failed (HTTP {(int)response.StatusCode}).",
            HttpStatusCode.RequestEntityTooLarge =>
                "XBackBone rejected the upload because it is too large (HTTP 413).",
            HttpStatusCode.UnprocessableEntity =>
                "XBackBone rejected the upload data (HTTP 422).",
            _ => $"XBackBone upload failed (HTTP {(int)response.StatusCode})."
        };

        string? serverMessage = TryReadServerMessage(body);
        return string.IsNullOrWhiteSpace(serverMessage)
            ? baseMessage
            : baseMessage + " " + RedactToken(serverMessage);
    }

    private static string? TryReadServerMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            string? message = JObject.Parse(body).Value<string>("message")?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            string singleLine = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return singleLine.Length <= MaximumServerMessageLength
                ? singleLine
                : singleLine[..MaximumServerMessageLength] + "...";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string RedactToken(string message)
    {
        return string.IsNullOrEmpty(_apiToken)
            ? message
            : message.Replace(_apiToken, "[redacted]", StringComparison.Ordinal);
    }

    private static async Task<string> ReadBodyBoundedAsync(HttpContent content, CancellationToken cancellation)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellation);
        using StreamReader reader = new(stream);
        char[] buffer = new char[MaximumResponseLength + 1];
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellation);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return new string(buffer, 0, Math.Min(totalRead, MaximumResponseLength));
    }

    private static string? GetOptionalAbsoluteHttpUrl(string? value)
    {
        return TryGetAbsoluteHttpUrl(value, out string? url) ? url : null;
    }

    private static bool TryGetAbsoluteHttpUrl(string? value, out string? url)
    {
        url = value?.Trim();
        return !string.IsNullOrWhiteSpace(url) &&
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            IsHttpScheme(uri);
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
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

public sealed class XBackBoneUploadResponse
{
    public XBackBoneUploadResponse(string canonicalUrl, string? rawUrl, string? deletionUrl)
    {
        CanonicalUrl = canonicalUrl;
        RawUrl = rawUrl;
        DeletionUrl = deletionUrl;
    }

    public string CanonicalUrl { get; }
    public string? RawUrl { get; }
    public string? DeletionUrl { get; }
}
