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

namespace XerahS.Uploaders.URLShorteners
{
    public class CustomURLShortenerService : URLShortenerService
    {
        public override UrlShortenerType EnumValue { get; } = UrlShortenerType.CustomURLShortener;

        public override bool CheckConfig(UploadersConfig config)
        {
            return config.CustomUploadersList != null && config.CustomUploadersList.IsValidIndex(config.CustomURLShortenerSelected);
        }

        public override URLShortener? CreateShortener(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            int index;

            if (taskInfo.OverrideCustomUploader)
            {
                index = taskInfo.CustomUploaderIndex.BetweenOrDefault(0, config.CustomUploadersList.Count - 1);
            }
            else
            {
                index = config.CustomURLShortenerSelected;
            }

            CustomUploaderItem? customUploader = config.CustomUploadersList.ReturnIfValidIndex(index);

            if (customUploader != null)
            {
                return new CustomURLShortener(customUploader);
            }

            return null;
        }
    }

    public sealed class CustomURLShortener : URLShortener
    {
        private CustomUploaderItem uploader;

        public CustomURLShortener(CustomUploaderItem customUploaderItem)
        {
            uploader = customUploaderItem;
        }

        public override UploadResult ShortenURL(string url)
        {
            CustomUploaderInput input = new CustomUploaderInput("", url);
            var handlers = new CustomUploaderRequestHandlers
            {
                None = () => new UploadResult
                {
                    URL = url,
                    Response = SendRequest(uploader.RequestMethod, uploader.GetRequestURL(input), null, uploader.GetHeaders(input))
                },
                MultipartFormData = () => new UploadResult
                {
                    URL = url,
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
                    Response = SendRequestURLEncoded(
                        uploader.RequestMethod,
                        uploader.GetRequestURL(input),
                        uploader.GetArguments(input),
                        uploader.GetHeaders(input))
                },
                JsonOrXml = () => new UploadResult
                {
                    URL = url,
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

            uploader.TryParseResponse(result, LastResponseInfo, Errors, input, true);

            return result;
        }
    }
}
