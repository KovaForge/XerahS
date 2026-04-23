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
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShareX.Imgur.Plugin.ViewModels;

/// <summary>
/// ViewModel for Imgur configuration
/// </summary>
public partial class ImgurConfigViewModel : ObservableObject, IUploaderConfigViewModel, IProviderContextAware
{
    private const string LegacyPlaceholderClientId = "30d41ft9z9r8jtt";

    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private int _accountTypeIndex = 0;

    [ObservableProperty]
    private string _albumId = string.Empty;

    [ObservableProperty]
    private int _thumbnailTypeIndex = 4; // Large thumbnail default

    [ObservableProperty]
    private bool _useDirectLink = true;

    [ObservableProperty]
    private bool _useGifv = true;

    [ObservableProperty]
    private bool _uploadToSelectedAlbum = false;

    [ObservableProperty]
    private ObservableCollection<ImgurAlbumData> _albums = new();

    [ObservableProperty]
    private ImgurAlbumData? _selectedAlbum;

    [ObservableProperty]
    private string? _albumStatusMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _authCallbackUrl = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    private ImgurUploader? _uploader;
    private ImgurConfigModel _config = new();
    private string _secretKey = Guid.NewGuid().ToString("N");
    private ISecretStore? _secrets;

    public ImgurConfigViewModel()
    {
        _uploader = null;
    }

    [RelayCommand]
    private void OpenLoginUrl()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.Equals(ClientId, LegacyPlaceholderClientId, StringComparison.Ordinal))
        {
            StatusMessage = "Enter your own Imgur Client ID from https://api.imgur.com/oauth2/addclient before logging in.";
            return;
        }

        EnsureUploader(rebuild: true);
        if (_uploader == null) return;
        string url = _uploader.GetAuthorizationURL();

        if (TryOpenUrl(url))
        {
            StatusMessage = "Opened Imgur login in browser. If no tab appeared, copy and open this URL manually: " + url;
        }
        else
        {
            StatusMessage = "Could not open browser automatically. Open this URL manually: " + url;
        }
    }

    [RelayCommand]
    private void CompleteLogin()
    {
        EnsureUploader(rebuild: true);
        if (_uploader == null || string.IsNullOrWhiteSpace(AuthCallbackUrl))
        {
            StatusMessage = "Please paste the full callback URL from Imgur (including the #access_token fragment).";
            return;
        }

        if (_uploader.GetAccessToken(AuthCallbackUrl))
        {
            IsLoggedIn = true;
            StatusMessage = "Logged in successfully!";
            AuthCallbackUrl = string.Empty;
            PersistToken();
        }
        else
        {
            StatusMessage = "Login failed. Verify your Client ID and paste the full callback URL returned by Imgur.";
        }
    }

    [RelayCommand]
    private void FetchAlbums()
    {
        if (_uploader == null || !IsLoggedIn)
        {
            AlbumStatusMessage = "You must be logged in to fetch albums";
            return;
        }

        try
        {
            var albumList = _uploader.GetAlbums();
            if (albumList != null && albumList.Count > 0)
            {
                Albums.Clear();
                foreach (var album in albumList)
                {
                    Albums.Add(album);
                }
                AlbumStatusMessage = $"Loaded {albumList.Count} albums";
            }
            else
            {
                Albums.Clear();
                AlbumStatusMessage = "No albums found or failed to fetch";
            }
        }
        catch (Exception ex)
        {
            AlbumStatusMessage = $"Error fetching albums: {ex.Message}";
        }
    }

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<ImgurConfigModel>(json);
            if (config != null)
            {
                _config = config;
                _secretKey = string.IsNullOrWhiteSpace(_config.SecretKey) ? Guid.NewGuid().ToString("N") : _config.SecretKey;

                ClientId = _config.ClientId ?? string.Empty;
                AccountTypeIndex = NormalizeAccountTypeIndex(_config.AccountType);
                ThumbnailTypeIndex = NormalizeThumbnailTypeIndex(_config.ThumbnailType);
                UseDirectLink = _config.DirectLink;
                UseGifv = _config.UseGIFV;
                UploadToSelectedAlbum = _config.UploadToSelectedAlbum;
                IsLoggedIn = HasToken();
                _uploader = BuildUploader();

                // Always refresh selected album state so reused view-model instances do not keep a stale album.
                SelectedAlbum = _config.SelectedAlbum;
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        AccountTypeIndex = NormalizeAccountTypeIndex((AccountType)AccountTypeIndex);
        ThumbnailTypeIndex = NormalizeThumbnailTypeIndex((ImgurThumbnailType)ThumbnailTypeIndex);

        _config.ClientId = ClientId;
        _config.AccountType = (AccountType)AccountTypeIndex;
        _config.ThumbnailType = (ImgurThumbnailType)ThumbnailTypeIndex;
        _config.DirectLink = UseDirectLink;
        _config.UseGIFV = UseGifv;
        _config.UploadToSelectedAlbum = UploadToSelectedAlbum;
        _config.SecretKey = _secretKey;

        // Save selected album
        if (UploadToSelectedAlbum && SelectedAlbum != null)
        {
            _config.SelectedAlbum = SelectedAlbum;
        }
        else
        {
            _config.SelectedAlbum = null;
        }

        PersistCredentials();
        return JsonConvert.SerializeObject(_config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            StatusMessage = "Client ID is required";
            return false;
        }

        if (AccountTypeIndex == (int)AccountType.User && !IsLoggedIn)
        {
            StatusMessage = "Login is required for User Account type";
            return false;
        }

        PersistCredentials();
        StatusMessage = null;
        return true;
    }

    public void SetContext(IProviderContext context)
    {
        _secrets = context.Secrets;
    }

    private ImgurUploader BuildUploader()
    {
        var clientSecret = _secrets?.GetSecret("imgur", _secretKey, "clientSecret")
            ?? "98871f37e179e496a0149e9c8558487779d424ft";
        var authInfo = new OAuth2Info(ClientId, clientSecret);
        var tokenJson = _secrets?.GetSecret("imgur", _secretKey, "oauthToken");
        var token = TryDeserializeToken(tokenJson);
        if (token != null)
        {
            authInfo.Token = token;
        }

        return new ImgurUploader(_config, authInfo);
    }

    private void EnsureUploader(bool rebuild = false)
    {
        if (rebuild || _uploader == null)
        {
            AccountTypeIndex = NormalizeAccountTypeIndex((AccountType)AccountTypeIndex);
            ThumbnailTypeIndex = NormalizeThumbnailTypeIndex((ImgurThumbnailType)ThumbnailTypeIndex);

            _config.ClientId = ClientId;
            _config.AccountType = (AccountType)AccountTypeIndex;
            _config.ThumbnailType = (ImgurThumbnailType)ThumbnailTypeIndex;
            _config.DirectLink = UseDirectLink;
            _config.UseGIFV = UseGifv;
            _config.UploadToSelectedAlbum = UploadToSelectedAlbum;
            _config.SelectedAlbum = UploadToSelectedAlbum ? SelectedAlbum : null;
            _config.SecretKey = _secretKey;
            _uploader = BuildUploader();
        }
    }

    private static int NormalizeAccountTypeIndex(AccountType accountType)
    {
        return Enum.IsDefined(accountType) ? (int)accountType : (int)AccountType.Anonymous;
    }

    private static int NormalizeThumbnailTypeIndex(ImgurThumbnailType thumbnailType)
    {
        return Enum.IsDefined(thumbnailType) ? (int)thumbnailType : (int)ImgurThumbnailType.Medium_Thumbnail;
    }

    private static bool TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}")
                    {
                        CreateNoWindow = true
                    });
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                    return true;
                }
            }
            catch
            {
                // Fall through and report failure to caller.
            }

            return false;
        }
    }

    private void PersistCredentials()
    {
        if (_secrets == null)
        {
            return;
        }

        _secrets.SetSecret("imgur", _secretKey, "clientSecret", "98871f37e179e496a0149e9c8558487779d424ft");
    }

    private void PersistToken()
    {
        if (_secrets == null || _uploader == null)
        {
            return;
        }

        if (_uploader.AuthInfo.Token != null && !string.IsNullOrEmpty(_uploader.AuthInfo.Token.access_token))
        {
            var json = JsonConvert.SerializeObject(_uploader.AuthInfo.Token, Formatting.None);
            _secrets.SetSecret("imgur", _secretKey, "oauthToken", json);
        }
    }

    private bool HasToken()
    {
        if (_secrets == null)
        {
            return false;
        }

        var tokenJson = _secrets.GetSecret("imgur", _secretKey, "oauthToken");
        var token = TryDeserializeToken(tokenJson);
        return token != null && !string.IsNullOrEmpty(token.access_token);
    }

    private static OAuth2Token? TryDeserializeToken(string? tokenJson)
    {
        if (string.IsNullOrWhiteSpace(tokenJson))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<OAuth2Token>(tokenJson);
        }
        catch
        {
            return null;
        }
    }
}
