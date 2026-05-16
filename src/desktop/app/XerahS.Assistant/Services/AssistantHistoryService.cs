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

using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.History;
using XerahS.Assistant.Models;
using XerahS.Assistant.Routing;

namespace XerahS.Assistant.Services;

public sealed record AssistantHistoryItem(
    string Id,
    string FilePath,
    string FileName,
    DateTimeOffset CapturedAt,
    string Type,
    string? OcrText,
    bool Exists,
    HistoryItem Source);

public interface IAssistantHistoryService
{
    Task<IReadOnlyList<AssistantHistoryItem>> GetLatestScreenshotsAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssistantHistoryItem>> SearchScreenshotsAsync(string query, int limit, CancellationToken cancellationToken);
    Task<string?> GetCachedOcrTextAsync(string filePath, CancellationToken cancellationToken);
    Task CacheOcrTextAsync(string filePath, string ocrText, CancellationToken cancellationToken);
    bool IsKnownHistoryFile(string filePath);
}

public sealed class AssistantHistoryService : IAssistantHistoryService
{
    public Task<IReadOnlyList<AssistantHistoryItem>> GetLatestScreenshotsAsync(int limit, CancellationToken cancellationToken)
    {
        int clampedLimit = Math.Clamp(limit, 1, AssistantCommandRouter.MaxLatestScreenshotLimit);

        return Task.Run<IReadOnlyList<AssistantHistoryItem>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = SettingsManager.GetHistoryFilePath();
            using var manager = new HistoryManagerSQLite(historyPath);

            IReadOnlyList<HistoryItem> items = GetLatestScreenshotHistoryItems(manager, clampedLimit);
            Dictionary<long, string> indexedTexts = new HistoryOcrIndexStore(historyPath).GetTexts(items.Select(item => item.Id));

            return items
                .Select(item => ToAssistantHistoryItem(item, indexedTexts))
                .ToList();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AssistantHistoryItem>> SearchScreenshotsAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<AssistantHistoryItem>>([]);
        }

        int clampedLimit = Math.Clamp(limit, 1, AssistantCommandRouter.MaxLatestScreenshotLimit);
        string needle = query.Trim();

        return Task.Run<IReadOnlyList<AssistantHistoryItem>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = SettingsManager.GetHistoryFilePath();
            using var manager = new HistoryManagerSQLite(historyPath);
            int count = manager.GetTotalCount();
            if (count <= 0)
            {
                return [];
            }

            List<HistoryItem> items = manager.GetHistoryItems(0, count)
                .Where(IsScreenshotHistoryItem)
                .ToList();
            Dictionary<long, string> indexedTexts = new HistoryOcrIndexStore(historyPath).GetTexts(items.Select(item => item.Id));

            return items
                .Where(item => MatchesSearch(item, needle, File.Exists(item.FilePath) && indexedTexts.TryGetValue(item.Id, out string? ocrText) ? ocrText : null))
                .OrderByDescending(item => item.DateTime)
                .Take(clampedLimit)
                .Select(item => ToAssistantHistoryItem(item, indexedTexts))
                .ToList();
        }, cancellationToken);
    }

    public Task<string?> GetCachedOcrTextAsync(string filePath, CancellationToken cancellationToken)
    {
        string? normalizedFilePath = NormalizeHistoryFilePath(filePath);
        if (normalizedFilePath == null || !File.Exists(normalizedFilePath))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = SettingsManager.GetHistoryFilePath();
            using var manager = new HistoryManagerSQLite(historyPath);
            HistoryItem? item = manager.GetLatestByFilePath(normalizedFilePath);
            return item == null
                ? null
                : new HistoryOcrIndexStore(historyPath).GetText(item.Id) ?? TryGetTag(item, "OcrText") ?? TryGetTag(item, "OCRText");
        }, cancellationToken);
    }

    public Task CacheOcrTextAsync(string filePath, string ocrText, CancellationToken cancellationToken)
    {
        string? normalizedFilePath = NormalizeHistoryFilePath(filePath);
        if (normalizedFilePath == null || string.IsNullOrWhiteSpace(ocrText) || !File.Exists(normalizedFilePath))
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = SettingsManager.GetHistoryFilePath();
            using var manager = new HistoryManagerSQLite(historyPath);
            HistoryItem? item = manager.GetLatestByFilePath(normalizedFilePath);
            if (item == null)
            {
                return;
            }

            await OcrIndexingService.PersistRecognizedTextAsync(item, ocrText, "assistant", null, cancellationToken);
        }, cancellationToken);
    }

    public bool IsKnownHistoryFile(string filePath)
    {
        string? normalized = NormalizeHistoryFilePath(filePath);
        if (normalized == null)
        {
            return false;
        }

        string historyPath = SettingsManager.GetHistoryFilePath();
        using var manager = new HistoryManagerSQLite(historyPath);
        return manager.ContainsFilePath(normalized);
    }

    private static IReadOnlyList<HistoryItem> GetLatestScreenshotHistoryItems(HistoryManagerSQLite manager, int limit)
    {
        const int PageSize = 250;
        List<HistoryItem> screenshots = new(limit);
        int offset = 0;

        while (screenshots.Count < limit)
        {
            List<HistoryItem> items = manager.GetHistoryItems(offset, PageSize);
            if (items.Count == 0)
            {
                break;
            }

            foreach (HistoryItem item in items)
            {
                if (IsScreenshotHistoryItem(item))
                {
                    screenshots.Add(item);
                    if (screenshots.Count == limit)
                    {
                        break;
                    }
                }
            }

            if (items.Count < PageSize)
            {
                break;
            }

            offset += items.Count;
        }

        return screenshots;
    }

    private static AssistantHistoryItem ToAssistantHistoryItem(HistoryItem item, IReadOnlyDictionary<long, string> indexedTexts)
    {
        string? ocrText = null;
        if (File.Exists(item.FilePath))
        {
            ocrText = indexedTexts.TryGetValue(item.Id, out string? indexedText)
                ? indexedText
                : TryGetTag(item, "OcrText") ?? TryGetTag(item, "OCRText");
        }

        return new AssistantHistoryItem(
            item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            item.FilePath,
            string.IsNullOrWhiteSpace(item.FileName) ? GetSafeFileName(item.FilePath) : item.FileName,
            new DateTimeOffset(item.DateTime == DateTime.MinValue ? DateTime.Now : item.DateTime),
            item.Type,
            ocrText,
            File.Exists(item.FilePath),
            item);
    }

    private static bool IsScreenshotHistoryItem(HistoryItem item)
    {
        if (string.IsNullOrWhiteSpace(item.FilePath))
        {
            return false;
        }

        return FileHelpers.IsImageFile(item.FilePath);
    }

    private static bool MatchesSearch(HistoryItem item, string query, string? indexedOcrText)
    {
        return ContainsSearchText(indexedOcrText, query) ||
            ContainsSearchText(item.FileName, query) ||
            ContainsSearchText(item.FilePath, query) ||
            ContainsSearchText(item.URL, query) ||
            ContainsSearchText(item.TagsWindowTitle, query) ||
            ContainsSearchText(item.TagsProcessName, query) ||
            item.Tags.Any(pair => ContainsSearchText(pair.Key, query) || ContainsSearchText(pair.Value, query));
    }

    private static bool ContainsSearchText(string? source, string query)
    {
        return !string.IsNullOrWhiteSpace(source) &&
            source.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetTag(HistoryItem item, string tag)
    {
        return item.Tags != null && item.Tags.TryGetValue(tag, out string? value)
            ? value
            : null;
    }

    private static string? NormalizeHistoryFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(filePath.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static string GetSafeFileName(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        string trimmed = filePath.Trim().TrimEnd('/', '\\');
        string fileName = Path.GetFileName(trimmed);
        if (!string.IsNullOrWhiteSpace(fileName) && !string.Equals(fileName, trimmed, StringComparison.Ordinal))
        {
            return fileName;
        }

        string[] segments = trimmed.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : fileName;
    }

}
