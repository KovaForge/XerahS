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
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Nextcloud.Plugin;

public sealed class NextcloudUploader : FileUploader, IUploadHandler
{
    private readonly NextcloudConfigModel _config;
    private readonly string _appPassword;
    private readonly string _sharePassword;

    public NextcloudUploader(NextcloudConfigModel config, string appPassword, string sharePassword)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _appPassword = appPassword ?? string.Empty;
        _sharePassword = sharePassword ?? string.Empty;
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        UploadOutcome outcome = UploadAsync(new UploadRequest
        {
            Content = stream,
            FileName = fileName,
            Category = UploaderCategory.File
        }, CancellationToken.None).GetAwaiter().GetResult();

        if (!outcome.Succeeded && !string.IsNullOrWhiteSpace(outcome.Error))
        {
            Errors.Add(outcome.Error);
        }

        return outcome.ToUploadResult();
    }

    public async Task<UploadOutcome> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ServerUrl))
        {
            return UploadOutcome.Failed("Nextcloud server URL is required.");
        }

        string loginName = !string.IsNullOrWhiteSpace(_config.LoginName) ? _config.LoginName : _config.UserId;
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(_appPassword))
        {
            return UploadOutcome.Failed("Nextcloud login name and app password are required.");
        }

        if (_config.CreatePublicShare)
        {
            if (!_config.SupportsPublicShares)
            {
                return UploadOutcome.Failed("This Nextcloud server does not support public shares. Disable public share creation or refresh the server profile.");
            }

            if (_config.AutoExpireShare && !_config.SupportsExpireDate)
            {
                return UploadOutcome.Failed("This Nextcloud server does not support share expiry. Disable auto-expire or refresh the server profile.");
            }

            if (!string.IsNullOrWhiteSpace(_sharePassword) && !_config.SupportsSharePasswords)
            {
                return UploadOutcome.Failed("This Nextcloud server does not support share passwords. Clear the share password or refresh the server profile.");
            }
        }

        try
        {
            string userId = !string.IsNullOrWhiteSpace(_config.UserId) ? _config.UserId : loginName;
            string relativeFolderPath = NameParser.Parse(NameParserType.Default, NextcloudClient.NormalizeRelativePath(_config.RemotePath));
            string relativeFilePath = NextcloudClient.CombineRelativePath(relativeFolderPath, request.FileName);
            string sharePath = "/" + relativeFilePath;

            NextcloudClient client = new(_config.ServerUrl, loginName, _appPassword);
            await client.UploadFileAsync(
                request.Content,
                userId,
                relativeFolderPath,
                request.FileName,
                _config.UseChunkedUpload && _config.SupportsChunking,
                _config.ChunkSizeMiB,
                bytesTransferred => request.Progress?.Report(new UploadProgressReport(bytesTransferred, request.ContentLength)),
                cancellationToken).ConfigureAwait(false);

            if (!_config.CreatePublicShare)
            {
                return UploadOutcome.Success(url: null, "Nextcloud upload completed.", urlExpected: false);
            }

            NextcloudShareInfo? shareInfo = await client.CreatePublicShareAsync(
                sharePath,
                _config.AutoExpireShare && _config.SupportsExpireDate,
                _config.ExpireAfterDays,
                _config.SupportsSharePasswords ? _sharePassword : string.Empty,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(shareInfo?.Url))
            {
                return UploadOutcome.Failed("Nextcloud upload succeeded but the public share URL was not returned.");
            }

            return UploadOutcome.Success(shareInfo.Url, "Nextcloud upload completed.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UploadOutcome.Failed(ex.Message);
        }
    }
}
