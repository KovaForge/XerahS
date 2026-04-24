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

            return manager.GetHistoryItems(0, 250)
                .Where(IsScreenshotHistoryItem)
                .Take(clampedLimit)
                .Select(ToAssistantHistoryItem)
                .ToList();
        }, cancellationToken);
    }

    public Task<string?> GetCachedOcrTextAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = SettingsManager.GetHistoryFilePath();
            using var manager = new HistoryManagerSQLite(historyPath);
            HistoryItem? item = manager.GetLatestByFilePath(filePath);
            return item == null ? null : TryGetTag(item, "OcrText") ?? TryGetTag(item, "OCRText");
        }, cancellationToken);
    }

    public Task CacheOcrTextAsync(string filePath, string ocrText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(ocrText))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = SettingsManager.GetHistoryFilePath();
            using var manager = new HistoryManagerSQLite(historyPath);
            HistoryItem? item = manager.GetLatestByFilePath(filePath);
            if (item == null)
            {
                return;
            }

            item.Tags ??= new Dictionary<string, string?>();
            if (item.Tags.TryGetValue("OcrText", out string? existing) && string.Equals(existing, ocrText, StringComparison.Ordinal))
            {
                return;
            }

            item.Tags["OcrText"] = ocrText;
            item.Tags.Remove("OCRText");
            manager.Edit(item);
        }, cancellationToken);
    }

    public bool IsKnownHistoryFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(filePath);
        }
        catch
        {
            return false;
        }

        string historyPath = SettingsManager.GetHistoryFilePath();
        using var manager = new HistoryManagerSQLite(historyPath);
        return manager.ContainsFilePath(normalized);
    }

    private static AssistantHistoryItem ToAssistantHistoryItem(HistoryItem item)
    {
        return new AssistantHistoryItem(
            item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            item.FilePath,
            string.IsNullOrWhiteSpace(item.FileName) ? Path.GetFileName(item.FilePath) : item.FileName,
            new DateTimeOffset(item.DateTime == DateTime.MinValue ? DateTime.Now : item.DateTime),
            item.Type,
            TryGetTag(item, "OcrText") ?? TryGetTag(item, "OCRText"),
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

    private static string? TryGetTag(HistoryItem item, string tag)
    {
        return item.Tags != null && item.Tags.TryGetValue(tag, out string? value)
            ? value
            : null;
    }

}
