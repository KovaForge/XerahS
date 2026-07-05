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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Immich.Plugin.ViewModels;

public partial class ImmichConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    [ObservableProperty]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private int _authModeIndex;

    [ObservableProperty]
    private string _apiKeyName = "XerahS Uploads";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _bootstrapEmail = string.Empty;

    [ObservableProperty]
    private string _bootstrapPassword = string.Empty;

    [ObservableProperty]
    private string _userId = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _serverVersion = string.Empty;

    [ObservableProperty]
    private string _externalDomain = string.Empty;

    [ObservableProperty]
    private bool _passwordLoginEnabled = true;

    [ObservableProperty]
    private bool _oAuthEnabled;

    [ObservableProperty]
    private bool _searchEnabled;

    [ObservableProperty]
    private bool _duplicateDetectionEnabled = true;

    [ObservableProperty]
    private bool _sidecarSupported;

    [ObservableProperty]
    private bool _addToAlbum;

    [ObservableProperty]
    private bool _autoCreateAlbum = true;

    [ObservableProperty]
    private bool _useDuplicateCheck = true;

    [ObservableProperty]
    private string _albumName = "ShareX Uploads";

    [ObservableProperty]
    private ImmichAlbumOption? _selectedAlbum;

    [ObservableProperty]
    private int _shareModeIndex;

    [ObservableProperty]
    private string _shareSlug = string.Empty;

    [ObservableProperty]
    private bool _useShareExpiry;

    [ObservableProperty]
    private int _expireAfterDays = 7;

    [ObservableProperty]
    private bool _allowShareDownload = true;

    [ObservableProperty]
    private bool _allowShareUpload;

    [ObservableProperty]
    private bool _showMetadata = true;

    [ObservableProperty]
    private string _sharePassword = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionSummary = "No Immich API key configured yet.";

    [ObservableProperty]
    private string _capabilitiesSummary = "Server capabilities will appear after verification.";

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<ImmichAlbumOption> Albums { get; } = new();

    public string[] AuthModes { get; } =
    {
        "Use existing API key",
        "Create scoped API key"
    };

    public string[] ShareModes { get; } =
    {
        "No shared link",
        "Share uploaded asset",
        "Share destination album"
    };

    public bool IsApiKeyMode => AuthModeIndex == 0;
    public bool IsBootstrapMode => AuthModeIndex == 1;
    public bool IsShareEnabled => ShareModeIndex > 0;
    public bool IsAlbumShareMode => ShareModeIndex == 2;
    public bool CanEditAlbumPicker => AddToAlbum;
    public bool CanCreateApiKey => IsBootstrapMode && PasswordLoginEnabled;

    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;
    private ImmichConfigModel _config = new();

    partial void OnAuthModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsApiKeyMode));
        OnPropertyChanged(nameof(IsBootstrapMode));
        OnPropertyChanged(nameof(CanCreateApiKey));
        StatusMessage = null;
    }

    partial void OnShareModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsShareEnabled));
        OnPropertyChanged(nameof(IsAlbumShareMode));

        if (!IsShareEnabled)
        {
            UseShareExpiry = false;
            ShareSlug = string.Empty;
            SharePassword = string.Empty;
        }

        if (IsAlbumShareMode)
        {
            AddToAlbum = true;
        }
    }

    partial void OnAddToAlbumChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditAlbumPicker));

        if (!value)
        {
            SelectedAlbum = null;
            AlbumName = string.Empty;

            if (IsAlbumShareMode)
            {
                ShareModeIndex = 1;
            }
        }
    }

    partial void OnSelectedAlbumChanged(ImmichAlbumOption? value)
    {
        if (value != null)
        {
            AlbumName = value.Name;
        }
    }

    partial void OnIsConnectedChanged(bool value)
    {
        UpdateConnectionSummary();
    }

    [RelayCommand]
    private async Task VerifyConnectionAsync()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            StatusMessage = error;
            return;
        }

        IsBusy = true;
        StatusMessage = "Verifying Immich server and capabilities...";

        try
        {
            PersistSecrets();

            ImmichServerProfile profile = await new ImmichClient(normalizedServerUrl, ApiKey).GetServerProfileAsync();
            ApplyProfile(profile);
            IsConnected = !string.IsNullOrWhiteSpace(ApiKey);
            StatusMessage = IsConnected
                ? "Immich connection verified."
                : "Immich server detected. Add or create an API key to finish connecting.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Immich verification failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateScopedApiKeyAsync()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            StatusMessage = error;
            return;
        }

        if (string.IsNullOrWhiteSpace(BootstrapEmail) || string.IsNullOrWhiteSpace(BootstrapPassword))
        {
            StatusMessage = "Immich email and password are required to create a scoped API key.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Signing in to Immich and creating a scoped API key...";

        try
        {
            string createdApiKey = await new ImmichClient(normalizedServerUrl, string.Empty)
                .CreateScopedApiKeyAsync(BootstrapEmail, BootstrapPassword, ApiKeyName);

            ApiKey = createdApiKey;
            AuthModeIndex = 0;
            BootstrapPassword = string.Empty;
            PersistSecrets();

            ImmichServerProfile profile = await new ImmichClient(normalizedServerUrl, createdApiKey).GetServerProfileAsync();
            ApplyProfile(profile);
            BootstrapEmail = string.IsNullOrWhiteSpace(profile.UserEmail) ? BootstrapEmail : profile.UserEmail;
            IsConnected = true;
            StatusMessage = "Immich API key created and stored.";

            await RefreshAlbumsCoreAsync(updateStatus: false);
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to create Immich API key: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAlbumsAsync()
    {
        if (!TryNormalizeServerUrl(out _, out string? error))
        {
            StatusMessage = error;
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "An Immich API key is required before albums can be loaded.";
            return;
        }

        IsBusy = true;

        try
        {
            await RefreshAlbumsCoreAsync(updateStatus: true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load Immich albums: " + ex.Message;
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
    private void ClearStoredCredentials()
    {
        if (_secrets != null)
        {
            _secrets.DeleteSecret("immich", _secretKey, "apiKey");
            _secrets.DeleteSecret("immich", _secretKey, "apiToken");
            _secrets.DeleteSecret("immich", _secretKey, "sharePassword");
        }

        ApiKey = string.Empty;
        SharePassword = string.Empty;
        BootstrapPassword = string.Empty;
        UserId = string.Empty;
        UserName = string.Empty;
        UserEmail = string.Empty;
        IsConnected = false;
        StatusMessage = "Stored Immich credentials were cleared.";
    }

    public void LoadFromJson(string json)
    {
        try
        {
            ImmichConfigModel? config = JsonConvert.DeserializeObject<ImmichConfigModel>(json);
            if (config == null)
            {
                return;
            }

            _config = config;
            _secretKey = string.IsNullOrWhiteSpace(config.SecretKey) ? Guid.NewGuid().ToString("N") : config.SecretKey;
            ServerUrl = config.ServerUrl ?? string.Empty;
            AuthModeIndex = config.AuthMode == ImmichAuthMode.BootstrapApiKey ? 1 : 0;
            ApiKeyName = string.IsNullOrWhiteSpace(config.ApiKeyName) ? "XerahS Uploads" : config.ApiKeyName;
            UserId = config.UserId ?? string.Empty;
            UserName = config.UserName ?? string.Empty;
            UserEmail = config.UserEmail ?? string.Empty;
            BootstrapEmail = UserEmail;
            ServerVersion = config.ServerVersion ?? string.Empty;
            ExternalDomain = config.ExternalDomain ?? string.Empty;
            PasswordLoginEnabled = config.PasswordLoginEnabled;
            OAuthEnabled = config.OAuthEnabled;
            SearchEnabled = config.SearchEnabled;
            DuplicateDetectionEnabled = config.DuplicateDetectionEnabled;
            SidecarSupported = config.SidecarSupported;
            AddToAlbum = config.AddToAlbum;
            AlbumName = config.AlbumName ?? string.Empty;
            AutoCreateAlbum = config.AutoCreateAlbum;
            UseDuplicateCheck = config.UseDuplicateCheck;
            ShareModeIndex = config.ShareMode switch
            {
                ImmichShareMode.Asset => 1,
                ImmichShareMode.Album => 2,
                _ => 0
            };
            ShareSlug = config.ShareSlug ?? string.Empty;
            UseShareExpiry = config.UseShareExpiry;
            ExpireAfterDays = config.ExpireAfterDays <= 0 ? 7 : config.ExpireAfterDays;
            AllowShareDownload = config.AllowShareDownload;
            AllowShareUpload = config.AllowShareUpload;
            ShowMetadata = config.ShowMetadata;

            SetAlbumOptionsPlaceholder(config.AlbumId, config.AlbumName);
            LoadSecretsFromStore();
            UpdateConnectionState();
        }
        catch
        {
            StatusMessage = "Failed to load Immich configuration.";
        }
    }

    public string ToJson()
    {
        if (TryNormalizeServerUrl(out string normalizedServerUrl, out _))
        {
            ServerUrl = normalizedServerUrl;
        }

        PersistSecrets();
        SyncSelectedAlbumIntoFields();

        _config = new ImmichConfigModel
        {
            SecretKey = _secretKey,
            ServerUrl = ServerUrl,
            AuthMode = IsBootstrapMode ? ImmichAuthMode.BootstrapApiKey : ImmichAuthMode.ApiKey,
            ApiKeyName = ApiKeyName,
            UserId = UserId,
            UserName = UserName,
            UserEmail = UserEmail,
            ServerVersion = ServerVersion,
            ExternalDomain = ExternalDomain,
            PasswordLoginEnabled = PasswordLoginEnabled,
            OAuthEnabled = OAuthEnabled,
            SearchEnabled = SearchEnabled,
            DuplicateDetectionEnabled = DuplicateDetectionEnabled,
            SidecarSupported = SidecarSupported,
            AddToAlbum = AddToAlbum,
            AlbumId = SelectedAlbum?.Id ?? string.Empty,
            AlbumName = AlbumName,
            AutoCreateAlbum = AutoCreateAlbum,
            UseDuplicateCheck = UseDuplicateCheck,
            ShareMode = ShareModeIndex switch
            {
                1 => ImmichShareMode.Asset,
                2 => ImmichShareMode.Album,
                _ => ImmichShareMode.None
            },
            ShareSlug = ShareSlug,
            UseShareExpiry = IsShareEnabled && UseShareExpiry,
            ExpireAfterDays = ExpireAfterDays <= 0 ? 7 : ExpireAfterDays,
            AllowShareDownload = AllowShareDownload,
            AllowShareUpload = AllowShareUpload,
            ShowMetadata = ShowMetadata
        };

        return JsonConvert.SerializeObject(_config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            StatusMessage = error;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "Immich API key is required.";
            return false;
        }

        if (AddToAlbum && string.IsNullOrWhiteSpace(SelectedAlbum?.Id) && string.IsNullOrWhiteSpace(AlbumName))
        {
            StatusMessage = "Choose an Immich album or enter an album name.";
            return false;
        }

        if (AddToAlbum && AutoCreateAlbum && string.IsNullOrWhiteSpace(AlbumName))
        {
            StatusMessage = "Album auto-create needs an album name.";
            return false;
        }

        if (IsAlbumShareMode && !AddToAlbum)
        {
            StatusMessage = "Album sharing requires an album destination.";
            return false;
        }

        if (IsShareEnabled && UseShareExpiry && ExpireAfterDays <= 0)
        {
            StatusMessage = "Shared link expiry must be at least 1 day.";
            return false;
        }

        ServerUrl = normalizedServerUrl;
        PersistSecrets();
        SyncSelectedAlbumIntoFields();
        StatusMessage = null;
        return true;
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;
        LoadSecretsFromStore();
        UpdateConnectionState();
    }

    private async Task RefreshAlbumsCoreAsync(bool updateStatus)
    {
        if (!TryNormalizeServerUrl(out string normalizedServerUrl, out string? error))
        {
            throw new InvalidOperationException(error);
        }

        ImmichClient client = new(normalizedServerUrl, ApiKey);
        IReadOnlyList<ImmichAlbum> albums = await client.GetAlbumsAsync();

        Albums.Clear();
        foreach (ImmichAlbum album in albums.OrderByDescending(album => album.UpdatedAt).ThenBy(album => album.AlbumName, StringComparer.OrdinalIgnoreCase))
        {
            Albums.Add(new ImmichAlbumOption(album.Id, album.AlbumName, album.AssetCount));
        }

        ImmichAlbumOption? currentSelection = null;
        if (!string.IsNullOrWhiteSpace(SelectedAlbum?.Id))
        {
            currentSelection = Albums.FirstOrDefault(album => album.Id == SelectedAlbum.Id);
        }

        if (currentSelection == null && !string.IsNullOrWhiteSpace(AlbumName))
        {
            currentSelection = Albums.FirstOrDefault(album => string.Equals(album.Name, AlbumName, StringComparison.OrdinalIgnoreCase));
        }

        SelectedAlbum = currentSelection;

        if (updateStatus)
        {
            StatusMessage = $"Loaded {Albums.Count} Immich album{(Albums.Count == 1 ? string.Empty : "s")}.";
        }
    }

    private void ApplyProfile(ImmichServerProfile profile)
    {
        ServerUrl = profile.ServerUrl;
        ServerVersion = profile.ServerVersion;
        ExternalDomain = profile.ExternalDomain;
        UserId = profile.UserId;
        UserName = profile.UserName;
        UserEmail = profile.UserEmail;
        BootstrapEmail = string.IsNullOrWhiteSpace(profile.UserEmail) ? BootstrapEmail : profile.UserEmail;
        PasswordLoginEnabled = profile.PasswordLoginEnabled;
        OAuthEnabled = profile.OAuthEnabled;
        SearchEnabled = profile.SearchEnabled;
        DuplicateDetectionEnabled = profile.DuplicateDetectionEnabled;
        SidecarSupported = profile.SidecarSupported;

        if (!string.IsNullOrWhiteSpace(profile.ApiKeyName))
        {
            ApiKeyName = profile.ApiKeyName;
        }

        UpdateConnectionState();
    }

    private void UpdateConnectionState()
    {
        IsConnected = !string.IsNullOrWhiteSpace(ApiKey);
        UpdateConnectionSummary();
        UpdateCapabilitiesSummary();
    }

    private void UpdateConnectionSummary()
    {
        if (!IsConnected)
        {
            ConnectionSummary = "No Immich API key configured yet.";
            return;
        }

        List<string> parts = new();

        if (!string.IsNullOrWhiteSpace(UserName))
        {
            parts.Add(UserName);
        }
        else if (!string.IsNullOrWhiteSpace(UserEmail))
        {
            parts.Add(UserEmail);
        }
        else
        {
            parts.Add("API key connected");
        }

        if (!string.IsNullOrWhiteSpace(UserEmail) && !parts.Any(part => string.Equals(part, UserEmail, StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add(UserEmail);
        }

        if (!string.IsNullOrWhiteSpace(ServerVersion))
        {
            parts.Add("Immich " + ServerVersion);
        }

        ConnectionSummary = string.Join(" | ", parts);
    }

    private void UpdateCapabilitiesSummary()
    {
        List<string> capabilities = new();

        if (DuplicateDetectionEnabled)
        {
            capabilities.Add("duplicate check");
        }

        if (SearchEnabled)
        {
            capabilities.Add("search");
        }

        if (SidecarSupported)
        {
            capabilities.Add("sidecars");
        }

        if (PasswordLoginEnabled)
        {
            capabilities.Add("password bootstrap");
        }

        if (OAuthEnabled)
        {
            capabilities.Add("oauth");
        }

        CapabilitiesSummary = capabilities.Count == 0
            ? "Server capabilities will appear after verification."
            : "Detected: " + string.Join(", ", capabilities) + ".";
    }

    private void LoadSecretsFromStore()
    {
        if (_secrets == null || string.IsNullOrWhiteSpace(_secretKey))
        {
            return;
        }

        ApiKey = _secrets.GetSecret("immich", _secretKey, "apiKey")
            ?? _secrets.GetSecret("immich", _secretKey, "apiToken")
            ?? ApiKey;

        SharePassword = _secrets.GetSecret("immich", _secretKey, "sharePassword") ?? SharePassword;
    }

    private void PersistSecrets()
    {
        if (_secrets == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            _secrets.DeleteSecret("immich", _secretKey, "apiKey");
            _secrets.DeleteSecret("immich", _secretKey, "apiToken");
        }
        else
        {
            _secrets.SetSecret("immich", _secretKey, "apiKey", ApiKey);
            _secrets.DeleteSecret("immich", _secretKey, "apiToken");
        }

        if (string.IsNullOrWhiteSpace(SharePassword))
        {
            _secrets.DeleteSecret("immich", _secretKey, "sharePassword");
        }
        else
        {
            _secrets.SetSecret("immich", _secretKey, "sharePassword", SharePassword);
        }
    }

    private void SyncSelectedAlbumIntoFields()
    {
        if (SelectedAlbum != null)
        {
            AlbumName = SelectedAlbum.Name;
        }
    }

    private void SetAlbumOptionsPlaceholder(string? albumId, string? albumName)
    {
        Albums.Clear();
        if (string.IsNullOrWhiteSpace(albumId) && string.IsNullOrWhiteSpace(albumName))
        {
            SelectedAlbum = null;
            AlbumName = string.Empty;
            return;
        }

        ImmichAlbumOption option = new(albumId ?? string.Empty, albumName ?? string.Empty, null);
        Albums.Add(option);
        // Only restore SelectedAlbum if it has a real ID; otherwise the placeholder
        // has no meaningful album identity and OnSelectedAlbumChanged would overwrite
        // AlbumName with just the name, losing the ID on the next save round-trip.
        if (!string.IsNullOrWhiteSpace(albumId))
        {
            SelectedAlbum = option;
        }
        else
        {
            SelectedAlbum = null;
            AlbumName = albumName ?? string.Empty;
        }
    }

    private bool TryNormalizeServerUrl(out string normalizedServerUrl, out string? error)
    {
        normalizedServerUrl = ImmichClient.NormalizeServerUrl(ServerUrl);
        error = null;

        if (string.IsNullOrWhiteSpace(normalizedServerUrl))
        {
            error = "Immich server URL is required.";
            return false;
        }

        if (!Uri.TryCreate(normalizedServerUrl, UriKind.Absolute, out Uri? uri) ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Immich server URL must be a valid http:// or https:// URL.";
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

public sealed class ImmichAlbumOption
{
    public ImmichAlbumOption(string id, string name, int? assetCount)
    {
        Id = id;
        Name = name;
        AssetCount = assetCount;
    }

    public string Id { get; }
    public string Name { get; }
    public int? AssetCount { get; }
    public string Summary => AssetCount.HasValue ? $"{Name} ({AssetCount.Value})" : Name;
}
