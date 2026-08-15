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

using XerahS.Uploaders;

namespace ShareX.XBackBone.Plugin;

public sealed class XBackBoneUploader : FileUploader
{
    private readonly XBackBoneConfigModel _config;
    private readonly string _apiToken;

    public XBackBoneUploader(XBackBoneConfigModel config, string apiToken)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _apiToken = apiToken ?? string.Empty;
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        UploadResult result = new();
        string normalizedServerUrl = XBackBoneClient.NormalizeServerUrl(_config.ServerUrl);

        if (!XBackBoneProvider.IsValidServerUrl(normalizedServerUrl))
        {
            Errors.Add("XBackBone instance URL must be a valid http:// or https:// URL.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            Errors.Add("XBackBone API token is required.");
            return result;
        }

        if (!Enum.IsDefined(_config.ApiGeneration))
        {
            Errors.Add("The selected XBackBone API generation is not supported.");
            return result;
        }

        try
        {
            long streamLength = stream.CanSeek ? stream.Length : 0;
            ProgressManager? progress = streamLength > 0 ? new ProgressManager(streamLength) : null;
            XBackBoneClient client = new(normalizedServerUrl, _apiToken);
            XBackBoneUploadResponse response = client.UploadAsync(
                stream,
                fileName,
                _config.ApiGeneration,
                bytesTransferred =>
                {
                    if (progress != null && AllowReportProgress && progress.UpdateProgress(bytesTransferred))
                    {
                        OnProgressChanged(progress);
                    }
                }).GetAwaiter().GetResult();

            result.URL = response.CanonicalUrl;
            result.ThumbnailURL = response.RawUrl;
            result.DeletionURL = response.DeletionUrl;
            result.Response = "XBackBone upload completed.";
            result.IsSuccess = true;
            return result;
        }
        catch (Exception ex)
        {
            Errors.Add(RedactToken(ex.Message));
            return result;
        }
    }

    private string RedactToken(string message)
    {
        return string.IsNullOrEmpty(_apiToken)
            ? message
            : message.Replace(_apiToken, "[redacted]", StringComparison.Ordinal);
    }
}
