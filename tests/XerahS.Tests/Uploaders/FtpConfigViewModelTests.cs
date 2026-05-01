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

using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using Renci.SshNet;
using ShareX.Ftp.Plugin;
using ShareX.Ftp.Plugin.ViewModels;
using XerahS.Uploaders;
using XerahS.Uploaders.FileUploaders;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class FtpConfigViewModelTests
{
    [Test]
    public void ProtocolChange_UpdatesDefaultPortWhenUserHasNotCustomizedIt()
    {
        var viewModel = new FtpConfigViewModel();

        viewModel.Protocol = FTPProtocol.SFTP;

        Assert.That(viewModel.Port, Is.EqualTo(22));
    }

    [Test]
    public void ProtocolChange_PreservesCustomPort()
    {
        var viewModel = new FtpConfigViewModel
        {
            Port = 2021
        };

        viewModel.Protocol = FTPProtocol.SFTP;

        Assert.That(viewModel.Port, Is.EqualTo(2021));
    }

    [Test]
    public void FtpsEncryptionChange_UpdatesDefaultPortWhenUserHasNotCustomizedIt()
    {
        var viewModel = new FtpConfigViewModel
        {
            Protocol = FTPProtocol.FTPS
        };

        viewModel.FtpsEncryptionMode = FTPSEncryption.Implicit;

        Assert.That(viewModel.Port, Is.EqualTo(990));
    }

    [Test]
    public void FtpsEncryptionChange_PreservesCustomPort()
    {
        var viewModel = new FtpConfigViewModel
        {
            Protocol = FTPProtocol.FTPS,
            Port = 2121
        };

        viewModel.FtpsEncryptionMode = FTPSEncryption.Implicit;

        Assert.That(viewModel.Port, Is.EqualTo(2121));
    }

    [Test]
    public void LoadFromJson_ReplacesMissingSftpPortWithProtocolDefault()
    {
        var viewModel = new FtpConfigViewModel();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Protocol = FTPProtocol.SFTP,
            Host = "example.com",
            Port = 0
        });

        viewModel.LoadFromJson(json);

        Assert.That(viewModel.Port, Is.EqualTo(22));
    }

    [Test]
    public void LoadFromJson_TrimsHost()
    {
        var viewModel = new FtpConfigViewModel();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Host = "  example.com  "
        });

        viewModel.LoadFromJson(json);

        Assert.That(viewModel.Host, Is.EqualTo("example.com"));
    }

    [Test]
    public void ToJson_TrimsHost()
    {
        var viewModel = new FtpConfigViewModel
        {
            Host = "  example.com  "
        };

        var config = JsonConvert.DeserializeObject<FtpConfigModel>(viewModel.ToJson());

        Assert.That(config?.Host, Is.EqualTo("example.com"));
    }

    [Test]
    public void LoadFromJson_ClearsPreviousErrorStatusAfterSuccessfulLoad()
    {
        var viewModel = new FtpConfigViewModel();
        viewModel.LoadFromJson("{");
        Assert.That(viewModel.StatusMessage, Is.EqualTo("Failed to load configuration"));

        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Host = "example.com"
        });

        viewModel.LoadFromJson(json);

        Assert.That(viewModel.StatusMessage, Is.Null);
    }

    [Test]
    public void LoadFromJson_ReplacesMissingImplicitFtpsPortWithProtocolDefault()
    {
        var viewModel = new FtpConfigViewModel();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Protocol = FTPProtocol.FTPS,
            FTPSEncryption = FTPSEncryption.Implicit,
            Host = "example.com",
            Port = 0
        });

        viewModel.LoadFromJson(json);

        Assert.That(viewModel.Port, Is.EqualTo(990));
    }

    [Test]
    public void LoadFromJson_ReplacesOutOfRangePortWithProtocolDefault()
    {
        var viewModel = new FtpConfigViewModel();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Protocol = FTPProtocol.SFTP,
            Host = "example.com",
            Port = 70000
        });

        viewModel.LoadFromJson(json);

        Assert.That(viewModel.Port, Is.EqualTo(22));
    }

    [Test]
    public void LoadFromJson_NormalizesInvalidEnumValuesToSafeDefaults()
    {
        var viewModel = new FtpConfigViewModel();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Protocol = (FTPProtocol)999,
            BrowserProtocol = (BrowserProtocol)999,
            FTPSEncryption = (FTPSEncryption)999,
            Host = "example.com",
            Port = 0
        });

        viewModel.LoadFromJson(json);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Protocol, Is.EqualTo(FTPProtocol.FTP));
            Assert.That(viewModel.BrowserProtocol, Is.EqualTo(BrowserProtocol.http));
            Assert.That(viewModel.FtpsEncryptionMode, Is.EqualTo(FTPSEncryption.Explicit));
            Assert.That(viewModel.Port, Is.EqualTo(21));
        });
    }

    [Test]
    public void Validate_RejectsOutOfRangePort()
    {
        var viewModel = new FtpConfigViewModel
        {
            Host = "example.com",
            Port = 0
        };

        bool isValid = viewModel.Validate();

        Assert.That(isValid, Is.False);
        Assert.That(viewModel.StatusMessage, Is.EqualTo("Port must be between 1 and 65535."));
    }

    [Test]
    public void Validate_RejectsMissingSftpKeyFileWhenNoPasswordFallbackExists()
    {
        var viewModel = new FtpConfigViewModel
        {
            Protocol = FTPProtocol.SFTP,
            Host = "example.com",
            Port = 22,
            Keypath = " /definitely/missing/key "
        };

        bool isValid = viewModel.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo("SFTP key file does not exist."));
        });
    }

    [Test]
    public void Validate_AllowsMissingSftpKeyFileWhenPasswordFallbackExists()
    {
        var viewModel = new FtpConfigViewModel
        {
            Protocol = FTPProtocol.SFTP,
            Host = "example.com",
            Port = 22,
            Password = "secret",
            Keypath = " /definitely/missing/key "
        };

        bool isValid = viewModel.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(viewModel.StatusMessage, Is.Null);
        });
    }

    [Test]
    public void ProviderCreateInstance_NormalizesInvalidEnumsAndMissingPort()
    {
        var provider = new FtpProvider();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Protocol = (FTPProtocol)999,
            BrowserProtocol = (BrowserProtocol)999,
            FTPSEncryption = (FTPSEncryption)999,
            Host = "example.com",
            Port = 0
        });

        var uploader = provider.CreateInstance(json);
        FTPAccount account = GetAccount((FtpUploader)uploader);

        Assert.Multiple(() =>
        {
            Assert.That(account.Protocol, Is.EqualTo(FTPProtocol.FTP));
            Assert.That(account.BrowserProtocol, Is.EqualTo(BrowserProtocol.http));
            Assert.That(account.FTPSEncryption, Is.EqualTo(FTPSEncryption.Explicit));
            Assert.That(account.Port, Is.EqualTo(21));
        });
    }

    [Test]
    public void ProviderCreateInstance_ReplacesOutOfRangePortWithProtocolDefault()
    {
        var provider = new FtpProvider();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Protocol = FTPProtocol.FTPS,
            FTPSEncryption = FTPSEncryption.Implicit,
            Host = "example.com",
            Port = 70000
        });

        var uploader = provider.CreateInstance(json);
        FTPAccount account = GetAccount((FtpUploader)uploader);

        Assert.That(account.Port, Is.EqualTo(990));
    }

    [Test]
    public void ProviderCreateInstance_TrimsHostBeforeCreatingUploader()
    {
        var provider = new FtpProvider();
        string json = JsonConvert.SerializeObject(new FtpConfigModel
        {
            Host = "  example.com  "
        });

        var uploader = provider.CreateInstance(json);
        FTPAccount account = GetAccount((FtpUploader)uploader);

        Assert.That(account.Host, Is.EqualTo("example.com"));
    }

    [Test]
    public void Upload_RejectsWhitespaceOnlyHostWithoutConnecting()
    {
        using var uploader = new FtpUploader(new FTPAccount
        {
            Host = "   ",
            Port = 21,
            Protocol = FTPProtocol.FTP
        });
        using var stream = new MemoryStream([1, 2, 3]);

        uploader.Upload(stream, "capture.png");

        Assert.That(uploader.Errors.Errors.Select(error => error.Text), Contains.Item("FTP host is required."));
    }

    [Test]
    public void GetSafeRemoteFileName_RemovesDirectorySegmentsFromUploadName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InvokeGetSafeRemoteFileName(@"C:\\Users\\alice\\Pictures\\capture.png"), Is.EqualTo("capture.png"));
            Assert.That(InvokeGetSafeRemoteFileName("../../nested/report.txt"), Is.EqualTo("report.txt"));
            Assert.That(InvokeGetSafeRemoteFileName("../"), Is.EqualTo("upload"));
        });
    }

    [Test]
    public void GetRemoteDirectoryPath_ReturnsOnlyParentDirectory()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InvokeGetRemoteDirectoryPath("capture.png"), Is.EqualTo(string.Empty));
            Assert.That(InvokeGetRemoteDirectoryPath("screenshots/capture.png"), Is.EqualTo("screenshots"));
            Assert.That(InvokeGetRemoteDirectoryPath("/var/www/capture.png"), Is.EqualTo("/var/www"));
            Assert.That(InvokeGetRemoteDirectoryPath("/capture.png"), Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void GetUriPath_RemovesProtocolPrefixFromHttpHomePath()
    {
        var account = new FTPAccount
        {
            Host = "ftp.example.com",
            BrowserProtocol = BrowserProtocol.https,
            HttpHomePath = "https://cdn.example.com/base",
            SubFolderPath = "shots"
        };

        string url = account.GetUriPath("capture 1.png");

        Assert.That(url, Is.EqualTo("https://cdn.example.com/base/shots/capture%201.png"));
    }

    [Test]
    public void CreateSftpClient_FallsBackToPassword_WhenConfiguredKeyPathIsMissing()
    {
        var uploader = new FtpUploader(new FTPAccount
        {
            Host = "example.com",
            Port = 22,
            Protocol = FTPProtocol.SFTP,
            Username = "alice",
            Password = "secret",
            Keypath = " /definitely/missing/key "
        });

        SftpClient? client = InvokeCreateSftpClient(uploader);

        Assert.Multiple(() =>
        {
            Assert.That(client, Is.Not.Null);
            Assert.That(uploader.Errors.Errors, Is.Empty);
        });

        client?.Dispose();
    }

    [Test]
    public void CreateSftpClient_FallsBackToPassword_WhenConfiguredKeyFileCannotBeLoaded()
    {
        string keyPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"invalid-sftp-key-{Guid.NewGuid():N}.key");
        File.WriteAllText(keyPath, "not a private key");

        try
        {
            var uploader = new FtpUploader(new FTPAccount
            {
                Host = "example.com",
                Port = 22,
                Protocol = FTPProtocol.SFTP,
                Username = "alice",
                Password = "secret",
                Keypath = keyPath
            });

            SftpClient? client = InvokeCreateSftpClient(uploader);

            Assert.Multiple(() =>
            {
                Assert.That(client, Is.Not.Null);
                Assert.That(uploader.Errors.Errors, Is.Empty);
            });

            client?.Dispose();
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    [Test]
    public void CreateSftpClient_ReportsMissingKeyFile_WhenNoPasswordFallbackExists()
    {
        var uploader = new FtpUploader(new FTPAccount
        {
            Host = "example.com",
            Port = 22,
            Protocol = FTPProtocol.SFTP,
            Username = "alice",
            Keypath = " /definitely/missing/key "
        });

        SftpClient? client = InvokeCreateSftpClient(uploader);

        Assert.Multiple(() =>
        {
            Assert.That(client, Is.Null);
            Assert.That(uploader.Errors.Errors.Select(error => error.Text), Contains.Item("SFTP key file not found: /definitely/missing/key"));
        });
    }

    private static FTPAccount GetAccount(FtpUploader uploader)
    {
        FieldInfo? field = typeof(FtpUploader).GetField("_account", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (FTPAccount)field!.GetValue(uploader)!;
    }

    private static SftpClient? InvokeCreateSftpClient(FtpUploader uploader)
    {
        MethodInfo? method = typeof(FtpUploader).GetMethod("CreateSftpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (SftpClient?)method!.Invoke(uploader, null);
    }

    private static string InvokeGetRemoteDirectoryPath(string remotePath)
    {
        MethodInfo? method = typeof(FtpUploader).GetMethod("GetRemoteDirectoryPath", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (string)method!.Invoke(null, [remotePath])!;
    }

    private static string InvokeGetSafeRemoteFileName(string? fileName)
    {
        MethodInfo? method = typeof(FtpUploader).GetMethod("GetSafeRemoteFileName", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (string)method!.Invoke(null, new object?[] { fileName })!;
    }
}
