#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or modify it
    under the terms of the GNU General Public License as published by the Free
    Software Foundation; either version 2 of the License, or (at your option)
    any later version.
*/

#endregion License Information (GPL v3)

using System.Security.Cryptography;
using System.Text;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.AmazonS3.Plugin;

internal static class S3CredentialSecrets
{
    private const string AccessKeyIdName = "accessKeyId";
    private const string SecretAccessKeyName = "secretAccessKey";

    public static bool TryGetAccessKeyCredentials(ISecretStore secrets, S3ConfigModel config,
        out string accessKeyId, out string secretAccessKey)
    {
        accessKeyId = string.Empty;
        secretAccessKey = string.Empty;

        if (!string.IsNullOrWhiteSpace(config.SecretKey) &&
            TryGetPair(secrets, config.SecretKey, out accessKeyId, out secretAccessKey))
        {
            StoreDestinationAlias(secrets, config, accessKeyId, secretAccessKey);
            return true;
        }

        string? destinationSecretKey = BuildDestinationSecretKey(config);
        if (destinationSecretKey == null ||
            !TryGetPair(secrets, destinationSecretKey, out accessKeyId, out secretAccessKey))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(config.SecretKey))
        {
            SetPair(secrets, config.SecretKey, accessKeyId, secretAccessKey);
        }

        return true;
    }

    public static void StoreAccessKeyCredentials(ISecretStore secrets, S3ConfigModel config,
        string accessKeyId, string secretAccessKey)
    {
        if (!string.IsNullOrWhiteSpace(config.SecretKey))
        {
            SetPair(secrets, config.SecretKey, accessKeyId, secretAccessKey);
        }

        StoreDestinationAlias(secrets, config, accessKeyId, secretAccessKey);
    }

    public static void DeleteAccessKeyCredentials(ISecretStore secrets, S3ConfigModel config)
    {
        if (!string.IsNullOrWhiteSpace(config.SecretKey))
        {
            DeletePair(secrets, config.SecretKey);
        }

        string? destinationSecretKey = BuildDestinationSecretKey(config);
        if (destinationSecretKey != null)
        {
            DeletePair(secrets, destinationSecretKey);
        }
    }

    private static bool TryGetPair(ISecretStore secrets, string secretKey,
        out string accessKeyId, out string secretAccessKey)
    {
        accessKeyId = secrets.GetSecret("amazons3", secretKey, AccessKeyIdName) ?? string.Empty;
        secretAccessKey = secrets.GetSecret("amazons3", secretKey, SecretAccessKeyName) ?? string.Empty;

        return !string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey);
    }

    private static void StoreDestinationAlias(ISecretStore secrets, S3ConfigModel config,
        string accessKeyId, string secretAccessKey)
    {
        string? destinationSecretKey = BuildDestinationSecretKey(config);
        if (destinationSecretKey != null)
        {
            SetPair(secrets, destinationSecretKey, accessKeyId, secretAccessKey);
        }
    }

    private static void SetPair(ISecretStore secrets, string secretKey,
        string accessKeyId, string secretAccessKey)
    {
        secrets.SetSecret("amazons3", secretKey, AccessKeyIdName, accessKeyId);
        secrets.SetSecret("amazons3", secretKey, SecretAccessKeyName, secretAccessKey);
    }

    private static void DeletePair(ISecretStore secrets, string secretKey)
    {
        secrets.DeleteSecret("amazons3", secretKey, AccessKeyIdName);
        secrets.DeleteSecret("amazons3", secretKey, SecretAccessKeyName);
    }

    private static string? BuildDestinationSecretKey(S3ConfigModel config)
    {
        string bucket = Normalize(config.BucketName);
        string customDomain = NormalizeDomain(config.CustomDomain);
        string endpoint = Normalize(config.Endpoint);
        string region = Normalize(config.Region);

        if (string.IsNullOrWhiteSpace(bucket) &&
            string.IsNullOrWhiteSpace(customDomain) &&
            string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        string material = string.Join("|", endpoint, region, bucket, customDomain);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return "destination:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
    }

    private static string NormalizeDomain(string? value)
    {
        string normalized = Normalize(value);
        if (normalized.StartsWith("https://", StringComparison.Ordinal))
        {
            normalized = normalized[8..];
        }
        else if (normalized.StartsWith("http://", StringComparison.Ordinal))
        {
            normalized = normalized[7..];
        }

        return normalized.TrimEnd('/');
    }
}
