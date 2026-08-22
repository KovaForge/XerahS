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

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XerahS.Core.Cloud;

public sealed record XerahSCloudOAuthAttempt(
    Uri AuthorizationUri,
    string State,
    string Nonce,
    string CodeVerifier,
    DateTimeOffset ExpiresAt);

public sealed record XerahSCloudOAuthCallback(string Code, string State);

public enum XerahSCloudOAuthCompletion
{
    Accepted,
    InvalidCallback,
    UnknownOrReplayedState,
    Expired,
    TokenRejected
}

public interface IXerahSCloudOAuthCoordinator
{
    XerahSCloudOAuthAttempt Begin();
    Task<XerahSCloudOAuthCompletion> CompleteAsync(Uri callbackUri, CancellationToken cancellationToken = default);
}

public interface IXerahSCloudOAuthTokenExchange
{
    Task<XerahSCloudSession> ExchangeAsync(
        string code,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken);
}

public interface IXerahSCloudTokenValidator
{
    XerahSCloudSession Validate(
        string accessToken,
        string refreshToken,
        int expiresInSeconds,
        string expectedNonce,
        XerahSCloudOptions options);
}

public interface IXerahSCloudClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemXerahSCloudClock : IXerahSCloudClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class XerahSCloudOAuthCallbackParser
{
    public static bool TryParse(Uri? uri, out XerahSCloudOAuthCallback? callback)
    {
        callback = null;
        if (uri == null ||
            !uri.IsAbsoluteUri ||
            !uri.Scheme.Equals("xerahs", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("oauth", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Equals("/callback", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.UserInfo.Length != 0)
        {
            return false;
        }

        Dictionary<string, string> values = ParseQuery(uri.Query);
        if (values.Keys.Any(IsForbiddenTokenParameter) ||
            values.Count != 2 ||
            !values.TryGetValue("code", out string? code) ||
            !values.TryGetValue("state", out string? state) ||
            string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        callback = new XerahSCloudOAuthCallback(code, state);
        return true;
    }

    public static bool IsCallbackArgument(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme.Equals("xerahs", StringComparison.OrdinalIgnoreCase) &&
        uri.Host.Equals("oauth", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string component in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = component.Split('=', 2);
            string key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
            string value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty;
            if (!values.TryAdd(key, value))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        return values;
    }

    private static bool IsForbiddenTokenParameter(string name) =>
        name.Equals("access_token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("refresh_token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("id_token", StringComparison.OrdinalIgnoreCase);
}

public static class XerahSCloudArgumentRedactor
{
    public const string RedactedCallback = "xerahs://oauth/callback?[REDACTED]";

    public static string[] Redact(IEnumerable<string>? arguments) =>
        arguments?.Select(argument => XerahSCloudOAuthCallbackParser.IsCallbackArgument(argument)
            ? RedactedCallback
            : argument).ToArray() ?? [];
}

public sealed class XerahSCloudOAuthCoordinator : IXerahSCloudOAuthCoordinator
{
    private static readonly TimeSpan AttemptLifetime = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, XerahSCloudOAuthAttempt> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _consumed = new(StringComparer.Ordinal);
    private readonly XerahSCloudOptions _options;
    private readonly IXerahSCloudOAuthTokenExchange _tokenExchange;
    private readonly IXerahSCloudSessionStore _sessionStore;
    private readonly IXerahSCloudClock _clock;

    public XerahSCloudOAuthCoordinator(
        XerahSCloudOptions options,
        IXerahSCloudOAuthTokenExchange tokenExchange,
        IXerahSCloudSessionStore sessionStore,
        IXerahSCloudClock clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenExchange = tokenExchange ?? throw new ArgumentNullException(nameof(tokenExchange));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public XerahSCloudOAuthAttempt Begin()
    {
        if (!_options.IsOAuthConfigured || _options.OAuthAuthority == null || string.IsNullOrWhiteSpace(_options.OAuthClientId))
        {
            throw new XerahSCloudException("XerahS Cloud OAuth is disabled or incomplete.");
        }

        XerahSCloudOptions.RequireSecureHttpEndpoint(_options.OAuthAuthority, nameof(_options.OAuthAuthority));
        string state = CreateRandomValue(32);
        string nonce = CreateRandomValue(32);
        string verifier = CreateRandomValue(64);
        string challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        DateTimeOffset expiresAt = _clock.UtcNow.Add(AttemptLifetime);

        Uri authorizationEndpoint = new(_options.OAuthAuthority, "/auth/v1/oauth/authorize");
        string query = string.Join('&', new Dictionary<string, string>
        {
            ["client_id"] = _options.OAuthClientId,
            ["redirect_uri"] = _options.OAuthRedirectUri.AbsoluteUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        var attempt = new XerahSCloudOAuthAttempt(
            new UriBuilder(authorizationEndpoint) { Query = query }.Uri,
            state,
            nonce,
            verifier,
            expiresAt);
        _pending[state] = attempt;
        return attempt;
    }

    public async Task<XerahSCloudOAuthCompletion> CompleteAsync(
        Uri callbackUri,
        CancellationToken cancellationToken = default)
    {
        if (!XerahSCloudOAuthCallbackParser.TryParse(callbackUri, out XerahSCloudOAuthCallback? callback) || callback == null)
        {
            return XerahSCloudOAuthCompletion.InvalidCallback;
        }

        if (_consumed.ContainsKey(callback.State) || !_pending.TryRemove(callback.State, out XerahSCloudOAuthAttempt? attempt))
        {
            return XerahSCloudOAuthCompletion.UnknownOrReplayedState;
        }

        _consumed.TryAdd(callback.State, 0);
        if (attempt.ExpiresAt <= _clock.UtcNow)
        {
            return XerahSCloudOAuthCompletion.Expired;
        }

        try
        {
            XerahSCloudSession session = await _tokenExchange
                .ExchangeAsync(callback.Code, attempt.CodeVerifier, attempt.Nonce, cancellationToken)
                .ConfigureAwait(false);
            _sessionStore.Accept(session);
            return XerahSCloudOAuthCompletion.Accepted;
        }
        catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException or JsonException)
        {
            return XerahSCloudOAuthCompletion.TokenRejected;
        }
    }

    private static string CreateRandomValue(int byteCount) => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class XerahSCloudOAuthTokenExchange : IXerahSCloudOAuthTokenExchange
{
    private readonly HttpClient _httpClient;
    private readonly XerahSCloudOptions _options;
    private readonly IXerahSCloudTokenValidator _tokenValidator;

    public XerahSCloudOAuthTokenExchange(
        HttpClient httpClient,
        XerahSCloudOptions options,
        IXerahSCloudTokenValidator tokenValidator)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
    }

    public async Task<XerahSCloudSession> ExchangeAsync(
        string code,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        if (_options.OAuthAuthority == null || string.IsNullOrWhiteSpace(_options.OAuthClientId))
        {
            throw new XerahSCloudException("XerahS Cloud OAuth is not configured.");
        }

        Uri tokenEndpoint = new(_options.OAuthAuthority, "/auth/v1/oauth/token");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = _options.OAuthClientId,
                ["redirect_uri"] = _options.OAuthRedirectUri.AbsoluteUri,
                ["code_verifier"] = codeVerifier
            })
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new XerahSCloudSecurityException($"OAuth token exchange failed with HTTP {(int)response.StatusCode}.");
        }

        OAuthTokenResponse? token = await response.Content
            .ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new XerahSCloudSecurityException("OAuth token exchange returned an invalid response.");
        }

        return _tokenValidator.Validate(
            token.AccessToken,
            token.RefreshToken,
            token.ExpiresIn,
            expectedNonce,
            _options);
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}

/// <summary>
/// Production fails closed until the deployed Supabase issuer/JWKS and tested AAL/AMR claim
/// contract are supplied. Tests and a later launch-gate implementation inject a strict validator.
/// </summary>
public sealed class LaunchGatedXerahSCloudTokenValidator : IXerahSCloudTokenValidator
{
    public XerahSCloudSession Validate(
        string accessToken,
        string refreshToken,
        int expiresInSeconds,
        string expectedNonce,
        XerahSCloudOptions options) =>
        throw new XerahSCloudSecurityException(
            "Desktop OAuth token acceptance is launch-gated until issuer, audience, nonce, session, and strong-auth claims are verified against production JWKS.");
}
