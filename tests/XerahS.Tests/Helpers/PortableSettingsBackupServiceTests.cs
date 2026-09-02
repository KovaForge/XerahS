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

using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using ShareX.AmazonS3.Plugin;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
[NonParallelizable]
public class PortableSettingsBackupServiceTests
{
    private string _testRoot = null!;
    private string _originalPersonalFolder = null!;

    [SetUp]
    public void SetUp()
    {
        _originalPersonalFolder = SettingsManager.PersonalFolder;
        _testRoot = Path.Combine(Path.GetTempPath(), "XerahS.Tests", "PortableSettings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        ProviderCatalog.Clear();
        ProviderCatalog.RegisterProvider(new AmazonS3Provider());
    }

    [TearDown]
    public void TearDown()
    {
        ProviderContextManager.ResetProviderContext();
        ProviderCatalog.Clear();
        SettingsManager.PersonalFolder = _originalPersonalFolder;
        InstanceManager.Instance.ReloadConfiguration();

        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Test]
    public void CreateAndRestore_RoundTripsSettingsDestinationAndPlaintextS3SecretsAcrossRoots()
    {
        const string accessKey = "AKIA_PORTABLE_TEST_123";
        const string secretAccessKey = "portable-secret-access-key-value";
        const string secretKey = "s3-portable-reference";
        string sourceRoot = Path.Combine(_testRoot, "source");
        string targetRoot = Path.Combine(_testRoot, "target");
        string archivePath = Path.Combine(_testRoot, "settings.xsbak");

        InitializeRoot(sourceRoot);
        SettingsManager.Settings.ShowTray = false;
        SettingsManager.Settings.CustomUploadersConfigPath = Path.Combine(sourceRoot, "external-uploaders");
        SettingsManager.Settings.CustomWorkflowsConfigPath = Path.Combine(sourceRoot, "external-workflows");
        Directory.CreateDirectory(SettingsManager.Settings.CustomUploadersConfigPath);
        Directory.CreateDirectory(SettingsManager.Settings.CustomWorkflowsConfigPath);

        var instance = new UploaderInstance
        {
            InstanceId = "portable-s3",
            ProviderId = "amazons3",
            Category = UploaderCategory.File,
            DisplayName = "Portable S3",
            IsAvailable = true,
            SettingsJson = $$"""
            {
              "AuthMode": 0,
              "SecretKey": "{{secretKey}}",
              "BucketName": "portable-bucket",
              "Region": "ap-southeast-2"
            }
            """
        };
        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, instance.InstanceId);

        ISecretStore sourceSecrets = ProviderContextManager.EnsureProviderContext().Secrets;
        sourceSecrets.SetSecret("amazons3", secretKey, "accessKeyId", accessKey);
        sourceSecrets.SetSecret("amazons3", secretKey, "secretAccessKey", secretAccessKey);
        File.WriteAllText(Path.Combine(SettingsManager.SettingsFolder, "ReClipConfig.json"), "{\"enabled\":true}");

        PortableSettingsBackupResult created = PortableSettingsBackupService.Create(archivePath);

        Assert.Multiple(() =>
        {
            Assert.That(created.SecretCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(File.Exists(archivePath), Is.True);
            Assert.That(ReadArchiveEntry(archivePath, "settings/secrets.json"), Does.Contain(accessKey));
            Assert.That(ReadArchiveEntry(archivePath, "settings/secrets.json"), Does.Contain(secretAccessKey));
            Assert.That(File.ReadAllText(SettingsManager.SecretsStoreFilePath), Does.Not.Contain(secretAccessKey));
        });

        InitializeRoot(targetRoot);
        SettingsManager.Settings.ShowTray = true;
        SettingsManager.SaveApplicationConfig();

        PortableSettingsRestoreResult restored = PortableSettingsBackupService.Restore(archivePath);
        ISecretStore targetSecrets = ProviderContextManager.EnsureProviderContext().Secrets;
        UploaderInstance? restoredInstance = InstanceManager.Instance.GetInstance(instance.InstanceId);

        Assert.Multiple(() =>
        {
            Assert.That(restored.SecretCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(SettingsManager.Settings.ShowTray, Is.False);
            Assert.That(SettingsManager.Settings.CustomUploadersConfigPath, Is.Empty);
            Assert.That(SettingsManager.Settings.CustomWorkflowsConfigPath, Is.Empty);
            Assert.That(restoredInstance, Is.Not.Null);
            Assert.That(restoredInstance!.DisplayName, Is.EqualTo("Portable S3"));
            Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.File)?.InstanceId, Is.EqualTo(instance.InstanceId));
            Assert.That(targetSecrets.GetSecret("amazons3", secretKey, "accessKeyId"), Is.EqualTo(accessKey));
            Assert.That(targetSecrets.GetSecret("amazons3", secretKey, "secretAccessKey"), Is.EqualTo(secretAccessKey));
            Assert.That(File.ReadAllText(SettingsManager.SecretsStoreFilePath), Does.Not.Contain(secretAccessKey));
            Assert.That(File.Exists(Path.Combine(SettingsManager.SettingsFolder, "ReClipConfig.json")), Is.True);
        });
    }

    [Test]
    public void Restore_WhenPayloadHashDoesNotMatch_DoesNotModifyCurrentSettings()
    {
        string sourceRoot = Path.Combine(_testRoot, "source-corrupt");
        string targetRoot = Path.Combine(_testRoot, "target-corrupt");
        string archivePath = Path.Combine(_testRoot, "corrupt.xsbak");

        InitializeRoot(sourceRoot);
        SettingsManager.Settings.ShowTray = false;
        PortableSettingsBackupService.Create(archivePath);

        using (ZipArchive zip = ZipFile.Open(archivePath, ZipArchiveMode.Update))
        {
            ZipArchiveEntry entry = zip.GetEntry("settings/application.json")!;
            entry.Delete();
            ZipArchiveEntry replacement = zip.CreateEntry("settings/application.json");
            using var writer = new StreamWriter(replacement.Open(), new UTF8Encoding(false));
            writer.Write("{\"ShowTray\":false,\"tampered\":true}");
        }

        InitializeRoot(targetRoot);
        SettingsManager.Settings.ShowTray = true;
        SettingsManager.SaveApplicationConfig();

        Assert.Throws<InvalidDataException>(() => PortableSettingsBackupService.Restore(archivePath));
        SettingsManager.LoadApplicationConfig(fallbackSupport: false);
        Assert.That(SettingsManager.Settings.ShowTray, Is.True);
    }

    private static void InitializeRoot(string personalFolder)
    {
        SettingsManager.PersonalFolder = personalFolder;
        InstanceManager.Instance.ReloadConfiguration();
        ProviderContextManager.ResetProviderContext();
        SettingsManager.LoadInitialSettings();
        InstanceManager.Instance.ImportConfigurationJson(JsonConvert.SerializeObject(new InstanceConfiguration()));
    }

    private static string ReadArchiveEntry(string archivePath, string entryName)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        ZipArchiveEntry entry = archive.GetEntry(entryName)!;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
