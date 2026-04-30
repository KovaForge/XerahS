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

namespace ShareX.Pastebin.Plugin;

/// <summary>
/// Pastebin text uploader - supports title, syntax, privacy, expiration, optional login
/// </summary>
public sealed class PastebinUploader : TextUploader
{
    private readonly PastebinConfigModel _config;

    public PastebinUploader(PastebinConfigModel config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override UploadResult UploadText(string text, string fileName)
    {
        UploadResult result = new UploadResult();

        if (string.IsNullOrEmpty(text) || _config == null)
        {
            return result;
        }

        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            Errors.Add("Pastebin API key is required. Get one at https://pastebin.com/doc_api");
            return result;
        }

        Dictionary<string, string> args = new Dictionary<string, string>
        {
            ["api_dev_key"] = _config.ApiKey,
            ["api_option"] = "paste",
            ["api_paste_code"] = text,
            ["api_paste_name"] = _config.Title ?? string.Empty,
            ["api_paste_format"] = string.IsNullOrWhiteSpace(_config.TextFormat) ? "text" : _config.TextFormat,
            ["api_paste_private"] = GetPrivacy(_config.Exposure),
            ["api_paste_expire_date"] = GetExpiration(_config.Expiration)
        };

        if (!string.IsNullOrEmpty(_config.UserKey))
        {
            args["api_user_key"] = _config.UserKey;
        }

        string? response = SendRequestMultiPart("https://pastebin.com/api/api_post.php", args);
        result.Response = response;

        if (!string.IsNullOrEmpty(response) && !response.StartsWith("Bad API request"))
        {
            bool isValidUrl = Uri.TryCreate(response, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https");
            if (isValidUrl)
            {
                string pasteKey = GetUrlLastSegment(response);

                if (_config.RawURL)
                {
                    result.URL = "https://pastebin.com/raw/" + pasteKey;
                }
                else
                {
                    result.URL = response;
                }

                AddDeletionMetadata(result, pasteKey);
            }
            else
            {
                Errors.Add(response);
            }
        }
        else if (!string.IsNullOrEmpty(response))
        {
            Errors.Add(response);
        }

        return result;
    }

    private void AddDeletionMetadata(UploadResult result, string pasteKey)
    {
        if (string.IsNullOrWhiteSpace(pasteKey))
        {
            return;
        }

        result.Metadata["Deletion.Provider"] = "Pastebin";
        result.Metadata["Deletion.PasteKey"] = pasteKey;

        if (!string.IsNullOrWhiteSpace(_config.UserKey))
        {
            // Pastebin does not return a one-click delete URL. Deletion is a POST to this endpoint
            // with api_option=delete, api_paste_key, api_dev_key, and api_user_key from the configured account.
            result.DeletionURL = "https://pastebin.com/api/api_post.php";
            result.Metadata["Deletion.Method"] = "POST";
            result.Metadata["Deletion.ApiOption"] = "delete";
            result.Metadata["Deletion.RequiresConfiguredAccount"] = "true";
        }
        else
        {
            result.Metadata["Deletion.Available"] = "false";
            result.Metadata["Deletion.Reason"] = "Pastebin guest pastes do not expose API deletion; create pastes with an api_user_key to enable deletion.";
        }
    }

    private static string GetPrivacy(PastebinPrivacy privacy)
    {
        return privacy switch
        {
            PastebinPrivacy.Public => "0",
            PastebinPrivacy.Private => "2",
            _ => "1" // Unlisted
        };
    }

    private static string GetExpiration(PastebinExpiration expiration)
    {
        return expiration switch
        {
            PastebinExpiration.M10 => "10M",
            PastebinExpiration.H1 => "1H",
            PastebinExpiration.D1 => "1D",
            PastebinExpiration.W1 => "1W",
            PastebinExpiration.W2 => "2W",
            PastebinExpiration.M1 => "1M",
            _ => "N"
        };
    }

    private static string GetUrlLastSegment(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        int lastSlash = url.TrimEnd('/').LastIndexOf('/');
        string segment = lastSlash >= 0 ? url.Substring(lastSlash + 1) : url;
        int q = segment.IndexOf('?');
        if (q >= 0) segment = segment.Remove(q);
        int h = segment.IndexOf('#');
        if (h >= 0) segment = segment.Remove(h);
        return segment;
    }
}
