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

namespace XerahS.Uploaders.SharingServices
{
    public class CustomURLSharingService : URLSharingService
    {
        public override URLSharingServices EnumValue { get; } = URLSharingServices.CustomURLSharingService;

        public override bool CheckConfig(UploadersConfig config)
        {
            return config.CustomUploadersList != null && config.CustomUploadersList.IsValidIndex(config.CustomURLSharingServiceSelected);
        }

        public override URLSharer? CreateSharer(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            int index;

            if (taskInfo.OverrideCustomUploader)
            {
                index = taskInfo.CustomUploaderIndex.BetweenOrDefault(0, config.CustomUploadersList.Count - 1);
            }
            else
            {
                index = config.CustomURLSharingServiceSelected;
            }

            CustomUploaderItem? customUploader = config.CustomUploadersList.ReturnIfValidIndex(index);

            if (customUploader != null)
            {
                return new CustomURLSharer(customUploader);
            }

            return null;
        }
    }

    public sealed class CustomURLSharer : URLSharer
    {
        private CustomUploaderItem uploader;

        public CustomURLSharer(CustomUploaderItem customUploaderItem)
        {
            uploader = customUploaderItem;
        }

        public override UploadResult ShareURL(string url)
        {
            CustomUploaderInput input = new CustomUploaderInput("", url);
            var handlers = new CustomUploaderRequestHandlers
            {
                None = () => new UploadResult
                {
                    URL = url,
                    IsURLExpected = false,
                    Response = SendRequest(uploader.RequestMethod, uploader.GetRequestURL(input), null, uploader.GetHeaders(input))
                },
                MultipartFormData = () => new UploadResult
                {
                    URL = url,
                    IsURLExpected = false,
                    Response = SendRequestMultiPart(
                        uploader.GetRequestURL(input),
                        uploader.GetArguments(input),
                        uploader.GetHeaders(input),
                        null,
                        uploader.RequestMethod)
                },
                FormUrlEncoded = () => new UploadResult
                {
                    URL = url,
                    IsURLExpected = false,
                    Response = SendRequestURLEncoded(
                        uploader.RequestMethod,
                        uploader.GetRequestURL(input),
                        uploader.GetArguments(input),
                        uploader.GetHeaders(input))
                },
                JsonOrXml = () => new UploadResult
                {
                    URL = url,
                    IsURLExpected = false,
                    Response = SendRequest(
                        uploader.RequestMethod,
                        uploader.GetRequestURL(input),
                        uploader.GetData(input),
                        uploader.GetContentType(),
                        null,
                        uploader.GetHeaders(input))
                }
            };

            UploadResult result = CustomUploaderRequestExecutor.Execute(uploader.Body, handlers);

            uploader.TryParseResponse(result, LastResponseInfo, Errors, input);

            return result;
        }
    }
}
