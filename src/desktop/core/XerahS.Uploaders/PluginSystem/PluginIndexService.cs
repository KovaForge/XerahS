#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Net.Http;
using System.Security.Cryptography;
using Newtonsoft.Json;
using XerahS.Common;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Fetches a community plugin registry and downloads verified .xsdp packages.
/// </summary>
public sealed class PluginIndexService
{
    public const string DefaultIndexUrl = "https://raw.githubusercontent.com/ShareX/XerahS/refs/heads/develop/plugins-index.json";

    private const long MaxIndexBytes = 2_000_000; // 2MB
    private const long MaxPackageBytes = 100_000_000; // Keep aligned with PluginPackager.

    private readonly HttpClient _httpClient;
    private readonly string _indexUrl;

    public PluginIndexService()
        : this(new HttpClient(), DefaultIndexUrl)
    {
    }

    public PluginIndexService(HttpClient httpClient, string indexUrl = DefaultIndexUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _indexUrl = indexUrl;
    }

    public async Task<CommunityPluginIndex> FetchIndexAsync(CancellationToken cancellationToken = default)
    {
        if (!IsHttpsUri(_indexUrl))
        {
            throw new InvalidOperationException("Plugin index URL must use HTTPS.");
        }

        using var response = await _httpClient.GetAsync(_indexUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxIndexBytes)
        {
            throw new InvalidDataException($"Plugin index exceeds maximum size of {MaxIndexBytes / 1_000_000}MB.");
        }

        string json = await ReadLimitedStringAsync(response.Content, MaxIndexBytes, cancellationToken).ConfigureAwait(false);
        return ParseIndex(json);
    }

    public static CommunityPluginIndex ParseIndex(string json)
    {
        var index = JsonConvert.DeserializeObject<CommunityPluginIndex>(json);
        if (index == null)
        {
            throw new InvalidDataException("Failed to deserialize plugin index.");
        }

        if (!index.IsValid(out var error))
        {
            throw new InvalidDataException($"Invalid plugin index: {error}");
        }

        return index;
    }

    public async Task<string> DownloadPackageAsync(CommunityPluginIndexEntry plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (!plugin.IsValid(out var error) || plugin.IsDraft)
        {
            throw new InvalidDataException($"Invalid plugin entry: {error ?? "Draft plugins cannot be downloaded."}");
        }

        using var response = await _httpClient.GetAsync(plugin.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxPackageBytes)
        {
            throw new InvalidDataException($"Plugin package exceeds maximum size of {MaxPackageBytes / 1_000_000}MB.");
        }

        string tempPath = Path.Combine(Path.GetTempPath(), $"{plugin.PluginId}-{Guid.NewGuid():N}.xsdp");

        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = File.Create(tempPath))
            {
                await CopyLimitedAsync(source, destination, MaxPackageBytes, cancellationToken).ConfigureAwait(false);
            }

            string actualChecksum = await ComputeSha256Async(tempPath, cancellationToken).ConfigureAwait(false);
            string expectedChecksum = NormalizeSha256Checksum(plugin.Checksum);

            if (!actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Downloaded plugin package checksum does not match the registry.");
            }

            return tempPath;
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to clean up plugin package download");
            }

            throw;
        }
    }

    public static bool IsValidSha256Checksum(string? checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum))
        {
            return false;
        }

        string normalized = NormalizeSha256Checksum(checksum);
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit);
    }

    private static string NormalizeSha256Checksum(string checksum)
    {
        string normalized = checksum.Trim();
        const string prefix = "sha256:";
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }

        return normalized;
    }

    private static bool IsHttpsUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadLimitedStringAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var memory = new MemoryStream();
        await CopyLimitedAsync(stream, memory, maxBytes, cancellationToken).ConfigureAwait(false);
        memory.Position = 0;
        using var reader = new StreamReader(memory);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            int bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > maxBytes)
            {
                throw new InvalidDataException($"Download exceeds maximum size of {maxBytes / 1_000_000}MB.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
