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

namespace XerahS.Core.Cloud;

public sealed class XerahSCloudOptions
{
    public Uri ApiBaseAddress { get; init; } = new("https://xerahs.com/");
    public Uri? OAuthAuthority { get; init; }
    public string? OAuthClientId { get; init; }
    public Uri OAuthRedirectUri { get; init; } = new("https://xerahs.com/auth/desktop/callback");
    public Uri OAuthCallbackUri { get; init; } = new("xerahs://oauth/callback");
    public bool FeatureEnabled { get; init; }

    public bool IsOAuthConfigured => FeatureEnabled &&
        OAuthAuthority != null &&
        !string.IsNullOrWhiteSpace(OAuthClientId);

    public static XerahSCloudOptions FromEnvironment()
    {
        string? apiBaseAddress = Environment.GetEnvironmentVariable("XERAHS_CLOUD_API_BASE_ADDRESS");
        string? authority = Environment.GetEnvironmentVariable("XERAHS_CLOUD_OAUTH_AUTHORITY");
        string? clientId = Environment.GetEnvironmentVariable("XERAHS_CLOUD_OAUTH_CLIENT_ID");
        string? redirectUri = Environment.GetEnvironmentVariable("XERAHS_CLOUD_OAUTH_REDIRECT_URI");
        bool enabled = bool.TryParse(
            Environment.GetEnvironmentVariable("XERAHS_CLOUD_DESKTOP_ENABLED"),
            out bool parsed) && parsed;
        Uri? parsedApiBaseAddress = TryCreateSecureUri(apiBaseAddress);
        Uri? parsedRedirectUri = TryCreateSecureUri(redirectUri);
        bool invalidEndpoint =
            (!string.IsNullOrWhiteSpace(apiBaseAddress) && parsedApiBaseAddress == null) ||
            (!string.IsNullOrWhiteSpace(redirectUri) && parsedRedirectUri == null);

        return new XerahSCloudOptions
        {
            ApiBaseAddress = parsedApiBaseAddress ?? new Uri("https://xerahs.com/"),
            OAuthAuthority = TryCreateSecureUri(authority),
            OAuthClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim(),
            OAuthRedirectUri = parsedRedirectUri ?? new Uri("https://xerahs.com/auth/desktop/callback"),
            FeatureEnabled = enabled && !invalidEndpoint
        };
    }

    internal static void RequireSecureHttpEndpoint(Uri endpoint, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(endpoint, parameterName);
        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new ArgumentException("XerahS Cloud endpoints must be absolute HTTPS URLs without credentials, query, or fragment.", parameterName);
        }
    }

    private static Uri? TryCreateSecureUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        try
        {
            RequireSecureHttpEndpoint(uri, nameof(value));
            return uri;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

public sealed record XerahSCloudPublishRequest(
    string ClientItemId,
    string Url,
    string? ThumbnailUrl,
    string Kind,
    string FileName,
    DateTimeOffset CapturedAt,
    string? Host,
    string? ContentType);

public sealed record XerahSCloudPublishResponse(
    string Id,
    string OwnerSubject,
    Uri? ProfileUrl,
    DateTimeOffset PublishedAt);

public enum XerahSCloudDeleteState
{
    Removed,
    AcceptedPending
}

public sealed record XerahSCloudDeleteResponse(XerahSCloudDeleteState State);

public interface IXerahSCloudClient
{
    bool IsConfigured { get; }
    bool HasSessionCredential { get; }
    string? CurrentOwnerSubject { get; }

    Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default);

    void SignOut();

    Task<XerahSCloudPublishResponse> PublishAsync(
        XerahSCloudPublishRequest request,
        CancellationToken cancellationToken = default);

    Task<XerahSCloudDeleteResponse> UnpublishAsync(
        string clientItemId,
        string expectedOwnerSubject,
        CancellationToken cancellationToken = default);
}

public class XerahSCloudException : Exception
{
    public XerahSCloudException(string message) : base(message)
    {
    }

    public XerahSCloudException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class XerahSCloudSecurityException : XerahSCloudException
{
    public XerahSCloudSecurityException(string message) : base(message)
    {
    }

    public XerahSCloudSecurityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
