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

using System.Text.Json.Nodes;
using XerahS.Platform.Abstractions;

namespace XerahS.McpServer.Runtime;

internal sealed class McpResourceService
{
    private readonly McpHistoryService _historyService;
    private readonly McpSettingsWorkflowService _settingsWorkflowService;

    public McpResourceService(
        McpHistoryService historyService,
        McpSettingsWorkflowService settingsWorkflowService)
    {
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _settingsWorkflowService = settingsWorkflowService ?? throw new ArgumentNullException(nameof(settingsWorkflowService));
    }

    public async Task<JsonObject> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        if (uri.StartsWith("xerahs://history/thumb/", StringComparison.OrdinalIgnoreCase))
        {
            var item = _historyService.FindItem(uri["xerahs://history/thumb/".Length..]);
            string blobPath;
            try
            {
                blobPath = McpHistoryService.ResolveBlobPath(item);
            }
            catch (FileNotFoundException)
            {
                return McpHistoryService.CreateBlobMissingResponse(uri, item);
            }

            var blobInfo = new FileInfo(blobPath);
            if (blobInfo.Length > McpHistoryService.MaxInlineBlobBytes)
            {
                return McpHistoryService.CreateBlobTooLargeResponse(uri, blobPath, blobInfo.Length);
            }

            return new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = McpJsonSerialization.GuessMimeType(blobPath),
                        ["blob"] = Convert.ToBase64String(await File.ReadAllBytesAsync(blobPath, cancellationToken))
                    })
            };
        }

        if (IsHistorySearchResourceUri(uri))
        {
            return ReadHistorySearchResource(uri);
        }

        if (uri.StartsWith("xerahs://history/", StringComparison.OrdinalIgnoreCase))
        {
            return McpJsonSerialization.CreateResource(
                uri,
                await _historyService.GetItemAsync(uri["xerahs://history/".Length..], cancellationToken));
        }

        if (uri.Equals("xerahs://capture/latest", StringComparison.OrdinalIgnoreCase))
        {
            var latest = _historyService.LoadItems().OrderByDescending(item => item.DateTime).FirstOrDefault()
                ?? throw new InvalidOperationException("No capture history is available.");
            return McpJsonSerialization.CreateResource(uri, await _historyService.CreateDetailsAsync(latest, cancellationToken));
        }

        if (uri.Equals("xerahs://workflows", StringComparison.OrdinalIgnoreCase))
        {
            return McpJsonSerialization.CreateResource(uri, _settingsWorkflowService.ListWorkflows());
        }

        if (uri.StartsWith("xerahs://workflows/", StringComparison.OrdinalIgnoreCase))
        {
            return McpJsonSerialization.CreateResource(
                uri,
                _settingsWorkflowService.GetWorkflow(uri["xerahs://workflows/".Length..]));
        }

        if (uri.StartsWith("xerahs://settings/", StringComparison.OrdinalIgnoreCase))
        {
            return McpJsonSerialization.CreateResource(
                uri,
                _settingsWorkflowService.GetSettings(uri["xerahs://settings/".Length..]));
        }

        if (uri.Equals("xerahs://monitors", StringComparison.OrdinalIgnoreCase))
        {
            var screens = PlatformServices.Screen.GetAllScreens();
            return McpJsonSerialization.CreateResource(uri, new JsonObject
            {
                ["monitors"] = new JsonArray(screens.Select((screen, index) => new JsonObject
                {
                    ["index"] = index,
                    ["device_name"] = screen.DeviceName,
                    ["is_primary"] = screen.IsPrimary,
                    ["bounds"] = McpJsonSerialization.SerializeRectangle(screen.Bounds),
                    ["working_area"] = McpJsonSerialization.SerializeRectangle(screen.WorkingArea),
                    ["scale_factor"] = screen.ScaleFactor
                }).Cast<JsonNode>().ToArray())
            });
        }

        if (uri.Equals("xerahs://destinations", StringComparison.OrdinalIgnoreCase))
        {
            return McpJsonSerialization.CreateResource(uri, _settingsWorkflowService.CreateDestinationsResource());
        }

        throw new ArgumentException($"Unknown resource URI: {uri}");
    }

    internal static bool IsHistorySearchResourceUri(string uri)
    {
        if (uri.Equals("xerahs://history/search", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        const string searchPrefix = "xerahs://history/search?";
        if (!uri.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = uri[searchPrefix.Length..];
        if (query.Length == 0)
        {
            return false;
        }

        return query.Split('&').Any(pair =>
        {
            var separator = pair.IndexOf('=');
            var key = separator >= 0 ? pair[..separator] : pair;
            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            return key.Length > 0 && HasValidPercentEncoding(key) && HasValidPercentEncoding(value);
        });
    }

    internal static string? DecodeResourceQueryComponent(string value)
    {
        if (!HasValidPercentEncoding(value))
        {
            return null;
        }

        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private JsonObject ReadHistorySearchResource(string uri)
    {
        var queryStart = uri.IndexOf('?');
        if (queryStart < 0)
        {
            return McpJsonSerialization.CreateResource(uri, _historyService.Query(null, null, null, "all", 20));
        }

        var queryString = uri[(queryStart + 1)..];
        string? query = null;
        string? fromDate = null;
        string? toDate = null;
        var limit = 20;

        var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex < 0)
            {
                continue;
            }

            var key = DecodeResourceQueryComponent(pair[..eqIndex]);
            var value = DecodeResourceQueryComponent(pair[(eqIndex + 1)..]);
            if (key == null || value == null)
            {
                continue;
            }

            if (string.Equals(key, "q", StringComparison.OrdinalIgnoreCase))
            {
                query = string.IsNullOrWhiteSpace(value) ? null : value;
            }
            else if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
            {
                fromDate = value;
            }
            else if (string.Equals(key, "to", StringComparison.OrdinalIgnoreCase))
            {
                toDate = value;
            }
            else if (string.Equals(key, "limit", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var parsedLimit))
            {
                limit = parsedLimit;
            }
        }

        return McpJsonSerialization.CreateResource(uri, _historyService.Query(query, fromDate, toDate, "all", limit));
    }

    private static bool HasValidPercentEncoding(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%')
            {
                continue;
            }

            if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2]))
            {
                return false;
            }

            i += 2;
        }

        return true;
    }
}
