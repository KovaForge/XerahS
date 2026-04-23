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
using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Uploaders;

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
        ResetProviderContext();
        SettingsManager.LoadAllSettings();
    }

    [TearDown]
    public void TearDown()
    {
        ResetProviderContext();

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

    [Test]
    public void ResetSettings_ClearsCachedProviderContext()
    {
        SettingsManager.Settings.UseMachineSpecificSecretsStore = true;
        var machineSpecificContext = ProviderContextManager.EnsureProviderContext();

        bool reset = SettingsManager.ResetSettings();
        var sharedContext = ProviderContextManager.EnsureProviderContext();

        Assert.Multiple(() =>
        {
            Assert.That(reset, Is.True);
            Assert.That(ProviderContextManager.Current, Is.SameAs(sharedContext));
            Assert.That(sharedContext, Is.Not.SameAs(machineSpecificContext));
            Assert.That(GetProviderContextSecretsPath(), Is.EqualTo(SettingsManager.SecretsStoreFilePath));
        });
    }

    [Test]
    public void ResetSettings_ClearsRecentTaskManagerState()
    {
        SettingsManager.Settings.RecentTasksSave = true;
        SettingsManager.RecentTaskManager.Add(new RecentTask
        {
            FilePath = "capture.png",
            URL = "https://example.test/capture.png"
        });

        bool reset = SettingsManager.ResetSettings();
        SettingsManager.SaveApplicationConfig();

        Assert.Multiple(() =>
        {
            Assert.That(reset, Is.True);
            Assert.That(SettingsManager.RecentTaskManager.ToArray(), Is.Empty,
                "Reset should clear the in-memory recent task queue.");
            Assert.That(SettingsManager.Settings.RecentTasks, Is.Null,
                "Saving immediately after reset should not repopulate ApplicationConfig with stale recent tasks.");
        });
    }

    [Test]
    public void UploadersAndWorkflowsConfigPaths_ResolveRelativeCustomFoldersAgainstAppBaseDirectory()
    {
        SettingsManager.Settings.CustomUploadersConfigPath = Path.Combine("relative-config", "uploaders");
        SettingsManager.Settings.CustomWorkflowsConfigPath = Path.Combine("relative-config", "workflows");
        SettingsManager.Settings.UseMachineSpecificUploadersConfig = false;
        SettingsManager.Settings.UseMachineSpecificWorkflowsConfig = false;

        string expectedUploadersFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relative-config", "uploaders"));
        string expectedWorkflowsFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relative-config", "workflows"));

        Assert.Multiple(() =>
        {
            Assert.That(Path.GetDirectoryName(SettingsManager.UploadersConfigFilePath), Is.EqualTo(expectedUploadersFolder));
            Assert.That(Path.GetDirectoryName(SettingsManager.WorkflowsConfigFilePath), Is.EqualTo(expectedWorkflowsFolder));
        });
    }

    [Test]
    public void UploadersAndWorkflowsConfigPaths_IgnoreWhitespaceOnlyCustomFolders()
    {
        SettingsManager.Settings.CustomUploadersConfigPath = "   ";
        SettingsManager.Settings.CustomWorkflowsConfigPath = "\t";
        SettingsManager.Settings.UseMachineSpecificUploadersConfig = false;
        SettingsManager.Settings.UseMachineSpecificWorkflowsConfig = false;

        Assert.Multiple(() =>
        {
            Assert.That(Path.GetDirectoryName(SettingsManager.UploadersConfigFilePath), Is.EqualTo(SettingsManager.SettingsFolder));
            Assert.That(Path.GetDirectoryName(SettingsManager.WorkflowsConfigFilePath), Is.EqualTo(SettingsManager.SettingsFolder));
        });
    }

    [Test]
    public void EnsureProviderContext_RecreatesContext_WhenSecretsStorePathChanges()
    {
        SettingsManager.Settings.UseMachineSpecificSecretsStore = false;
        var sharedContext = ProviderContextManager.EnsureProviderContext();

        SettingsManager.Settings.UseMachineSpecificSecretsStore = true;
        var machineSpecificContext = ProviderContextManager.EnsureProviderContext();

        Assert.Multiple(() =>
        {
            Assert.That(machineSpecificContext, Is.Not.SameAs(sharedContext));
            Assert.That(GetProviderContextSecretsPath(), Is.EqualTo(SettingsManager.SecretsStoreFilePath));
        });
    }

    private static void ResetProviderContext()
    {
        ProviderContextManager.ResetProviderContext();
    }

    private static string? GetProviderContextSecretsPath()
    {
        return typeof(ProviderContextManager)
            .GetField("_contextSecretsPath", BindingFlags.Static | BindingFlags.NonPublic)?
            .GetValue(null) as string;
    }
}
