#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Optional interface for providers that can delete the remote object created by an upload
/// ("Delete from host" in History). Implement alongside IUploaderProvider.
/// </summary>
public interface IUploadRemover
{
    /// <summary>
    /// Deletes the object behind <paramref name="url"/>. <paramref name="uploadMetadata"/> holds the
    /// <c>UploadResult.Metadata</c> recorded when the upload finished (may be empty for older uploads).
    /// Returns false when the object could not be identified or deleted.
    /// </summary>
    Task<bool> DeleteUploadAsync(
        ExplorerContext context,
        string url,
        IReadOnlyDictionary<string, string?> uploadMetadata,
        CancellationToken cancellation = default);
}
