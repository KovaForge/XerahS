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

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using XerahS.Common;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Media Browser write operations on a bucket (upload, create folder, rename, delete, probe),
/// done through the AWS SDK so large uploads go multipart and requests are signed like uploads.
/// Keys are absolute object keys; folders are key prefixes ending with "/".
/// </summary>
internal sealed class S3ExplorerOperations(IAmazonS3 client, S3ConfigModel config)
{
    private const int DeleteBatchSize = 1000;

    public async Task UploadAsync(string key, Stream content, CancellationToken cancellation)
    {
        var request = new TransferUtilityUploadRequest
        {
            BucketName = config.BucketName,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = MimeTypes.GetMimeTypeFromFileName(key),
            StorageClass = AmazonS3Uploader.MapStorageClass(config.StorageClass),
            DisablePayloadSigning = !config.SignedPayload,
        };

        if (config.SetPublicACL)
        {
            request.CannedACL = S3CannedACL.PublicRead;
        }

        using var transfer = new TransferUtility(client);
        await transfer.UploadAsync(request, cancellation);
    }

    /// <summary>S3 folders are zero-byte objects whose key ends with "/".</summary>
    public async Task CreateFolderAsync(string folderKey, CancellationToken cancellation)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = config.BucketName,
            Key = EnsureFolder(folderKey),
            InputStream = new MemoryStream([]),
            CannedACL = config.SetPublicACL ? S3CannedACL.PublicRead : null,
        }, cancellation);
    }

    public async Task RenameFileAsync(string sourceKey, string destinationKey, CancellationToken cancellation)
    {
        await CopyAsync(sourceKey, destinationKey, cancellation);
        try
        {
            await client.DeleteObjectAsync(config.BucketName, sourceKey, cancellation);
        }
        catch
        {
            // Keep exactly one copy: undo the copy if the original could not be removed.
            await client.DeleteObjectAsync(config.BucketName, destinationKey, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Copies every object under the source prefix, then removes the originals. A failed copy
    /// rolls back the objects already copied, so the folder is never left half-renamed.
    /// </summary>
    public async Task RenameFolderAsync(string sourcePrefix, string destinationPrefix, CancellationToken cancellation)
    {
        sourcePrefix = EnsureFolder(sourcePrefix);
        destinationPrefix = EnsureFolder(destinationPrefix);
        List<string> keys = await ListKeysAsync(sourcePrefix, int.MaxValue, cancellation);
        var copied = new List<string>();

        try
        {
            foreach (string key in keys)
            {
                cancellation.ThrowIfCancellationRequested();
                string destination = destinationPrefix + key[sourcePrefix.Length..];
                await CopyAsync(key, destination, cancellation);
                copied.Add(destination);
            }

            if (!keys.Contains(sourcePrefix))
            {
                // Folder that only existed implicitly: keep it visible under the new name.
                await CreateFolderAsync(destinationPrefix, cancellation);
            }
        }
        catch
        {
            await DeleteKeysAsync(copied, CancellationToken.None);
            throw;
        }

        await DeleteKeysAsync(keys, cancellation);
    }

    public Task DeleteFileAsync(string key, CancellationToken cancellation) =>
        client.DeleteObjectAsync(config.BucketName, key, cancellation);

    /// <summary>Deletes the folder marker and every object under the prefix.</summary>
    public async Task DeleteFolderAsync(string prefix, CancellationToken cancellation)
    {
        prefix = EnsureFolder(prefix);
        List<string> keys = await ListKeysAsync(prefix, int.MaxValue, cancellation);
        if (!keys.Contains(prefix))
        {
            keys.Add(prefix);
        }

        await DeleteKeysAsync(keys, cancellation);
    }

    public async Task<bool> HasChildrenAsync(string prefix, CancellationToken cancellation)
    {
        prefix = EnsureFolder(prefix);
        List<string> keys = await ListKeysAsync(prefix, 2, cancellation);
        return keys.Any(key => !key.Equals(prefix, StringComparison.Ordinal));
    }

    private async Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellation)
    {
        await client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = config.BucketName,
            SourceKey = sourceKey,
            DestinationBucket = config.BucketName,
            DestinationKey = destinationKey,
            CannedACL = config.SetPublicACL ? S3CannedACL.PublicRead : null,
            StorageClass = AmazonS3Uploader.MapStorageClass(config.StorageClass),
        }, cancellation);
    }

    private async Task<List<string>> ListKeysAsync(string prefix, int limit, CancellationToken cancellation)
    {
        var keys = new List<string>();
        var request = new ListObjectsV2Request
        {
            BucketName = config.BucketName,
            Prefix = prefix,
            MaxKeys = Math.Min(limit, 1000),
        };

        while (true)
        {
            ListObjectsV2Response response = await client.ListObjectsV2Async(request, cancellation);
            if (response.S3Objects != null)
            {
                keys.AddRange(response.S3Objects.Select(o => o.Key));
            }

            if (keys.Count >= limit || response.IsTruncated != true || string.IsNullOrEmpty(response.NextContinuationToken))
            {
                return keys;
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
    }

    private async Task DeleteKeysAsync(IReadOnlyList<string> keys, CancellationToken cancellation)
    {
        for (int offset = 0; offset < keys.Count; offset += DeleteBatchSize)
        {
            cancellation.ThrowIfCancellationRequested();
            var request = new DeleteObjectsRequest
            {
                BucketName = config.BucketName,
                Objects = keys.Skip(offset).Take(DeleteBatchSize).Select(key => new KeyVersion { Key = key }).ToList(),
                Quiet = true,
            };

            DeleteObjectsResponse response = await client.DeleteObjectsAsync(request, cancellation);
            if (response.DeleteErrors is { Count: > 0 } errors)
            {
                DeleteError first = errors[0];
                throw new InvalidOperationException($"Could not delete '{first.Key}': {first.Code} {first.Message}".Trim());
            }
        }
    }

    internal static string EnsureFolder(string key) =>
        string.IsNullOrEmpty(key) || key.EndsWith('/') ? key : key + "/";
}
