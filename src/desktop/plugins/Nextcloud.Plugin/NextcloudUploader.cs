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

namespace ShareX.Nextcloud.Plugin;

public sealed class NextcloudUploader : FileUploader
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
        UploadResult result = new();

        if (string.IsNullOrWhiteSpace(_config.ServerUrl))
        {
            Errors.Add("Nextcloud server URL is required.");
            return result;
        }

        string loginName = !string.IsNullOrWhiteSpace(_config.LoginName) ? _config.LoginName : _config.UserId;
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(_appPassword))
        {
            Errors.Add("Nextcloud login name and app password are required.");
            return result;
        }

        try
        {
            string userId = !string.IsNullOrWhiteSpace(_config.UserId) ? _config.UserId : loginName;
            string relativeFolderPath = NameParser.Parse(NameParserType.Default, NextcloudClient.NormalizeRelativePath(_config.RemotePath));
            string relativeFilePath = NextcloudClient.CombineRelativePath(relativeFolderPath, fileName);
            string sharePath = "/" + relativeFilePath;

            long streamLength = stream.CanSeek ? stream.Length : 0;
            ProgressManager? progress = streamLength > 0 ? new ProgressManager(streamLength) : null;
            NextcloudClient client = new(_config.ServerUrl, loginName, _appPassword);

            client.UploadFileAsync(
                stream,
                userId,
                relativeFolderPath,
                fileName,
                _config.UseChunkedUpload && _config.SupportsChunking,
                _config.ChunkSizeMiB,
                bytesTransferred =>
                {
                    if (progress != null && AllowReportProgress && progress.UpdateProgress(bytesTransferred))
                    {
                        OnProgressChanged(progress);
                    }
                }).GetAwaiter().GetResult();

            result.IsSuccess = true;
            result.Response = "Nextcloud upload completed.";

            if (!_config.CreatePublicShare)
            {
                result.IsURLExpected = false;
                return result;
            }

            NextcloudShareInfo? shareInfo = client.CreatePublicShareAsync(
                sharePath,
                _config.AutoExpireShare && _config.SupportsExpireDate,
                _config.ExpireAfterDays,
                _config.SupportsSharePasswords ? _sharePassword : string.Empty).GetAwaiter().GetResult();

            result.URL = shareInfo?.Url;
            result.IsSuccess = !string.IsNullOrWhiteSpace(result.URL);

            if (string.IsNullOrWhiteSpace(result.URL))
            {
                Errors.Add("Nextcloud upload succeeded but the public share URL was not returned.");
            }

            return result;
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            return result;
        }
    }
}
