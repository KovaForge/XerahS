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

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Builds ListObjectsV2 prefixes and IAM-aware Media Explorer errors.
/// </summary>
internal static class S3ExplorerListHelper
{
    public static string ResolveListPrefix(string? objectPrefix, string? folderPath)
    {
        string root = EnsureTrailingSlash(ExtractStaticPrefix(objectPrefix));
        string folder = NormalizePrefix(folderPath);
        if (string.IsNullOrEmpty(folder))
        {
            return root;
        }

        return root + EnsureTrailingSlash(folder);
    }

    public static string ExtractStaticPrefix(string? objectPrefix)
    {
        if (string.IsNullOrWhiteSpace(objectPrefix))
        {
            return string.Empty;
        }

        string value = objectPrefix.Replace('\\', '/').Trim().Trim('/');
        int tokenIndex = value.IndexOf('%');
        if (tokenIndex < 0)
        {
            return value;
        }

        string staticPart = value[..tokenIndex];
        int lastSeparator = staticPart.LastIndexOf('/');
        return lastSeparator < 0 ? string.Empty : staticPart[..lastSeparator].Trim('/');
    }

    public static string GetExplorerPath(string objectKey, string? objectPrefix)
    {
        string normalizedKey = objectKey.Replace('\\', '/').TrimStart('/');
        string root = EnsureTrailingSlash(ExtractStaticPrefix(objectPrefix));
        return !string.IsNullOrEmpty(root) && normalizedKey.StartsWith(root, StringComparison.Ordinal)
            ? normalizedKey[root.Length..]
            : normalizedKey;
    }

    public static bool IsListBucketDenied(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("s3:ListBucket", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Access Denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("S3 request failed: 403", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildListBucketDeniedMessage(string? bucketName, string serviceMessage)
    {
        string bucket = string.IsNullOrWhiteSpace(bucketName) ? "YOUR_BUCKET" : bucketName.Trim();
        return
            "Amazon S3 Media Explorer cannot list this bucket because the service denied the ListObjectsV2 request. " +
            "Existing object upload, download, or delete permissions may still work while browsing requires a bucket-level list permission. " +
            $"For AWS S3, allow s3:ListBucket on arn:aws:s3:::{bucket}; for compatible services, grant the equivalent bucket-list permission. " +
            "If access is prefix-scoped, allow the configured static prefix with the service's prefix condition (s3:prefix on AWS). " +
            "The service said: " + serviceMessage;
    }

    private static string NormalizePrefix(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').Trim().Trim('/');
    }

    private static string EnsureTrailingSlash(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        return prefix.EndsWith('/') ? prefix : prefix + "/";
    }
}
