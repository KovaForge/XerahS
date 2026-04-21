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
using System.Diagnostics;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Nextcloud.Plugin.ViewModels;

public partial class NextcloudConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string _loginName = string.Empty;

    [ObservableProperty]
    private string _appPassword = string.Empty;

    [ObservableProperty]
    private string _userId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _remotePath = "ShareX/%y/%mo";

    [ObservableProperty]
    private bool _createPublicShare = true;

    [ObservableProperty]
    private bool _autoExpireShare;

    [ObservableProperty]
    private int _expireAfterDays = 7;

    [ObservableProperty]
    private bool _useChunkedUpload = true;

    [ObservableProperty]
    private int _chunkSizeMiB = 10;

    [ObservableProperty]
    private string _sharePassword = string.Empty;

    [ObservableProperty]
    private string _serverVersion = string.Empty;

    [ObservableProperty]
    private string _serverProductName = "Nextcloud";

    [ObservableProperty]
    private string _themingName = string.Empty;

    [ObservableProperty]
    private bool _supportsPublicShares = true;

    [ObservableProperty]
    private bool _supportsSharePasswords;

    [ObservableProperty]
    private bool _supportsExpireDate;

    [ObservableProperty]
    private bool _supportsChunking;

    [ObservableProperty]
    private bool _supportsSearch;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _hasPendingLoginFlow;

    [ObservableProperty]
    private string _connectionSummary = "No Nextcloud account connected.";

    [ObservableProperty]
    private string _capabilitiesSummary = "Capabilities will appear after profile refresh.";

    [ObservableProperty]
    private string? _statusMessage;

    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;
    private NextcloudConfigModel _config = new();
    private string _pendingLoginUrl = string.Empty;
    private string _pendingPollEndpoint = string.Empty;
    private string _pendingPollToken = string.Empty;

    partial void OnCreatePublicShareChanged(bool value)
    {
        if (!value)
        {
            AutoExpireShare = false;
            SharePassword = string.Empty;
        }
    }

    [RelayCommand]
    private async Task StartBrowserLoginAsync()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            StatusMessage = error;
            return;
        }

        IsBusy = true;
        StatusMessage = "Starting Nextcloud Login Flow v2...";

        try
        {
            NextcloudLoginFlowState loginFlow = await NextcloudClient.StartLoginFlowAsync(normalizedServerUrl);
            ServerUrl = normalizedServerUrl;
            _pendingLoginUrl = loginFlow.LoginUrl;
            _pendingPollEndpoint = loginFlow.PollEndpoint;
            _pendingPollToken = loginFlow.PollToken;
            HasPendingLoginFlow = true;
            StatusMessage = "Approve access in your browser, then click Finish Browser Login.";
            OpenUrl(loginFlow.LoginUrl);
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to start Nextcloud browser login: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenBrowserAgain()
    {
        if (string.IsNullOrWhiteSpace(_pendingLoginUrl))
        {
            StatusMessage = "Start browser login first.";
            return;
        }

        OpenUrl(_pendingLoginUrl);
        StatusMessage = "Re-opened the Nextcloud login page.";
    }

    [RelayCommand]
    private async Task FinishBrowserLoginAsync()
    {
        if (!HasPendingLoginFlow || string.IsNullOrWhiteSpace(_pendingPollEndpoint) || string.IsNullOrWhiteSpace(_pendingPollToken))
        {
            StatusMessage = "Start browser login first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Waiting for Nextcloud to return an app password...";

        try
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(3);

            while (DateTimeOffset.UtcNow < deadline)
            {
                NextcloudLoginResult? result = await NextcloudClient.PollLoginFlowAsync(_pendingPollEndpoint, _pendingPollToken);
                if (result != null)
                {
                    ServerUrl = result.ServerUrl;
                    LoginName = result.LoginName;
                    AppPassword = result.AppPassword;
                    PersistSecrets();
                    ClearPendingLoginFlow();
                    await RefreshServerProfileCoreAsync();
                    IsConnected = true;
                    StatusMessage = "Nextcloud browser login completed.";
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            StatusMessage = "Nextcloud browser login timed out. If you already approved access, click Finish Browser Login again.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Nextcloud browser login failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshServerProfileAsync()
    {
        IsBusy = true;

        try
        {
            await RefreshServerProfileCoreAsync();
            StatusMessage = "Nextcloud server profile refreshed.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to refresh Nextcloud server profile: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenServer()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            StatusMessage = error;
            return;
        }

        OpenUrl(normalizedServerUrl);
    }

    [RelayCommand]
    private void OpenSecuritySettings()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            StatusMessage = error;
            return;
        }

        OpenUrl(normalizedServerUrl + "/index.php/settings/user/security");
    }

    [RelayCommand]
    private void ClearStoredCredentials()
    {
        if (_secrets != null)
        {
            _secrets.DeleteSecret("nextcloud", _secretKey, "appPassword");
            _secrets.DeleteSecret("nextcloud", _secretKey, "sharePassword");
        }

        AppPassword = string.Empty;
        SharePassword = string.Empty;
        DisplayName = string.Empty;
        UserId = string.Empty;
        ServerVersion = string.Empty;
        ServerProductName = "Nextcloud";
        ThemingName = string.Empty;
        SupportsPublicShares = false;
        SupportsSharePasswords = false;
        SupportsExpireDate = false;
        SupportsChunking = false;
        SupportsSearch = false;
        IsConnected = false;
        ClearPendingLoginFlow();
        UpdateConnectionSummary();
        UpdateCapabilitiesSummary();
        StatusMessage = "Stored Nextcloud credentials were cleared.";
    }

    public void LoadFromJson(string json)
    {
        try
        {
            NextcloudConfigModel? config = JsonConvert.DeserializeObject<NextcloudConfigModel>(json);
            if (config == null)
            {
                return;
            }

            _config = config;
            _secretKey = string.IsNullOrWhiteSpace(config.SecretKey) ? Guid.NewGuid().ToString("N") : config.SecretKey;
            ServerUrl = config.ServerUrl ?? string.Empty;
            string normalizedLoginName = NormalizeLoginIdentity(config.LoginName, config.UserId);
            LoginName = normalizedLoginName;
            UserId = string.IsNullOrWhiteSpace(config.UserId) ? normalizedLoginName : config.UserId;
            DisplayName = config.DisplayName ?? string.Empty;
            RemotePath = string.IsNullOrWhiteSpace(config.RemotePath) ? "ShareX/%y/%mo" : config.RemotePath;
            CreatePublicShare = config.CreatePublicShare;
            AutoExpireShare = config.AutoExpireShare;
            ExpireAfterDays = config.ExpireAfterDays <= 0 ? 7 : config.ExpireAfterDays;
            UseChunkedUpload = config.UseChunkedUpload;
            ChunkSizeMiB = config.ChunkSizeMiB <= 0 ? 10 : config.ChunkSizeMiB;
            ServerVersion = config.ServerVersion ?? string.Empty;
            ServerProductName = string.IsNullOrWhiteSpace(config.ServerProductName) ? "Nextcloud" : config.ServerProductName;
            ThemingName = config.ThemingName ?? string.Empty;
            SupportsPublicShares = config.SupportsPublicShares;
            SupportsSharePasswords = config.SupportsSharePasswords;
            SupportsExpireDate = config.SupportsExpireDate;
            SupportsChunking = config.SupportsChunking;
            SupportsSearch = config.SupportsSearch;
            LoadSecretsFromStore();
            UpdateConnectionState();
        }
        catch
        {
            StatusMessage = "Failed to load Nextcloud configuration.";
        }
    }

    public string ToJson()
    {
        if (TryNormalizeServerUrl(out string normalizedServerUrl, out _))
        {
            ServerUrl = normalizedServerUrl;
        }

        PersistSecrets();

        _config = new NextcloudConfigModel
        {
            SecretKey = _secretKey,
            ServerUrl = ServerUrl,
            LoginName = LoginName,
            UserId = string.IsNullOrWhiteSpace(UserId) ? LoginName : UserId,
            DisplayName = DisplayName,
            RemotePath = RemotePath,
            CreatePublicShare = CreatePublicShare,
            AutoExpireShare = CreatePublicShare && AutoExpireShare,
            ExpireAfterDays = ExpireAfterDays,
            UseChunkedUpload = UseChunkedUpload,
            ChunkSizeMiB = ChunkSizeMiB,
            ServerVersion = ServerVersion,
            ServerProductName = ServerProductName,
            ThemingName = ThemingName,
            SupportsPublicShares = SupportsPublicShares,
            SupportsSharePasswords = SupportsSharePasswords,
            SupportsExpireDate = SupportsExpireDate,
            SupportsChunking = SupportsChunking,
            SupportsSearch = SupportsSearch
        };

        return JsonConvert.SerializeObject(_config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? urlError))
        {
            StatusMessage = urlError;
            return false;
        }

        LoginName = NormalizeLoginIdentity(LoginName, UserId);
        if (string.IsNullOrWhiteSpace(LoginName))
        {
            StatusMessage = "Nextcloud login name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(UserId))
        {
            UserId = LoginName;
        }

        if (string.IsNullOrWhiteSpace(AppPassword))
        {
            StatusMessage = "Nextcloud app password is required. Use browser login or create an app password in Nextcloud security settings.";
            return false;
        }

        if (CreatePublicShare && AutoExpireShare && ExpireAfterDays <= 0)
        {
            StatusMessage = "Share expiry must be at least 1 day.";
            return false;
        }

        ServerUrl = normalizedServerUrl;
        PersistSecrets();
        StatusMessage = null;
        return true;
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;
        LoadSecretsFromStore();
        UpdateConnectionState();
    }

    private async Task RefreshServerProfileCoreAsync()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            throw new InvalidOperationException(error);
        }

        if (string.IsNullOrWhiteSpace(LoginName) || string.IsNullOrWhiteSpace(AppPassword))
        {
            throw new InvalidOperationException("Nextcloud login name and app password are required.");
        }

        PersistSecrets();
        NextcloudServerProfile profile = await new NextcloudClient(normalizedServerUrl, LoginName, AppPassword).GetServerProfileAsync();
        ApplyProfile(profile);
    }

    private void ApplyProfile(NextcloudServerProfile profile)
    {
        ServerUrl = profile.ServerUrl;
        LoginName = profile.LoginName;
        UserId = profile.UserId;
        DisplayName = profile.DisplayName;
        ServerVersion = profile.ServerVersion;
        ServerProductName = profile.ServerProductName;
        ThemingName = profile.ThemingName;
        SupportsPublicShares = profile.SupportsPublicShares;
        SupportsSharePasswords = profile.SupportsSharePasswords;
        SupportsExpireDate = profile.SupportsExpireDate;
        SupportsChunking = profile.SupportsChunking;
        SupportsSearch = profile.SupportsSearch;
        IsConnected = !string.IsNullOrWhiteSpace(LoginName) && !string.IsNullOrWhiteSpace(AppPassword);
        UpdateConnectionSummary();
        UpdateCapabilitiesSummary();
    }

    private void UpdateConnectionState()
    {
        LoginName = NormalizeLoginIdentity(LoginName, UserId);
        if (string.IsNullOrWhiteSpace(UserId))
        {
            UserId = LoginName;
        }

        IsConnected = !string.IsNullOrWhiteSpace(LoginName) && !string.IsNullOrWhiteSpace(AppPassword);
        UpdateConnectionSummary();
        UpdateCapabilitiesSummary();
    }

    private static string NormalizeLoginIdentity(string? loginName, string? userId)
    {
        string normalizedLoginName = loginName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedLoginName))
        {
            return normalizedLoginName;
        }

        return userId?.Trim() ?? string.Empty;
    }

    private void UpdateConnectionSummary()
    {
        if (!IsConnected)
        {
            ConnectionSummary = "No Nextcloud account connected.";
            return;
        }

        string identity = !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : LoginName;
        string userIdSummary = !string.IsNullOrWhiteSpace(UserId) ? $"User ID: {UserId}" : $"Login: {LoginName}";
        string versionSummary = !string.IsNullOrWhiteSpace(ServerVersion) ? $" · {ServerProductName} {ServerVersion}" : string.Empty;
        ConnectionSummary = $"{identity} · {userIdSummary}{versionSummary}";
    }

    private void UpdateCapabilitiesSummary()
    {
        List<string> capabilities = new();

        if (SupportsChunking)
        {
            capabilities.Add("chunked uploads");
        }

        if (SupportsPublicShares)
        {
            capabilities.Add("public links");
        }

        if (SupportsExpireDate)
        {
            capabilities.Add("share expiry");
        }

        if (SupportsSharePasswords)
        {
            capabilities.Add("share passwords");
        }

        if (SupportsSearch)
        {
            capabilities.Add("search");
        }

        if (capabilities.Count == 0)
        {
            CapabilitiesSummary = "Capabilities will appear after profile refresh.";
            return;
        }

        CapabilitiesSummary = "Detected: " + string.Join(", ", capabilities) + ".";
    }

    private void LoadSecretsFromStore()
    {
        if (_secrets == null || string.IsNullOrWhiteSpace(_secretKey))
        {
            return;
        }

        AppPassword = _secrets.GetSecret("nextcloud", _secretKey, "appPassword") ?? AppPassword;
        SharePassword = _secrets.GetSecret("nextcloud", _secretKey, "sharePassword") ?? SharePassword;
    }

    private void PersistSecrets()
    {
        if (_secrets == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AppPassword))
        {
            _secrets.DeleteSecret("nextcloud", _secretKey, "appPassword");
        }
        else
        {
            _secrets.SetSecret("nextcloud", _secretKey, "appPassword", AppPassword);
        }

        if (string.IsNullOrWhiteSpace(SharePassword))
        {
            _secrets.DeleteSecret("nextcloud", _secretKey, "sharePassword");
        }
        else
        {
            _secrets.SetSecret("nextcloud", _secretKey, "sharePassword", SharePassword);
        }
    }

    private void ClearPendingLoginFlow()
    {
        _pendingLoginUrl = string.Empty;
        _pendingPollEndpoint = string.Empty;
        _pendingPollToken = string.Empty;
        HasPendingLoginFlow = false;
    }

    private bool TryNormalizeServerUrl(out string normalizedServerUrl, out string? error)
    {
        normalizedServerUrl = NextcloudClient.NormalizeServerUrl(ServerUrl);
        error = null;

        if (string.IsNullOrWhiteSpace(normalizedServerUrl))
        {
            error = "Nextcloud server URL is required.";
            return false;
        }

        if (!Uri.TryCreate(normalizedServerUrl, UriKind.Absolute, out Uri? uri) ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Nextcloud server URL must be a valid http:// or https:// URL.";
            return false;
        }

        return true;
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                URLHelpers.OpenURL(url);
            }
        }
        catch
        {
        }
    }
}
