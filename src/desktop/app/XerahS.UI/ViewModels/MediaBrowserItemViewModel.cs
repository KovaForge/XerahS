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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.ImageEditor.Presentation.Theming;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

/// <summary>Kinds used by the Media Browser type filter.</summary>
public enum MediaBrowserTypeFilter
{
    All,
    Images,
    Videos,
    Audio,
    Documents,
    Archives,
}

/// <summary>A remote file or folder with display helpers and a lazily loaded thumbnail.</summary>
public sealed partial class MediaBrowserItemViewModel : ObservableObject
{
    private static readonly string[] DocumentExtensions = [".pdf", ".txt", ".md", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".json", ".xml", ".html", ".log", ".rtf", ".odt"];
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private Bitmap? _thumbnail;

    public MediaBrowserItemViewModel(MediaItem item)
    {
        Item = item;
    }

    public MediaItem Item { get; }
    public string Name => Item.Name;
    public bool IsFolder => Item.IsFolder;
    public bool HasThumbnail => Thumbnail != null;
    public bool IsImage => !IsFolder && (FileHelpers.IsImageFile(Name) || Item.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);
    public bool IsVideo => !IsFolder && (FileHelpers.IsVideoFile(Name) || Item.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true);
    public bool IsAudio => !IsFolder && Item.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true;
    public bool HasUrl => !IsFolder && !string.IsNullOrWhiteSpace(Item.Url);

    public string Icon => IsFolder ? LucideIcons.folder
        : IsImage ? LucideIcons.file_image
        : IsVideo ? LucideIcons.file_video
        : IsAudio ? LucideIcons.file_audio
        : IsArchive ? LucideIcons.file_archive
        : IsDocument ? LucideIcons.file_text
        : LucideIcons.file;

    public string SizeText => IsFolder ? string.Empty : FormatBytes(Item.SizeBytes);
    public string ModifiedText => Item.ModifiedAt is { } modified ? modified.ToLocalTime().ToString("g") : string.Empty;
    public DateTime? Modified => Item.ModifiedAt ?? Item.CreatedAt;

    private bool IsDocument => DocumentExtensions.Contains(System.IO.Path.GetExtension(Name), StringComparer.OrdinalIgnoreCase)
        || Item.MimeType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsArchive => ArchiveExtensions.Contains(System.IO.Path.GetExtension(Name), StringComparer.OrdinalIgnoreCase);

    public bool Matches(MediaBrowserTypeFilter filter) => IsFolder || filter switch
    {
        MediaBrowserTypeFilter.Images => IsImage,
        MediaBrowserTypeFilter.Videos => IsVideo,
        MediaBrowserTypeFilter.Audio => IsAudio,
        MediaBrowserTypeFilter.Documents => IsDocument,
        MediaBrowserTypeFilter.Archives => IsArchive,
        _ => true,
    };

    internal static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unit = 0;
        double value = bytes;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }
}
