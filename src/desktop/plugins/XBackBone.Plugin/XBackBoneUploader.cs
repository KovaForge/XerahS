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
using XerahS.Uploaders.PluginSystem;

namespace ShareX.XBackBone.Plugin;

public sealed class XBackBoneUploader : FileUploader, IUploadHandler
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
        return UploadAsync(new UploadRequest
        {
            Content = stream,
            FileName = fileName,
            Category = UploaderCategory.File
        }, CancellationToken.None).GetAwaiter().GetResult().ToUploadResult();
    }

    public async Task<UploadOutcome> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default)
    {
        string normalizedServerUrl = XBackBoneClient.NormalizeServerUrl(_config.ServerUrl);

        if (!XBackBoneProvider.IsValidServerUrl(normalizedServerUrl))
        {
            return UploadOutcome.Failed("XBackBone instance URL must be a valid http:// or https:// URL.");
        }

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            return UploadOutcome.Failed("XBackBone API token is required.");
        }

        if (!Enum.IsDefined(_config.ApiGeneration))
        {
            return UploadOutcome.Failed("The selected XBackBone API generation is not supported.");
        }

        try
        {
            XBackBoneClient client = new(normalizedServerUrl, _apiToken);
            XBackBoneUploadResponse response = await client.UploadAsync(
                request.Content,
                request.FileName,
                _config.ApiGeneration,
                bytesTransferred => request.Progress?.Report(new UploadProgressReport(bytesTransferred, request.ContentLength)),
                cancellationToken).ConfigureAwait(false);

            return new UploadOutcome
            {
                Succeeded = true,
                Url = response.CanonicalUrl,
                ThumbnailUrl = response.RawUrl,
                DeletionUrl = response.DeletionUrl,
                Response = "XBackBone upload completed."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UploadOutcome.Failed(RedactToken(ex.Message));
        }
    }

    private string RedactToken(string message)
    {
        return string.IsNullOrEmpty(_apiToken)
            ? message
            : message.Replace(_apiToken, "[redacted]", StringComparison.Ordinal);
    }
}
