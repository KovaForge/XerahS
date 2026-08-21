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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.XBackBone.Plugin.ViewModels;

public partial class XBackBoneConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private int _apiGenerationIndex;

    [ObservableProperty]
    private string _apiToken = string.Empty;

    [ObservableProperty]
    private string _tokenSummary = "No API token is stored.";

    [ObservableProperty]
    private string? _statusMessage;

    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;

    public string[] ApiGenerations { get; } =
    {
        "XBackBone 3.x (stable releases)",
        "API v1 (next-generation)"
    };

    partial void OnApiTokenChanged(string value)
    {
        TokenSummary = string.IsNullOrWhiteSpace(value)
            ? "No API token is stored."
            : "An API token is configured and will be stored securely.";
    }

    [RelayCommand]
    private void ClearStoredToken()
    {
        _secrets?.DeleteSecret("xbackbone", _secretKey, "apiToken");
        ApiToken = string.Empty;
        StatusMessage = "Stored XBackBone API token was cleared.";
    }

    public void LoadFromJson(string json)
    {
        try
        {
            XBackBoneConfigModel? config = JsonConvert.DeserializeObject<XBackBoneConfigModel>(json);
            if (config == null)
            {
                return;
            }

            _secretKey = string.IsNullOrWhiteSpace(config.SecretKey) ? Guid.NewGuid().ToString("N") : config.SecretKey;
            ServerUrl = config.ServerUrl ?? string.Empty;
            ApiGenerationIndex = config.ApiGeneration switch
            {
                XBackBoneApiGeneration.Stable3 => 0,
                XBackBoneApiGeneration.ApiV1 => 1,
                _ => -1
            };
            LoadTokenFromStore();
            StatusMessage = null;
        }
        catch (JsonException)
        {
            StatusMessage = "Failed to load XBackBone configuration.";
        }
    }

    public string ToJson()
    {
        string normalizedServerUrl = XBackBoneClient.NormalizeServerUrl(ServerUrl);
        if (XBackBoneProvider.IsValidServerUrl(normalizedServerUrl))
        {
            ServerUrl = normalizedServerUrl;
        }

        PersistToken();

        XBackBoneConfigModel config = new()
        {
            SecretKey = _secretKey,
            ServerUrl = ServerUrl,
            ApiGeneration = GetSelectedApiGeneration()
        };

        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public bool Validate()
    {
        string normalizedServerUrl = XBackBoneClient.NormalizeServerUrl(ServerUrl);
        if (!XBackBoneProvider.IsValidServerUrl(normalizedServerUrl))
        {
            StatusMessage = "XBackBone instance URL must be a valid http:// or https:// URL.";
            return false;
        }

        if (ApiGenerationIndex is < 0 or > 1)
        {
            StatusMessage = "Choose a supported XBackBone API generation.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            StatusMessage = "XBackBone API token is required.";
            return false;
        }

        ServerUrl = normalizedServerUrl;
        PersistToken();
        StatusMessage = null;
        return true;
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;
        LoadTokenFromStore();
    }

    private XBackBoneApiGeneration GetSelectedApiGeneration()
    {
        return ApiGenerationIndex switch
        {
            0 => XBackBoneApiGeneration.Stable3,
            1 => XBackBoneApiGeneration.ApiV1,
            _ => (XBackBoneApiGeneration)(-1)
        };
    }

    private void LoadTokenFromStore()
    {
        if (_secrets == null || string.IsNullOrWhiteSpace(_secretKey))
        {
            return;
        }

        ApiToken = _secrets.GetSecret("xbackbone", _secretKey, "apiToken") ?? string.Empty;
    }

    private void PersistToken()
    {
        if (_secrets == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            _secrets.DeleteSecret("xbackbone", _secretKey, "apiToken");
        }
        else
        {
            _secrets.SetSecret("xbackbone", _secretKey, "apiToken", ApiToken);
        }
    }
}
