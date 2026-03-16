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

using System;

namespace XerahS.Uploaders
{
    internal sealed class UnsupportedCustomUploaderBodyException : NotSupportedException
    {
        public CustomUploaderBody Body { get; }

        public UnsupportedCustomUploaderBodyException(CustomUploaderBody body)
            : base($"Unsupported request format: {body}")
        {
            Body = body;
        }
    }

    internal sealed class CustomUploaderRequestHandlers
    {
        public Func<UploadResult>? None { get; init; }
        public Func<UploadResult>? MultipartFormData { get; init; }
        public Func<UploadResult>? FormUrlEncoded { get; init; }
        public Func<UploadResult>? JsonOrXml { get; init; }
        public Func<UploadResult>? Binary { get; init; }
    }

    internal static class CustomUploaderRequestExecutor
    {
        public static UploadResult Execute(CustomUploaderBody body, CustomUploaderRequestHandlers handlers)
        {
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            Func<UploadResult>? handler = body switch
            {
                CustomUploaderBody.None => handlers.None,
                CustomUploaderBody.MultipartFormData => handlers.MultipartFormData,
                CustomUploaderBody.FormURLEncoded => handlers.FormUrlEncoded,
                CustomUploaderBody.JSON => handlers.JsonOrXml,
                CustomUploaderBody.XML => handlers.JsonOrXml,
                CustomUploaderBody.Binary => handlers.Binary,
                _ => null
            };

            if (handler == null)
            {
                throw new UnsupportedCustomUploaderBodyException(body);
            }

            return handler();
        }
    }
}
