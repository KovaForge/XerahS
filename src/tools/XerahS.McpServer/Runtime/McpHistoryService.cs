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

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.History;
using XerahS.Platform.Abstractions;

namespace XerahS.McpServer.Runtime;

internal sealed class McpHistoryService
{
    internal const long MaxInlineBlobBytes = 5 * 1024 * 1024;

    private readonly Func<string> _historyPathResolver;

    public McpHistoryService()
        : this(SettingsManager.GetHistoryFilePath)
    {
    }

    internal McpHistoryService(Func<string> historyPathResolver)
    {
        _historyPathResolver = historyPathResolver ?? throw new ArgumentNullException(nameof(historyPathResolver));
    }

    public JsonObject Query(string? query, string? fromDate, string? toDate, string fileType, int limit)
    {
        List<HistoryItem> historyItems = LoadItems();
        Dictionary<long, string> indexedTexts = new HistoryOcrIndexStore(_historyPathResolver())
            .GetTexts(historyItems.Select(item => item.Id));

        var filtered = historyItems
            .Where(item => MatchesDate(item, fromDate, toDate))
            .Where(item => MatchesFileType(item, fileType))
            .Where(item => MatchesQuery(item, query, indexedTexts.TryGetValue(item.Id, out string? ocrText) ? ocrText : null))
            .OrderByDescending(item => item.DateTime)
            .ToList();

        var boundedLimit = Math.Clamp(limit, 1, 100);
        var page = filtered
            .Take(boundedLimit)
            .Select(item => CreateSummary(item, indexedTexts.TryGetValue(item.Id, out string? ocrText) ? ocrText : null))
            .Cast<JsonNode>()
            .ToArray();

        return new JsonObject
        {
            ["items"] = new JsonArray(page),
            ["total_count"] = filtered.Count,
            ["has_more"] = filtered.Count > boundedLimit
        };
    }

    public Task<JsonObject> GetItemAsync(string? id, CancellationToken cancellationToken)
    {
        return CreateDetailsAsync(FindItem(id), cancellationToken);
    }

    public async Task<JsonObject> CreateDetailsAsync(HistoryItem item, CancellationToken cancellationToken)
    {
        long fileSize = 0;
        string? fileHash = null;
        int? width = null;
        int? height = null;
        string? ocrText = TryGetIndexedOcrText(item.Id);

        bool sourcePathConfigured = !string.IsNullOrWhiteSpace(item.FilePath);
        bool sourceFileExists = sourcePathConfigured && File.Exists(item.FilePath);
        bool thumbnailPathConfigured = !string.IsNullOrWhiteSpace(item.ThumbnailURL);
        bool thumbnailFileExists = thumbnailPathConfigured &&
            TryResolveLocalFilePath(item.ThumbnailURL, out var resolvedThumb) &&
            File.Exists(resolvedThumb);

        if (sourceFileExists)
        {
            var fileInfo = new FileInfo(item.FilePath);
            fileSize = fileInfo.Length;

            using var hash = MD5.Create();
            await using var stream = File.OpenRead(item.FilePath);
            fileHash = Convert.ToHexString(await hash.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();

            if (FileHelpers.IsImageFile(item.FilePath))
            {
                using var bitmap = SkiaSharp.SKBitmap.Decode(item.FilePath);
                if (bitmap != null)
                {
                    width = bitmap.Width;
                    height = bitmap.Height;

                    if (string.IsNullOrWhiteSpace(ocrText) && PlatformServices.Ocr?.IsSupported == true)
                    {
                        var result = await PlatformServices.Ocr.RecognizeAsync(bitmap, new OcrOptions());
                        if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
                        {
                            ocrText = result.Text;
                            await OcrIndexingService.PersistRecognizedTextAsync(item, result.Text, "mcp-history-details", null, cancellationToken);
                        }
                    }
                }
            }
        }

        return new JsonObject
        {
            ["id"] = item.Id.ToString(CultureInfo.InvariantCulture),
            ["file_path"] = item.FilePath,
            ["file_url"] = CreateFileUrl(item.FilePath),
            ["file_exists"] = sourceFileExists,
            ["file_missing_path"] = sourcePathConfigured && !sourceFileExists ? item.FilePath : null,
            ["thumbnail_path"] = string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL,
            ["thumbnail_resource"] = CreateBlobResourceUriIfLocal(item),
            ["thumbnail_exists"] = thumbnailFileExists,
            ["capture_type"] = InferCaptureType(item),
            ["capture_width"] = width,
            ["capture_height"] = height,
            ["created_at"] = item.DateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["file_size_bytes"] = fileSize,
            ["file_hash_md5"] = fileHash,
            ["upload_url"] = string.IsNullOrWhiteSpace(item.URL) ? null : item.URL,
            ["ocr_text"] = ocrText,
            ["window_title"] = item.TagsWindowTitle,
            ["application_name"] = item.TagsProcessName,
            ["tags"] = McpJsonSerialization.ToJsonArray(item.Tags.Keys),
            ["host"] = item.Host,
            ["type"] = item.Type
        };
    }

    public void AppendItem(string filePath, string type, string? url = null, string? windowTitle = null, string? processName = null)
    {
        using var historyManager = new HistoryManagerSQLite(_historyPathResolver());
        var item = new HistoryItem
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            DateTime = DateTime.Now,
            Type = type,
            URL = url ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            item.Tags["WindowTitle"] = windowTitle;
        }

        if (!string.IsNullOrWhiteSpace(processName))
        {
            item.Tags["ProcessName"] = processName;
        }

        historyManager.AppendHistoryItem(item);
    }

    public List<HistoryItem> LoadItems()
    {
        var historyPath = _historyPathResolver();
        if (!File.Exists(historyPath))
        {
            return [];
        }

        using var manager = new HistoryManagerSQLite(historyPath);
        var count = manager.GetTotalCount();
        return count > 0 ? manager.GetHistoryItems(0, count) : [];
    }

    public HistoryItem FindItem(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("id is required.", nameof(id));
        }

        if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
        {
            throw new ArgumentException("History item IDs must be integer row IDs.", nameof(id));
        }

        var item = LoadItems().FirstOrDefault(historyItem => historyItem.Id == parsedId);
        return item ?? throw new InvalidOperationException($"History item '{id}' was not found.");
    }

    internal static string ResolveBlobPath(HistoryItem item)
    {
        if (TryResolveLocalFilePath(item.ThumbnailURL, out var thumbnailPath) && File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        if (TryResolveLocalFilePath(item.FilePath, out var filePath) && File.Exists(filePath))
        {
            return filePath;
        }

        bool hasThumbnail = !string.IsNullOrWhiteSpace(item.ThumbnailURL);
        bool hasFilePath = !string.IsNullOrWhiteSpace(item.FilePath);
        string message = (hasThumbnail && hasFilePath)
            ? "History item thumbnail and source files were not found."
            : hasFilePath
                ? "History item source file was not found."
                : "History item thumbnail source file was not found.";
        throw new FileNotFoundException(message, item.FilePath);
    }

    internal static string CreateBlobResourceUri(HistoryItem item)
    {
        return $"xerahs://history/thumb/{item.Id.ToString(CultureInfo.InvariantCulture)}";
    }

    internal static JsonObject CreateBlobTooLargeResponse(string uri, string blobPath, long byteLength)
    {
        var details = new JsonObject
        {
            ["error"] = "history_blob_too_large",
            ["message"] = "History item blob is too large to inline. Open the local file path directly or reduce the capture/thumbnail size.",
            ["resource_uri"] = uri,
            ["file_path"] = blobPath,
            ["file_size_bytes"] = byteLength,
            ["max_inline_bytes"] = MaxInlineBlobBytes
        };

        return McpJsonSerialization.CreateResource(uri, details);
    }

    internal static JsonObject CreateBlobMissingResponse(string uri, HistoryItem item)
    {
        var details = new JsonObject
        {
            ["error"] = "history_blob_missing",
            ["message"] = "History item thumbnail/source file is no longer available locally. The capture may have been moved, deleted, or the thumbnail cache may have been cleaned.",
            ["resource_uri"] = uri,
            ["history_id"] = item.Id.ToString(CultureInfo.InvariantCulture),
            ["file_path"] = string.IsNullOrWhiteSpace(item.FilePath) ? null : item.FilePath,
            ["thumbnail_path"] = string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL
        };

        return McpJsonSerialization.CreateResource(uri, details);
    }

    internal static string? CreateFileUrl(string? filePath)
    {
        if (!TryResolveLocalFilePath(filePath, out var resolvedPath))
        {
            return null;
        }

        string escapedPath = Uri.EscapeDataString(resolvedPath).Replace("%5C", "/");
        // Returning the already escaped URI avoids System.Uri normalizing legal
        // trailing filename whitespace away on Windows.
        return "file:///" + escapedPath.Replace("//", "/");
    }

    private string? TryGetIndexedOcrText(long historyItemId)
    {
        try
        {
            return new HistoryOcrIndexStore(_historyPathResolver()).GetText(historyItemId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
    }

    private static bool TryResolveLocalFilePath(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(value))
            {
                int trailingWhitespace = 0;
                while (trailingWhitespace < value.Length && char.IsWhiteSpace(value[value.Length - 1 - trailingWhitespace]))
                {
                    trailingWhitespace++;
                }

                path = Path.GetFullPath(value);
                if (trailingWhitespace > 0)
                {
                    int preservedTrailingWhitespace = 0;
                    while (preservedTrailingWhitespace < path.Length && char.IsWhiteSpace(path[path.Length - 1 - preservedTrailingWhitespace]))
                    {
                        preservedTrailingWhitespace++;
                    }

                    if (preservedTrailingWhitespace < trailingWhitespace)
                    {
                        path += new string(' ', trailingWhitespace - preservedTrailingWhitespace);
                    }
                }

                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                return false;
            }

            path = uri.LocalPath;
            return !string.IsNullOrWhiteSpace(path);
        }

        try
        {
            path = Path.GetFullPath(value);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string? CreateBlobResourceUriIfLocal(HistoryItem item)
    {
        if (TryResolveLocalFilePath(item.ThumbnailURL, out var thumbnailPath) && File.Exists(thumbnailPath))
        {
            return CreateBlobResourceUri(item);
        }

        if (TryResolveLocalFilePath(item.FilePath, out var filePath) && File.Exists(filePath))
        {
            return CreateBlobResourceUri(item);
        }

        return null;
    }

    private static JsonObject CreateSummary(HistoryItem item, string? ocrText)
    {
        long size = 0;
        if (!string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
        {
            size = new FileInfo(item.FilePath).Length;
        }

        return new JsonObject
        {
            ["id"] = item.Id.ToString(CultureInfo.InvariantCulture),
            ["file_path"] = item.FilePath,
            ["thumbnail_url"] = string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL,
            ["thumbnail_resource"] = CreateBlobResourceUriIfLocal(item),
            ["created_at"] = item.DateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["file_size_bytes"] = size,
            ["ocr_text"] = string.IsNullOrWhiteSpace(ocrText) ? null : ocrText,
            ["tags"] = McpJsonSerialization.ToJsonArray(item.Tags.Keys)
        };
    }

    private static bool MatchesDate(HistoryItem item, string? fromDate, string? toDate)
    {
        if (DateOnly.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) &&
            item.DateTime.Date < from.ToDateTime(TimeOnly.MinValue).Date)
        {
            return false;
        }

        if (DateOnly.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) &&
            item.DateTime.Date > to.ToDateTime(TimeOnly.MaxValue).Date)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesFileType(HistoryItem item, string? fileType)
    {
        return fileType?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => true,
            "image" => string.Equals(item.Type, "Image", StringComparison.OrdinalIgnoreCase),
            "text" => string.Equals(item.Type, "Text", StringComparison.OrdinalIgnoreCase),
            "video" => string.Equals(item.Type, "Video", StringComparison.OrdinalIgnoreCase),
            "file" => string.Equals(item.Type, "File", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static bool MatchesQuery(HistoryItem item, string? query, string? indexedOcrText)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var needle = query.Trim();
        return Contains(item.FileName, needle) ||
               Contains(item.FilePath, needle) ||
               Contains(item.URL, needle) ||
               Contains(item.Host, needle) ||
               Contains(item.TagsWindowTitle, needle) ||
               Contains(item.TagsProcessName, needle) ||
               Contains(indexedOcrText, needle) ||
               item.Tags.Any(pair => Contains(pair.Key, needle) || Contains(pair.Value, needle));
    }

    private static bool Contains(string? source, string needle) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string InferCaptureType(HistoryItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.TagsWindowTitle))
        {
            return "window";
        }

        return item.Type.Equals("Image", StringComparison.OrdinalIgnoreCase) ? "screen" : item.Type.ToLowerInvariant();
    }
}
