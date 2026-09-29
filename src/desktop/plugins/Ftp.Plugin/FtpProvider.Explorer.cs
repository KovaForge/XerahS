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

using FluentFTP;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.FileUploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Ftp.Plugin;

/// <summary>
/// Media Browser support for FTP, FTPS and SFTP (ported from ShareX's remote storage providers).
/// Paths are relative to the server root ("" is "/"); every operation opens its own connection,
/// so a dropped connection never poisons later calls.
/// </summary>
public partial class FtpProvider
{
    private const string SettingsMetadataKey = "settingsJson";
    private const long MaxThumbnailBytes = 5 * 1024 * 1024;

    public bool SupportsFolders => true;

    public ExplorerCapabilities BrowserCapabilities =>
        ExplorerCapabilities.Download | ExplorerCapabilities.Upload | ExplorerCapabilities.Rename |
        ExplorerCapabilities.Delete | ExplorerCapabilities.Url | ExplorerCapabilities.CreateFolder |
        ExplorerCapabilities.Thumbnails;

    public Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        FTPAccount account = ReadAccount(query.SettingsJson);
        string directory = NormalizePath(query.FolderPath);

        return RunAsync(account, (ftp, sftp) =>
        {
            IEnumerable<MediaItem> items = ftp != null
                ? ftp.GetListing(Remote(account, directory))
                    .Where(item => item.Name is not "." and not ".." && item.Type is FtpObjectType.File or FtpObjectType.Directory)
                    .Select(item => CreateItem(account, query.SettingsJson, directory, item.Name,
                        item.Type == FtpObjectType.Directory, item.Type == FtpObjectType.File ? item.Size : 0,
                        item.Modified == default ? null : item.Modified))
                : sftp!.ListDirectory(Remote(account, directory))
                    .Where(file => file.Name is not "." and not "..")
                    .Select(file => CreateItem(account, query.SettingsJson, directory, file.Name,
                        file.IsDirectory && !file.IsSymbolicLink, file.IsDirectory ? 0 : file.Length,
                        file.LastWriteTimeUtc == default ? null : DateTime.SpecifyKind(file.LastWriteTimeUtc, DateTimeKind.Utc)));

            return new ExplorerPage { Items = items.ToList() };
        }, cancellation);
    }

    public async Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        if (item.IsFolder || item.SizeBytes > MaxThumbnailBytes || !FileHelpers.IsImageFile(item.Name))
        {
            return null;
        }

        await using Stream? content = await GetContentAsync(item, cancellation);
        return content is MemoryStream memory ? memory.ToArray() : null;
    }

    public async Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
    {
        if (item.IsFolder || !item.Metadata.TryGetValue(SettingsMetadataKey, out string? settingsJson))
        {
            return null;
        }

        FTPAccount account = ReadAccount(settingsJson);
        var buffer = new MemoryStream();
        await RunAsync(account, (ftp, sftp) =>
        {
            if (ftp != null)
            {
                if (!ftp.DownloadStream(buffer, Remote(account, item.Path)))
                {
                    throw new InvalidOperationException($"Could not download {item.Name}.");
                }
            }
            else
            {
                sftp!.DownloadFile(Remote(account, item.Path), buffer);
            }

            return true;
        }, cancellation);

        buffer.Position = 0;
        return buffer;
    }

    // Legacy members without instance settings; the Media Browser uses the ExplorerContext overloads.
    public Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default) =>
        item.Metadata.TryGetValue(SettingsMetadataKey, out string? settingsJson)
            ? DeleteAsync(new ExplorerContext { SettingsJson = settingsJson }, item, cancellation)
            : Task.FromResult(false);

    public Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default) =>
        Task.FromResult(false);

    public Task<bool> CreateFolderAsync(ExplorerContext context, string parentPath, string folderName, CancellationToken cancellation = default)
    {
        ValidateName(folderName);
        FTPAccount account = ReadAccount(context.SettingsJson);
        string path = Remote(account, Combine(NormalizePath(parentPath), folderName));
        return RunAsync(account, (ftp, sftp) =>
        {
            if (ftp != null)
            {
                if (!ftp.CreateDirectory(path))
                {
                    throw new InvalidOperationException($"Could not create {folderName}.");
                }
            }
            else
            {
                sftp!.CreateDirectory(path);
            }

            return true;
        }, cancellation);
    }

    public Task<bool> UploadAsync(ExplorerContext context, string folderPath, string fileName, Stream content, CancellationToken cancellation = default)
    {
        ValidateName(fileName);
        FTPAccount account = ReadAccount(context.SettingsJson);
        string path = Remote(account, Combine(NormalizePath(folderPath), fileName));
        return RunAsync(account, (ftp, sftp) =>
        {
            if (ftp != null)
            {
                if (ftp.UploadStream(content, path, FtpRemoteExists.Overwrite, createRemoteDir: true) != FtpStatus.Success)
                {
                    throw new InvalidOperationException($"Could not upload {fileName}.");
                }
            }
            else
            {
                sftp!.UploadFile(content, path, canOverride: true);
            }

            return true;
        }, cancellation);
    }

    public Task<bool> RenameAsync(ExplorerContext context, MediaItem item, string newName, CancellationToken cancellation = default)
    {
        ValidateName(newName);
        FTPAccount account = ReadAccount(context.SettingsJson);
        string source = NormalizePath(item.Path);
        string destination = Combine(ParentOf(source), newName);
        return RunAsync(account, (ftp, sftp) =>
        {
            if (ftp != null)
            {
                ftp.Rename(Remote(account, source), Remote(account, destination));
            }
            else
            {
                sftp!.RenameFile(Remote(account, source), Remote(account, destination));
            }

            return true;
        }, cancellation);
    }

    public Task<bool> DeleteAsync(ExplorerContext context, MediaItem item, CancellationToken cancellation = default)
    {
        FTPAccount account = ReadAccount(context.SettingsJson);
        string path = NormalizePath(item.Path);
        return RunAsync(account, (ftp, sftp) =>
        {
            if (ftp != null)
            {
                if (item.IsFolder)
                {
                    ftp.DeleteDirectory(Remote(account, path)); // recursive in FluentFTP
                }
                else
                {
                    ftp.DeleteFile(Remote(account, path));
                }
            }
            else if (item.IsFolder)
            {
                DeleteSftpDirectory(sftp!, path, cancellation);
            }
            else
            {
                sftp!.DeleteFile(Remote(account, path));
            }

            return true;
        }, cancellation);
    }

    public Task<bool?> HasChildrenAsync(ExplorerContext context, MediaItem folder, CancellationToken cancellation = default)
    {
        FTPAccount account = ReadAccount(context.SettingsJson);
        string path = Remote(account, NormalizePath(folder.Path));
        return RunAsync<bool?>(account, (ftp, sftp) => ftp != null
            ? ftp.GetListing(path).Any(item => item.Name is not "." and not "..")
            : sftp!.ListDirectory(path).Any(file => file.Name is not "." and not ".."), cancellation);
    }

    private static void DeleteSftpDirectory(SftpClient sftp, string directory, CancellationToken cancellation)
    {
        foreach (ISftpFile file in sftp.ListDirectory(SftpPath(directory)).Where(file => file.Name is not "." and not ".."))
        {
            cancellation.ThrowIfCancellationRequested();
            string child = Combine(directory, file.Name);
            if (file.IsDirectory && !file.IsSymbolicLink)
            {
                DeleteSftpDirectory(sftp, child, cancellation);
            }
            else
            {
                sftp.DeleteFile(SftpPath(child));
            }
        }

        sftp.DeleteDirectory(SftpPath(directory));
    }

    private static MediaItem CreateItem(FTPAccount account, string? settingsJson, string directory, string name, bool isFolder, long size, DateTime? modified)
    {
        string path = Combine(directory, name);
        var item = new MediaItem
        {
            Id = path,
            Name = name,
            Path = path,
            IsFolder = isFolder,
            SizeBytes = isFolder ? 0 : Math.Max(size, 0),
            ModifiedAt = modified,
            MimeType = isFolder ? null : MimeTypes.GetMimeTypeFromFileName(name),
            Url = isFolder ? null : TryGetUrl(account, name, directory),
        };

        if (settingsJson != null)
        {
            item.Metadata[SettingsMetadataKey] = settingsJson;
        }

        return item;
    }

    private static string? TryGetUrl(FTPAccount account, string name, string directory)
    {
        try
        {
            return account.GetUriPath(name, directory);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException)
        {
            return null;
        }
    }

    private static FTPAccount ReadAccount(string? settingsJson)
    {
        FtpConfigModel? config = string.IsNullOrWhiteSpace(settingsJson) ? null : JsonConvert.DeserializeObject<FtpConfigModel>(settingsJson);
        if (config == null || string.IsNullOrWhiteSpace(config.Host))
        {
            throw new InvalidOperationException("The FTP account has no host configured.");
        }

        return ToFtpAccount(config);
    }

    /// <summary>Runs work on a fresh FTP or SFTP connection off the UI thread.</summary>
    private static Task<T> RunAsync<T>(FTPAccount account, Func<FtpClient?, SftpClient?, T> work, CancellationToken cancellation)
    {
        return Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            if (account.Protocol == FTPProtocol.SFTP)
            {
                using SftpClient sftp = FtpClientFactory.CreateSftp(account, out string? error)
                    ?? throw new InvalidOperationException(error ?? "Could not create the SFTP client.");
                sftp.Connect();
                try
                {
                    return work(null, sftp);
                }
                finally
                {
                    sftp.Disconnect();
                }
            }

            using FtpClient ftp = FtpClientFactory.CreateFtp(account);
            ftp.Connect();
            try
            {
                return work(ftp, null);
            }
            finally
            {
                ftp.Disconnect();
            }
        }, cancellation);
    }

    internal static string NormalizePath(string? path) => path?.Replace('\\', '/').Trim('/') ?? string.Empty;

    /// <summary>FTP paths are absolute from the server root; SFTP paths are relative to the login's home, like ShareX.</summary>
    private static string Remote(FTPAccount account, string path) =>
        account.Protocol == FTPProtocol.SFTP ? SftpPath(path) : "/" + NormalizePath(path);

    private static string SftpPath(string path)
    {
        string normalized = NormalizePath(path);
        return normalized.Length > 0 ? normalized : ".";
    }

    private static string Combine(string directory, string name) =>
        string.IsNullOrEmpty(directory) ? name : directory + "/" + name;

    private static string ParentOf(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator >= 0 ? path[..separator] : string.Empty;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\'))
        {
            throw new ArgumentException($"'{name}' is not a valid name.", nameof(name));
        }
    }
}
