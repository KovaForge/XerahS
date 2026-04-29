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
    public void LoadAllSettings_InitializesRecentTasksFromApplicationConfig()
    {
        var savedTask = new RecentTask
        {
            FilePath = "saved-capture.png",
            URL = "https://example.test/saved-capture.png"
        };

        SettingsManager.Settings.RecentTasksSave = true;
        SettingsManager.Settings.RecentTasksMaxCount = 5;
        SettingsManager.RecentTaskManager.Add(savedTask);
        SettingsManager.SaveApplicationConfig();
        SettingsManager.RecentTaskManager.Clear();

        SettingsManager.LoadAllSettings();

        RecentTask[] loadedTasks = SettingsManager.RecentTaskManager.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(loadedTasks, Has.Length.EqualTo(1));
            Assert.That(loadedTasks[0].FilePath, Is.EqualTo(savedTask.FilePath));
            Assert.That(loadedTasks[0].URL, Is.EqualTo(savedTask.URL));
        });
    }

    [Test]
    public void LoadAllSettings_ClearsRecentTasksWhenApplicationConfigHasNone()
    {
        SettingsManager.RecentTaskManager.Add(new RecentTask
        {
            FilePath = "stale-capture.png",
            URL = "https://example.test/stale-capture.png"
        });

        SettingsManager.Settings.RecentTasksSave = false;
        SettingsManager.Settings.RecentTasks = null;
        SettingsManager.SaveApplicationConfig();

        SettingsManager.LoadAllSettings();

        Assert.That(SettingsManager.RecentTaskManager.ToArray(), Is.Empty);
    }

    [Test]
    public void SaveApplicationConfigAsync_RaisesSettingsChangedLikeSynchronousSave()
    {
        int raisedCount = 0;
        EventHandler handler = (_, _) => raisedCount++;

        SettingsManager.SettingsChanged += handler;
        try
        {
            SettingsManager.SaveApplicationConfigAsync();
        }
        finally
        {
            SettingsManager.SettingsChanged -= handler;
        }

        Assert.That(raisedCount, Is.EqualTo(1));
    }

    [Test]
    public void SaveToMemoryStream_ReturnsStreamReadyForReading()
    {
        using MemoryStream stream = SettingsManager.Settings.SaveToMemoryStream();
        long initialPosition = stream.Position;
        using StreamReader reader = new(stream);

        string json = reader.ReadToEnd();

        Assert.Multiple(() =>
        {
            Assert.That(initialPosition, Is.EqualTo(0),
                "The returned stream should be readable immediately without callers seeking first.");
            Assert.That(json, Does.Contain(nameof(SettingsManager.Settings.ApplicationVersion)));
        });
    }

    [Test]
    public void Save_CreatesWeeklyBackup_WhenWeeklyBackupEnabledWithoutDailyBackup()
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "settings-backup-tests", Guid.NewGuid().ToString("N"));
        string backupDirectory = Path.Combine(directory, "Backup");
        string configPath = Path.Combine(directory, "ApplicationConfig.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(configPath, "{\"ApplicationVersion\":\"0.0.1\"}");

        var config = new ApplicationConfig
        {
            BackupFolder = backupDirectory,
            CreateBackup = false,
            CreateWeeklyBackup = true
        };

        Assert.That(config.Save(configPath), Is.True);

        string[] backupFiles = Directory.GetFiles(backupDirectory, "backup-*-W*.zip", SearchOption.AllDirectories);

        Assert.That(backupFiles, Has.Length.EqualTo(1),
            "Weekly backups should not depend on the separate daily-backup flag being enabled.");

        using ZipArchive archive = ZipFile.OpenRead(backupFiles[0]);
        Assert.That(archive.GetEntry(Path.GetFileName(configPath)), Is.Not.Null);
    }

    [Test]
    public void IsUpgradeFrom_UsesNumericVersionComparison()
    {
        var config = new ApplicationConfig
        {
            ApplicationVersion = "0.10.0"
        };
        typeof(SettingsBase<ApplicationConfig>)
            .GetField("<IsUpgrade>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(config, true);

        Assert.Multiple(() =>
        {
            Assert.That(config.IsUpgradeFrom("0.9.0"), Is.False,
                "Version 0.10.0 should not be treated as older than 0.9.0 by lexicographic comparison.");
            Assert.That(config.IsUpgradeFrom("0.10.0"), Is.True);
            Assert.That(config.IsUpgradeFrom("0.11.0"), Is.True);
        });
    }

    [Test]
    public void IsUpgradeFrom_ReturnsFalseWhenNotUpgrade()
    {
        var config = new ApplicationConfig
        {
            ApplicationVersion = "0.1.0"
        };

        Assert.That(config.IsUpgradeFrom("9.0.0"), Is.False);
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
