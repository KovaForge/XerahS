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

using XerahS.Common;

namespace XerahS.History;

/// <summary>
/// Owns the local cache keys used by XerahS Cloud. Keeping these mutations in one place
/// prevents History and future UI surfaces from inventing incompatible ownership rules.
/// </summary>
public static class HistoryPublishMetadata
{
    public const string ClientIdTag = "PublishedClientId";
    public const string PublishedAtTag = "Published";
    public const string ServerIdTag = "PublishedId";
    public const string OwnerSubjectTag = "PublishedOwnerId";

    public static bool IsPublishableMedia(HistoryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.URL))
        {
            return false;
        }

        string candidate = !string.IsNullOrWhiteSpace(item.FilePath) ? item.FilePath : item.FileName;
        return FileHelpers.IsImageFile(candidate) ||
            FileHelpers.IsVideoFile(candidate) ||
            item.Type.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            item.Type.Equals("Video", StringComparison.OrdinalIgnoreCase) ||
            item.Type.Equals("Screencast", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPublished(HistoryItem? item) =>
        TryGetTag(item, PublishedAtTag, out _) && TryGetTag(item, ServerIdTag, out _);

    public static bool CanPublish(HistoryItem? item) => IsPublishableMedia(item) && !IsPublished(item);

    public static bool CanUnpublish(HistoryItem? item, string? currentOwnerSubject = null)
    {
        if (!IsPublishableMedia(item) || !IsPublished(item))
        {
            return false;
        }

        string? boundOwner = GetOwnerSubject(item);
        return string.IsNullOrWhiteSpace(currentOwnerSubject) ||
            string.IsNullOrWhiteSpace(boundOwner) ||
            string.Equals(boundOwner, currentOwnerSubject, StringComparison.Ordinal);
    }

    public static string EnsureClientId(HistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureTags(item);

        if (TryGetTag(item, ClientIdTag, out string? existing) && Guid.TryParse(existing, out Guid parsed))
        {
            string normalized = parsed.ToString("D");
            item.Tags[ClientIdTag] = normalized;
            return normalized;
        }

        string clientId = Guid.NewGuid().ToString("D");
        item.Tags[ClientIdTag] = clientId;
        return clientId;
    }

    public static string? GetServerId(HistoryItem? item) =>
        TryGetTag(item, ServerIdTag, out string? value) ? value : null;

    public static string? GetOwnerSubject(HistoryItem? item) =>
        TryGetTag(item, OwnerSubjectTag, out string? value) ? value : null;

    public static void MarkPublished(
        HistoryItem item,
        string serverId,
        string ownerSubject,
        DateTimeOffset publishedAt)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerSubject);

        EnsureClientId(item);
        item.Tags[PublishedAtTag] = publishedAt.ToUniversalTime().ToString("O");
        item.Tags[ServerIdTag] = serverId;
        item.Tags[OwnerSubjectTag] = ownerSubject;
    }

    public static void MarkUnpublished(HistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureTags(item);
        item.Tags.Remove(PublishedAtTag);
        item.Tags.Remove(ServerIdTag);
        // Retain the stable client ID and its owner binding. A later account switch must
        // reconcile this item before it can silently adopt a different owner's identity.
    }

    public static string CreateTitle(HistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        string candidate = item.FileName;
        if (string.IsNullOrWhiteSpace(candidate) && Uri.TryCreate(item.URL, UriKind.Absolute, out Uri? uri))
        {
            candidate = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        }

        string title = Path.GetFileNameWithoutExtension(candidate)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("A publishable item must have a filename-derived title.");
        }

        return title;
    }

    private static bool TryGetTag(HistoryItem? item, string name, out string? value)
    {
        value = null;
        return item?.Tags != null &&
            item.Tags.TryGetValue(name, out value) &&
            !string.IsNullOrWhiteSpace(value);
    }

    private static void EnsureTags(HistoryItem item)
    {
        item.Tags ??= new Dictionary<string, string?>();
    }
}
