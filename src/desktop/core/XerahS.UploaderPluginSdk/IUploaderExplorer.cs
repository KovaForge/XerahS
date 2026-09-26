#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion

namespace XerahS.Uploaders.PluginSystem;

/// <summary>What a provider's remote storage supports in the Media Browser.</summary>
[Flags]
public enum ExplorerCapabilities
{
    None = 0,
    Download = 1,
    Upload = 2,
    Rename = 4,
    Delete = 8,
    Url = 16,
    CreateFolder = 32,
    Thumbnails = 64,
}

/// <summary>
/// The uploader instance a Media Browser operation runs against. Providers are shared across
/// instances, so every mutating call carries the instance settings explicitly.
/// </summary>
public sealed class ExplorerContext
{
    public string? SettingsJson { get; init; }
    public string? InstanceId { get; init; }
}

/// <summary>
/// Optional interface for providers that support browsing remote files (Media Browser).
/// Implement alongside IUploaderProvider. Members added after v1.1 have defaults, so older
/// plugins keep working and simply advertise fewer <see cref="BrowserCapabilities"/>.
/// </summary>
public interface IUploaderExplorer
{
    bool SupportsFolders { get; }
    Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default);
    Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default);
    Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default);
    Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default);
    Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default);

    /// <summary>
    /// Operations the browser may offer. The default matches the v1.1 contract (download, delete,
    /// URLs, thumbnails and, for folder-aware providers, folder creation).
    /// </summary>
    ExplorerCapabilities BrowserCapabilities =>
        ExplorerCapabilities.Download | ExplorerCapabilities.Delete | ExplorerCapabilities.Url | ExplorerCapabilities.Thumbnails |
        (SupportsFolders ? ExplorerCapabilities.CreateFolder : ExplorerCapabilities.None);

    /// <summary>Uploads <paramref name="content"/> as <paramref name="fileName"/> into <paramref name="folderPath"/>.</summary>
    Task<bool> UploadAsync(ExplorerContext context, string folderPath, string fileName, Stream content, CancellationToken cancellation = default) =>
        Task.FromResult(false);

    /// <summary>Renames a file or folder in place.</summary>
    Task<bool> RenameAsync(ExplorerContext context, MediaItem item, string newName, CancellationToken cancellation = default) =>
        Task.FromResult(false);

    /// <summary>Creates a folder using the instance settings.</summary>
    Task<bool> CreateFolderAsync(ExplorerContext context, string parentPath, string folderName, CancellationToken cancellation = default) =>
        CreateFolderAsync(parentPath, folderName, cancellation);

    /// <summary>Deletes a file, or a folder and everything in it.</summary>
    Task<bool> DeleteAsync(ExplorerContext context, MediaItem item, CancellationToken cancellation = default) =>
        DeleteAsync(item, cancellation);

    /// <summary>Whether a folder has contents (to warn before deleting). Null when unknown.</summary>
    Task<bool?> HasChildrenAsync(ExplorerContext context, MediaItem folder, CancellationToken cancellation = default) =>
        Task.FromResult<bool?>(null);
}
