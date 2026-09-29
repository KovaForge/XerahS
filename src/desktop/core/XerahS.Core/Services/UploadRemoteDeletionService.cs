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
using XerahS.History;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Services;

/// <summary>
/// "Delete from host" for history items: finds the uploader instance that made the upload and asks
/// its provider (when it implements <see cref="IUploadRemover"/>) to delete the remote object.
/// </summary>
public static class UploadRemoteDeletionService
{
    /// <summary>History tag holding the uploader instance id of a successful upload.</summary>
    public const string UploaderInstanceIdTag = "UploaderInstanceId";

    /// <summary>History tag set (UTC, round-trip format) once the remote object was deleted.</summary>
    public const string RemoteDeletedAtTag = "RemoteDeletedAt";

    /// <summary>Prefix for upload result metadata copied into history tags.</summary>
    public const string UploadResultTagPrefix = "UploadResult.";

    /// <summary>True when the item records an uploader instance whose provider can delete uploads.</summary>
    public static bool CanDelete(HistoryItem? item)
    {
        return item != null && TryResolve(item, out _, out _);
    }

    public static async Task<bool> DeleteAsync(HistoryItem item, CancellationToken cancellation = default)
    {
        if (!TryResolve(item, out UploaderInstance? instance, out IUploadRemover? remover))
        {
            return false;
        }

        var context = new ExplorerContext { SettingsJson = instance!.SettingsJson, InstanceId = instance.InstanceId };
        try
        {
            bool deleted = await remover!.DeleteUploadAsync(context, item.URL, GetUploadMetadata(item), cancellation);
            DebugHelper.WriteLine($"[RemoteDelete] {(deleted ? "Deleted" : "Could not delete")} {item.URL} via {instance.DisplayName}.");
            if (deleted)
            {
                item.Tags![RemoteDeletedAtTag] = DateTime.UtcNow.ToString("O");
            }

            return deleted;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"[RemoteDelete] Failed to delete {item.URL} via {instance.DisplayName}");
            return false;
        }
    }

    internal static IReadOnlyDictionary<string, string?> GetUploadMetadata(HistoryItem item)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (item.Tags == null)
        {
            return metadata;
        }

        foreach (var pair in item.Tags)
        {
            if (pair.Key.StartsWith(UploadResultTagPrefix, StringComparison.Ordinal))
            {
                metadata[pair.Key[UploadResultTagPrefix.Length..]] = pair.Value;
            }
        }

        return metadata;
    }

    private static bool TryResolve(HistoryItem item, out UploaderInstance? instance, out IUploadRemover? remover)
    {
        instance = null;
        remover = null;

        if (string.IsNullOrWhiteSpace(item.URL) ||
            item.Tags == null ||
            item.Tags.ContainsKey(RemoteDeletedAtTag) ||
            !item.Tags.TryGetValue(UploaderInstanceIdTag, out string? instanceId) ||
            string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        instance = InstanceManager.Instance.GetInstance(instanceId);
        if (instance == null)
        {
            return false;
        }

        remover = ProviderCatalog.GetProvider(instance.ProviderId) as IUploadRemover;
        return remover != null;
    }
}
