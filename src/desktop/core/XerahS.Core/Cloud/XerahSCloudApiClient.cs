#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace XerahS.Core.Cloud;

public sealed class XerahSCloudApiClient : IXerahSCloudClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IXerahSCloudSessionStore _sessionStore;
    private readonly XerahSCloudOptions _options;

    public XerahSCloudApiClient(
        HttpClient httpClient,
        IXerahSCloudSessionStore sessionStore,
        XerahSCloudOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        XerahSCloudOptions.RequireSecureHttpEndpoint(options.ApiBaseAddress, nameof(options.ApiBaseAddress));
    }

    public bool IsConfigured => _options.IsOAuthConfigured;

    public string? CurrentOwnerSubject => _sessionStore.Current?.OwnerSubject;

    public async Task<XerahSCloudPublishResponse> PublishAsync(
        XerahSCloudPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();
        XerahSCloudSession session = RequireSession();
        ValidateDestinationUrl(request.Url);

        using HttpRequestMessage message = CreateRequest(
            HttpMethod.Put,
            $"api/v1/items/{Uri.EscapeDataString(request.ClientItemId)}",
            session.AccessToken);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.ClientItemId);
        message.Content = JsonContent.Create(new
        {
            request.Url,
            request.ThumbnailUrl,
            request.Kind,
            request.FileName,
            request.CapturedAt,
            request.Host,
            request.ContentType
        }, options: JsonOptions);

        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateResponseException("Publish", response.StatusCode);
        }

        PublishEnvelope? envelope = await response.Content
            .ReadFromJsonAsync<PublishEnvelope>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (envelope?.Item == null || string.IsNullOrWhiteSpace(envelope.Item.Id))
        {
            throw new XerahSCloudException("Publish returned an invalid response.");
        }

        return new XerahSCloudPublishResponse(
            envelope.Item.Id,
            session.OwnerSubject,
            null,
            envelope.Item.PublishedAt);
    }

    public async Task<XerahSCloudDeleteResponse> UnpublishAsync(
        string clientItemId,
        string expectedOwnerSubject,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientItemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedOwnerSubject);
        EnsureConfigured();
        XerahSCloudSession session = RequireSession();

        if (!string.Equals(session.OwnerSubject, expectedOwnerSubject, StringComparison.Ordinal))
        {
            throw new XerahSCloudSecurityException("The history item belongs to a different XerahS Cloud account.");
        }

        using HttpRequestMessage message = CreateRequest(
            HttpMethod.Delete,
            $"api/v1/items/{Uri.EscapeDataString(clientItemId)}",
            session.AccessToken);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", $"unpublish:{clientItemId}");
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new XerahSCloudDeleteResponse(XerahSCloudDeleteState.AcceptedPending);
        }

        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.NotFound)
        {
            return new XerahSCloudDeleteResponse(XerahSCloudDeleteState.Removed);
        }

        throw CreateResponseException("Unpublish", response.StatusCode);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string accessToken)
    {
        Uri endpoint = new(_options.ApiBaseAddress, relativePath);
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private XerahSCloudSession RequireSession()
    {
        XerahSCloudSession? session = _sessionStore.Current;
        if (session == null || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new XerahSCloudSecurityException("A current XerahS Cloud session is required.");
        }

        return session;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new XerahSCloudException("XerahS Cloud desktop access is disabled until the OAuth launch gate is configured.");
        }
    }

    private static void ValidateDestinationUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new XerahSCloudSecurityException("Only credential-free HTTPS destination URLs can be published.");
        }
    }

    private static XerahSCloudException CreateResponseException(string operation, HttpStatusCode statusCode) =>
        new($"{operation} failed with HTTP {(int)statusCode} ({statusCode}).");

    private sealed record PublishEnvelope(PublishedItem Item);

    private sealed record PublishedItem(string Id, DateTimeOffset PublishedAt);
}
