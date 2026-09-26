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

using System.Net;
using System.Net.Security;
using FluentFTP;
using Renci.SshNet;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.FileUploaders;

namespace ShareX.Ftp.Plugin;

/// <summary>Creates FTP/FTPS and SFTP clients for an account; shared by uploads and the Media Browser.</summary>
internal static class FtpClientFactory
{
    public static FtpClient CreateFtp(FTPAccount account)
    {
        var client = new FtpClient
        {
            Host = NormalizeHost(account.Host),
            Port = account.Port,
            Credentials = new NetworkCredential(account.Username ?? "", account.Password ?? "")
        };

        client.Config.DataConnectionType = account.IsActive ? FtpDataConnectionType.AutoActive : FtpDataConnectionType.AutoPassive;

        if (account.Protocol == FTPProtocol.FTPS)
        {
            client.Config.EncryptionMode = account.FTPSEncryption == FTPSEncryption.Implicit ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
            client.Config.DataConnectionEncryption = true;
            client.ValidateCertificate += (_, e) =>
            {
                if (e.PolicyErrors != SslPolicyErrors.None)
                    e.Accept = true;
            };
        }

        return client;
    }

    /// <summary>Key file first (falling back to the password), else password. Null with an error when neither works.</summary>
    public static SftpClient? CreateSftp(FTPAccount account, out string? error)
    {
        error = null;
        string keyPath = account.Keypath?.Trim() ?? string.Empty;
        bool hasPassword = !string.IsNullOrWhiteSpace(account.Password);

        if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
        {
            try
            {
                PrivateKeyFile keyFile = string.IsNullOrEmpty(account.Passphrase)
                    ? new PrivateKeyFile(keyPath)
                    : new PrivateKeyFile(keyPath, account.Passphrase);
                return new SftpClient(NormalizeHost(account.Host), account.Port, account.Username ?? "", keyFile);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);

                if (hasPassword)
                {
                    return CreatePasswordSftp(account);
                }

                error = "SFTP key file could not be loaded: " + keyPath;
                return null;
            }
        }

        if (hasPassword)
            return CreatePasswordSftp(account);

        error = !string.IsNullOrWhiteSpace(keyPath)
            ? "SFTP key file not found: " + keyPath
            : "SFTP requires either a key file or password.";
        return null;
    }

    private static SftpClient CreatePasswordSftp(FTPAccount account) =>
        new(NormalizeHost(account.Host), account.Port, account.Username ?? "", account.Password);

    private static string NormalizeHost(string? host) => host?.Trim() ?? string.Empty;
}
