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
using Newtonsoft.Json;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Ftp.Plugin.ViewModels;

public partial class FtpConfigViewModel : ObservableObject, IUploaderConfigViewModel
{
    private FTPProtocol _previousProtocol = FTPProtocol.FTP;
    private FTPSEncryption _previousFtpsEncryptionMode = FTPSEncryption.Explicit;

    [ObservableProperty]
    private string _accountName = "FTP Account";

    [ObservableProperty]
    private FTPProtocol _protocol = FTPProtocol.FTP;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 21;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _subFolderPath = string.Empty;

    [ObservableProperty]
    private BrowserProtocol _browserProtocol = BrowserProtocol.http;

    [ObservableProperty]
    private string _httpHomePath = string.Empty;

    [ObservableProperty]
    private bool _httpHomePathAutoAddSubFolderPath = true;

    [ObservableProperty]
    private bool _httpHomePathNoExtension;

    [ObservableProperty]
    private FTPSEncryption _ftpsEncryptionMode = FTPSEncryption.Explicit;

    [ObservableProperty]
    private string _keypath = string.Empty;

    [ObservableProperty]
    private string _passphrase = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public bool ShowFtpsOptions => Protocol == FTPProtocol.FTPS;
    public bool ShowSftpKeyOptions => Protocol == FTPProtocol.SFTP;

    partial void OnProtocolChanged(FTPProtocol value)
    {
        if (Port == GetDefaultPort(_previousProtocol, _previousFtpsEncryptionMode))
        {
            Port = GetDefaultPort(value, FtpsEncryptionMode);
        }

        _previousProtocol = value;

        OnPropertyChanged(nameof(ShowFtpsOptions));
        OnPropertyChanged(nameof(ShowSftpKeyOptions));
    }

    partial void OnFtpsEncryptionModeChanged(FTPSEncryption value)
    {
        if (Protocol == FTPProtocol.FTPS && Port == GetDefaultPort(FTPProtocol.FTPS, _previousFtpsEncryptionMode))
        {
            Port = GetDefaultPort(FTPProtocol.FTPS, value);
        }

        _previousFtpsEncryptionMode = value;
    }

    public IReadOnlyList<FTPProtocol> ProtocolOptions { get; } = new[] { FTPProtocol.FTP, FTPProtocol.FTPS, FTPProtocol.SFTP };
    public IReadOnlyList<BrowserProtocol> BrowserProtocolOptions { get; } = new[] { BrowserProtocol.http, BrowserProtocol.https, BrowserProtocol.ftp, BrowserProtocol.ftps, BrowserProtocol.sftp };
    public IReadOnlyList<FTPSEncryption> FtpsEncryptionOptions { get; } = new[] { FTPSEncryption.Explicit, FTPSEncryption.Implicit };

    public void LoadFromJson(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<FtpConfigModel>(json);
            if (config != null)
            {
                AccountName = config.AccountName ?? "FTP Account";
                Protocol = config.Protocol;
                Host = config.Host ?? string.Empty;
                Port = config.Port;
                Username = config.Username ?? string.Empty;
                Password = config.Password ?? string.Empty;
                IsActive = config.IsActive;
                SubFolderPath = config.SubFolderPath ?? string.Empty;
                BrowserProtocol = config.BrowserProtocol;
                HttpHomePath = config.HttpHomePath ?? string.Empty;
                HttpHomePathAutoAddSubFolderPath = config.HttpHomePathAutoAddSubFolderPath;
                HttpHomePathNoExtension = config.HttpHomePathNoExtension;
                FtpsEncryptionMode = config.FTPSEncryption;
                Keypath = config.Keypath ?? string.Empty;
                Passphrase = config.Passphrase ?? string.Empty;
            }
        }
        catch
        {
            StatusMessage = "Failed to load configuration";
        }
    }

    public string ToJson()
    {
        var config = new FtpConfigModel
        {
            AccountName = AccountName ?? "FTP Account",
            Protocol = Protocol,
            Host = Host ?? string.Empty,
            Port = Port,
            Username = Username ?? string.Empty,
            Password = Password ?? string.Empty,
            IsActive = IsActive,
            SubFolderPath = SubFolderPath ?? string.Empty,
            BrowserProtocol = BrowserProtocol,
            HttpHomePath = HttpHomePath ?? string.Empty,
            HttpHomePathAutoAddSubFolderPath = HttpHomePathAutoAddSubFolderPath,
            HttpHomePathNoExtension = HttpHomePathNoExtension,
            FTPSEncryption = FtpsEncryptionMode,
            Keypath = Keypath ?? string.Empty,
            Passphrase = Passphrase ?? string.Empty
        };
        return JsonConvert.SerializeObject(config, Formatting.Indented);
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            StatusMessage = "Host is required.";
            return false;
        }

        if (Port is <= 0 or > 65535)
        {
            StatusMessage = "Port must be between 1 and 65535.";
            return false;
        }

        if (Protocol == FTPProtocol.SFTP && string.IsNullOrWhiteSpace(Keypath) && string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "SFTP requires either a key file path or password.";
            return false;
        }

        StatusMessage = null;
        return true;
    }

    private static int GetDefaultPort(FTPProtocol protocol, FTPSEncryption ftpsEncryptionMode)
    {
        return protocol switch
        {
            FTPProtocol.SFTP => 22,
            FTPProtocol.FTPS when ftpsEncryptionMode == FTPSEncryption.Implicit => 990,
            _ => 21
        };
    }
}
