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

namespace XerahS.Uploaders.PluginSystem;

public static class UploadOutcomeMapper
{
    public static UploadResult ToUploadResult(this UploadOutcome outcome)
    {
        UploadResult result = new()
        {
            URL = outcome.Url,
            ThumbnailURL = outcome.ThumbnailUrl,
            DeletionURL = outcome.DeletionUrl,
            ShortenedURL = outcome.ShortenedUrl,
            Response = outcome.Response ?? outcome.Error,
            IsURLExpected = outcome.UrlExpected,
            IsSuccess = outcome.Succeeded
        };

        if (!outcome.Succeeded && !string.IsNullOrWhiteSpace(outcome.Error))
        {
            result.Errors.Add(outcome.Error);
        }

        return result;
    }

    public static UploadOutcome FromUploadResult(UploadResult result)
    {
        bool succeeded = result != null && (result.IsSuccess || (!result.IsError && !string.IsNullOrWhiteSpace(result.URL)));
        if (result == null)
        {
            return UploadOutcome.Failed("Uploader returned no result.");
        }

        if (!succeeded)
        {
            string error = result.ErrorsToString();
            if (string.IsNullOrWhiteSpace(error))
            {
                error = result.Response ?? "Upload failed.";
            }

            return UploadOutcome.Failed(error);
        }

        return new UploadOutcome
        {
            Succeeded = true,
            Url = result.URL,
            ThumbnailUrl = result.ThumbnailURL,
            DeletionUrl = result.DeletionURL,
            ShortenedUrl = result.ShortenedURL,
            Response = result.Response,
            UrlExpected = result.IsURLExpected
        };
    }
}
