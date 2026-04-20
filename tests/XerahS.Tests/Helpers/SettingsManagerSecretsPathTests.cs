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

using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;

namespace XerahS.Tests.Helpers;

[TestFixture]
[NonParallelizable]
public class SettingsManagerSecretsPathTests
{
    private string? _originalPersonalFolder;

    [SetUp]
    public void SetUp()
    {
        _originalPersonalFolder = SettingsManager.PersonalFolder;
        SettingsManager.PersonalFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "settings-manager-tests", Guid.NewGuid().ToString("N"));
        SettingsManager.LoadAllSettings();
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_originalPersonalFolder))
        {
            SettingsManager.PersonalFolder = _originalPersonalFolder;
            SettingsManager.LoadAllSettings();
        }
    }

    [Test]
    public void SecretsStoreFilePath_UsesMachineSpecificFileName_WhenEnabled()
    {
        bool original = SettingsManager.Settings.UseMachineSpecificSecretsStore;
        try
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = true;

            string expectedMachineName = FileHelpers.SanitizeFileName(Environment.MachineName);
            string fileName = Path.GetFileName(SettingsManager.SecretsStoreFilePath);

            if (string.IsNullOrEmpty(expectedMachineName))
            {
                Assert.That(fileName, Is.EqualTo(SettingsManager.SecretsStoreFileName));
            }
            else
            {
                Assert.That(fileName, Is.EqualTo($"{SettingsManager.SecretsStoreFileNamePrefix}-{expectedMachineName}.{SettingsManager.SecretsStoreFileNameExtension}"));
            }
        }
        finally
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = original;
        }
    }

    [Test]
    public void SecretsStoreFilePath_UsesSharedFileName_WhenDisabled()
    {
        bool original = SettingsManager.Settings.UseMachineSpecificSecretsStore;
        try
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = false;

            string fileName = Path.GetFileName(SettingsManager.SecretsStoreFilePath);
            Assert.That(fileName, Is.EqualTo(SettingsManager.SecretsStoreFileName));
        }
        finally
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = original;
        }
    }

    [Test]
    public void ResetSettings_DeletesAndBacksUpSecretsStoreArtifacts()
    {
        SettingsManager.Settings.UseMachineSpecificSecretsStore = true;
        SettingsManager.EnsureDirectoriesExist();

        string secretsPath = SettingsManager.SecretsStoreFilePath;
        string keyPath = Path.Combine(Path.GetDirectoryName(secretsPath) ?? SettingsManager.SettingsFolder, "SecretsStore.key");

        File.WriteAllText(SettingsManager.ApplicationConfigFilePath, "{}");
        File.WriteAllText(SettingsManager.UploadersConfigFilePath, "{}");
        File.WriteAllText(SettingsManager.WorkflowsConfigFilePath, "{}");
        File.WriteAllText(secretsPath, "{\"provider\":\"token\"}");
        File.WriteAllText(keyPath, "secret-key");

        bool reset = SettingsManager.ResetSettings();

        string latestResetBackup = Directory
            .GetDirectories(SettingsManager.BackupFolder, "Reset_*")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .First();

        Assert.Multiple(() =>
        {
            Assert.That(reset, Is.True);
            Assert.That(File.Exists(secretsPath), Is.False, "SecretsStore.json should be removed during reset.");
            Assert.That(File.Exists(keyPath), Is.False, "SecretsStore.key should be removed during reset.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(secretsPath))), Is.True,
                "SecretsStore.json should be backed up before deletion.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(keyPath))), Is.True,
                "SecretsStore.key should be backed up before deletion.");
        });
    }

    [Test]
    public void ResetSettings_DeletesAndBacksUpResolvedMachineSpecificAndCustomConfigFiles()
    {
        SettingsManager.Settings.UseMachineSpecificUploadersConfig = true;
        SettingsManager.Settings.UseMachineSpecificWorkflowsConfig = true;
        SettingsManager.Settings.UseMachineSpecificSecretsStore = true;

        string customUploadersFolder = Path.Combine(SettingsManager.PersonalFolder, "custom-uploaders");
        string customWorkflowsFolder = Path.Combine(SettingsManager.PersonalFolder, "custom-workflows");
        Directory.CreateDirectory(customUploadersFolder);
        Directory.CreateDirectory(customWorkflowsFolder);

        SettingsManager.Settings.CustomUploadersConfigPath = customUploadersFolder;
        SettingsManager.Settings.CustomWorkflowsConfigPath = customWorkflowsFolder;
        SettingsManager.EnsureDirectoriesExist();

        string applicationConfigPath = SettingsManager.ApplicationConfigFilePath;
        string uploadersConfigPath = SettingsManager.UploadersConfigFilePath;
        string workflowsConfigPath = SettingsManager.WorkflowsConfigFilePath;
        string secretsPath = SettingsManager.SecretsStoreFilePath;
        string keyPath = Path.Combine(Path.GetDirectoryName(secretsPath) ?? SettingsManager.SettingsFolder, "SecretsStore.key");

        File.WriteAllText(applicationConfigPath, "{}");
        File.WriteAllText(uploadersConfigPath, "{}");
        File.WriteAllText(workflowsConfigPath, "{}");
        File.WriteAllText(secretsPath, "{\"provider\":\"token\"}");
        File.WriteAllText(keyPath, "secret-key");

        bool reset = SettingsManager.ResetSettings();

        string latestResetBackup = Directory
            .GetDirectories(SettingsManager.BackupFolder, "Reset_*")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .First();

        Assert.Multiple(() =>
        {
            Assert.That(reset, Is.True);
            Assert.That(File.Exists(applicationConfigPath), Is.False, "ApplicationConfig.json should be removed during reset.");
            Assert.That(File.Exists(uploadersConfigPath), Is.False, "Resolved uploaders config should be removed during reset.");
            Assert.That(File.Exists(workflowsConfigPath), Is.False, "Resolved workflows config should be removed during reset.");
            Assert.That(File.Exists(secretsPath), Is.False, "Resolved secrets store should be removed during reset.");
            Assert.That(File.Exists(keyPath), Is.False, "SecretsStore.key should be removed during reset.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(applicationConfigPath))), Is.True,
                "ApplicationConfig.json should be backed up before deletion.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(uploadersConfigPath))), Is.True,
                "Resolved uploaders config should be backed up before deletion.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(workflowsConfigPath))), Is.True,
                "Resolved workflows config should be backed up before deletion.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(secretsPath))), Is.True,
                "Resolved secrets store should be backed up before deletion.");
            Assert.That(File.Exists(Path.Combine(latestResetBackup, Path.GetFileName(keyPath))), Is.True,
                "SecretsStore.key should be backed up before deletion.");
        });
    }
}
