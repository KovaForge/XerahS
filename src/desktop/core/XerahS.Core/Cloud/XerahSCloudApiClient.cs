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
    private readonly HttpClient? _httpClient;
    private readonly IXerahSCloudSessionStore _sessionStore;
    private readonly IXerahSCloudOAuthTokenExchange _tokenExchange;
    private readonly XerahSCloudOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public XerahSCloudApiClient(
        HttpClient? httpClient,
        IXerahSCloudSessionStore sessionStore,
        IXerahSCloudOAuthTokenExchange tokenExchange,
        XerahSCloudOptions options)
    {
        _httpClient = httpClient;
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _tokenExchange = tokenExchange ?? throw new ArgumentNullException(nameof(tokenExchange));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        XerahSCloudOptions.RequireSecureHttpEndpoint(options.ApiBaseAddress, nameof(options.ApiBaseAddress));
    }

    private HttpClient Http => _httpClient ?? XerahS.Common.HttpClientFactory.Create();

    public bool IsConfigured => _options.IsOAuthConfigured;

    public bool HasSessionCredential
    {
        get
        {
            try
            {
                return _sessionStore.Current != null || _sessionStore.ReadRefreshCredential() != null;
            }
            catch (XerahSCloudSecurityException)
            {
                return false;
            }
        }
    }

    public string? CurrentOwnerSubject
    {
        get
        {
            try
            {
                return _sessionStore.Current?.OwnerSubject ?? _sessionStore.ReadRefreshCredential()?.OwnerSubject;
            }
            catch (XerahSCloudSecurityException)
            {
                return null;
            }
        }
    }

    public async Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (!HasSessionCredential)
        {
            return false;
        }

        await GetAccountAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<XerahSCloudAccountSummary> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        XerahSCloudSession session = await GetSessionAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        using HttpResponseMessage response = await SendWithRefreshRetryAsync(
            token => CreateRequest(HttpMethod.Get, "api/v1/me", token),
            session,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateResponseException("Account verification", response.StatusCode);
        }

        AccountEnvelope? account = await response.Content
            .ReadFromJsonAsync<AccountEnvelope>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (account == null ||
            string.IsNullOrWhiteSpace(account.Slug) ||
            account.Slug.Length > 30 ||
            account.Slug.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) ||
            !account.StrongAuth)
        {
            throw new XerahSCloudSecurityException("The XerahS Cloud account response did not pass security checks.");
        }

        Uri profileUrl = new(_options.ApiBaseAddress, $"{Uri.EscapeDataString(account.Slug)}/");
        Uri settingsUrl = new(_options.ApiBaseAddress, "settings");
        return new XerahSCloudAccountSummary(
            account.Slug,
            profileUrl,
            settingsUrl,
            account.TimeZone ?? "UTC",
            account.StrongAuth,
            account.TrialStatus ?? "unknown",
            account.TrialEndsAt,
            account.SubscriptionStatus,
            account.PaidThrough,
            account.CanPublish,
            account.DisputeSuspended);
    }

    public void SignOut() => _sessionStore.Clear();

    public async Task<XerahSCloudPublishResponse> PublishAsync(
        XerahSCloudPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();
        ValidateDestinationUrl(request.Url);

        object body = new
        {
            request.Url,
            request.ThumbnailUrl,
            request.Kind,
            request.FileName,
            request.CapturedAt,
            request.Host,
            request.ContentType
        };
        XerahSCloudSession session = await GetSessionAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        using HttpResponseMessage response = await SendWithRefreshRetryAsync(
            token =>
            {
                HttpRequestMessage message = CreateRequest(
                    HttpMethod.Put,
                    $"api/v1/items/{Uri.EscapeDataString(request.ClientItemId)}",
                    token);
                message.Headers.TryAddWithoutValidation("Idempotency-Key", request.ClientItemId);
                message.Content = JsonContent.Create(body, options: JsonOptions);
                return message;
            },
            session,
            cancellationToken).ConfigureAwait(false);
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
        XerahSCloudSession session = await GetSessionAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(session.OwnerSubject, expectedOwnerSubject, StringComparison.Ordinal))
        {
            throw new XerahSCloudSecurityException("The history item belongs to a different XerahS Cloud account.");
        }

        using HttpResponseMessage response = await SendWithRefreshRetryAsync(
            token =>
            {
                HttpRequestMessage message = CreateRequest(
                    HttpMethod.Delete,
                    $"api/v1/items/{Uri.EscapeDataString(clientItemId)}",
                    token);
                message.Headers.TryAddWithoutValidation("Idempotency-Key", $"unpublish:{clientItemId}");
                return message;
            },
            session,
            cancellationToken).ConfigureAwait(false);

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

    private async Task<HttpResponseMessage> SendWithRefreshRetryAsync(
        Func<string, HttpRequestMessage> requestFactory,
        XerahSCloudSession session,
        CancellationToken cancellationToken)
    {
        using (HttpRequestMessage request = requestFactory(session.AccessToken))
        {
            HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            response.Dispose();
        }

        XerahSCloudSession refreshed = await GetSessionAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage retry = requestFactory(refreshed.AccessToken);
        return await Http.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<XerahSCloudSession> GetSessionAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        XerahSCloudSession? current = _sessionStore.Current;
        if (!forceRefresh && current != null && current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return current;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = _sessionStore.Current;
            if (!forceRefresh && current != null && current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return current;
            }

            (string OwnerSubject, string RefreshToken)? credential = _sessionStore.ReadRefreshCredential();
            if (credential == null)
            {
                throw new XerahSCloudSecurityException("A current XerahS Cloud session is required.");
            }

            try
            {
                XerahSCloudSession refreshed = await _tokenExchange
                    .RefreshAsync(credential.Value.RefreshToken, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(refreshed.OwnerSubject, credential.Value.OwnerSubject, StringComparison.Ordinal))
                {
                    _sessionStore.Clear();
                    throw new XerahSCloudSecurityException("OAuth refresh attempted to switch XerahS Cloud accounts.");
                }

                _sessionStore.Accept(refreshed);
                return refreshed;
            }
            catch (XerahSCloudSecurityException)
            {
                _sessionStore.Clear();
                throw;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
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

    private sealed record AccountEnvelope(
        string Slug,
        string? TimeZone,
        bool StrongAuth,
        string? TrialStatus,
        DateTimeOffset? TrialEndsAt,
        string? SubscriptionStatus,
        DateTimeOffset? PaidThrough,
        bool CanPublish,
        bool DisputeSuspended);
}
