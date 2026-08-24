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

public sealed class UploadOutcome
{
    public bool Succeeded { get; init; }
    public string? Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? DeletionUrl { get; init; }
    public string? ShortenedUrl { get; init; }
    public string? Response { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public bool Retryable { get; init; }
    public bool UrlExpected { get; init; } = true;

    public static UploadOutcome Success(string? url, string? response = null, bool urlExpected = true) => new()
    {
        Succeeded = true,
        Url = url,
        Response = response,
        UrlExpected = urlExpected
    };

    public static UploadOutcome Failed(string error, string? errorCode = null, bool retryable = false) => new()
    {
        Succeeded = false,
        Error = error,
        ErrorCode = errorCode,
        Retryable = retryable,
        UrlExpected = true
    };
}
