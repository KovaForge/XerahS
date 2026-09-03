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
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using XerahS.Common;
using XerahS.Common.Utilities;
using XerahS.Core.Uploaders;
using XerahS.Core.Security;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Managers;

/// <summary>
/// Creates and restores a portable, intentionally unencrypted settings archive.
/// Secret values are plaintext inside the archive and are re-encrypted by the
/// destination computer's <see cref="ISecretStore"/> during restore.
/// </summary>
public static class PortableSettingsBackupService
{
    public const string FileExtension = "xsbak";
    public static string DefaultFileName => GetDefaultFileName(SystemInfo.GetApplicationVersion(), Environment.MachineName);

    private const string FormatName = "XerahS.SettingsBackup";
    private const int FormatVersion = 1;
    private const long MaximumArchiveBytes = 128L * 1024 * 1024;
    private const long MaximumExpandedBytes = 256L * 1024 * 1024;
    private const int MaximumEntryCount = 512;

    private const string ManifestEntryName = "manifest.json";
    private const string ApplicationEntryName = "settings/application.json";
    private const string UploadersEntryName = "settings/uploaders.json";
    private const string WorkflowsEntryName = "settings/workflows.json";
    private const string InstancesEntryName = "settings/uploader-instances.json";
    private const string SecretsEntryName = "settings/secrets.json";
    private const string AdditionalPrefix = "settings/additional/";
    private const string CustomUploadersPrefix = "custom-uploaders/";

    public static PortableSettingsBackupResult Create(string outputFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        outputFilePath = NormalizeBackupFilePath(outputFilePath);

        SettingsManager.SaveAllSettings();
        SettingsManager.UploadersConfig.SyncPolymorphicSettingsFromLegacy();

        ISecretStore secretStore = ProviderContextManager.EnsureProviderContext().Secrets;
        List<string> warnings = new();
        List<PortableSecret> secrets = CollectSecrets(secretStore, warnings);

        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [ApplicationEntryName] = ReadAllBytes(SettingsManager.Settings.SaveToMemoryStream()),
            [UploadersEntryName] = ReadAllBytes(SettingsManager.UploadersConfig.SaveToMemoryStream()),
            [WorkflowsEntryName] = ReadAllBytes(SettingsManager.WorkflowsConfig.SaveToMemoryStream()),
            [InstancesEntryName] = Encoding.UTF8.GetBytes(InstanceManager.Instance.ExportConfigurationJson()),
            [SecretsEntryName] = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(secrets, Formatting.Indented))
        };

        AddAdditionalSettingsFiles(content, outputFilePath);
        AddCustomUploaderDefinitions(content);

        var manifest = new BackupManifest
        {
            Format = FormatName,
            FormatVersion = FormatVersion,
            CreatedAtUtc = DateTime.UtcNow,
            ApplicationVersion = SystemInfo.GetApplicationVersion(),
            ContainsPlaintextSecrets = true,
            SecretCount = secrets.Count,
            Warnings = warnings,
            Files = content
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new BackupFileManifest
                {
                    Path = pair.Key,
                    Length = pair.Value.LongLength,
                    Sha256 = Convert.ToHexString(SHA256.HashData(pair.Value))
                })
                .ToList()
        };

        byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));
        WriteArchiveAtomically(outputFilePath, content, manifestBytes);

        return new PortableSettingsBackupResult(outputFilePath, secrets.Count, content.Count, warnings);
    }

    public static string GetDefaultFileName(string applicationVersion, string computerName)
    {
        string safeVersion = SanitizeFileNameSegment(applicationVersion, "unknown");
        string safeComputerName = SanitizeFileNameSegment(computerName, "unknown-computer");
        return $"xerahs-{safeVersion}-{safeComputerName}-backup.{FileExtension}";
    }

    public static string NormalizeBackupFilePath(string outputFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);

        string fullPath = Path.GetFullPath(outputFilePath);
        string expectedExtension = $".{FileExtension}";
        return string.Equals(Path.GetExtension(fullPath), expectedExtension, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : Path.ChangeExtension(fullPath, FileExtension);
    }

    public static PortableSettingsRestoreResult Restore(string inputFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);

        LoadedArchive archive = ReadAndValidateArchive(inputFilePath);
        string applicationJson = GetRequiredText(archive, ApplicationEntryName);
        string uploadersJson = GetRequiredText(archive, UploadersEntryName);
        string workflowsJson = GetRequiredText(archive, WorkflowsEntryName);
        string instancesJson = GetRequiredText(archive, InstancesEntryName);
        string secretsJson = GetRequiredText(archive, SecretsEntryName);

        JObject portableApplication = JObject.Parse(applicationJson);
        portableApplication[nameof(ApplicationConfig.CustomUploadersConfigPath)] = string.Empty;
        portableApplication[nameof(ApplicationConfig.CustomWorkflowsConfigPath)] = string.Empty;
        applicationJson = portableApplication.ToString(Formatting.Indented);

        ValidateSettings<ApplicationConfig>(applicationJson, "application settings");
        ValidateSettings<UploadersConfig>(uploadersJson, "uploader settings");
        ValidateSettings<WorkflowsConfig>(workflowsJson, "workflow settings");
        _ = JsonConvert.DeserializeObject<InstanceConfiguration>(instancesJson)
            ?? throw new InvalidDataException("Destination instance configuration is empty.");

        List<PortableSecret> secrets = JsonConvert.DeserializeObject<List<PortableSecret>>(secretsJson)
            ?? throw new InvalidDataException("Secret payload is empty.");
        ValidateSecrets(secrets);

        bool useMachineSpecificUploaders = portableApplication.Value<bool?>(nameof(ApplicationConfig.UseMachineSpecificUploadersConfig)) ?? false;
        bool useMachineSpecificWorkflows = portableApplication.Value<bool?>(nameof(ApplicationConfig.UseMachineSpecificWorkflowsConfig)) ?? false;
        string uploadersTargetPath = GetTargetConfigPath(
            SettingsManager.SettingsFolder,
            SettingsManager.UploadersConfigFileNamePrefix,
            SettingsManager.UploadersConfigFileNameExtension,
            SettingsManager.UploadersConfigFileName,
            useMachineSpecificUploaders);
        string workflowsTargetPath = GetTargetConfigPath(
            SettingsManager.SettingsFolder,
            SettingsManager.WorkflowsConfigFileNamePrefix,
            SettingsManager.WorkflowsConfigFileNameExtension,
            SettingsManager.WorkflowsConfigFileName,
            useMachineSpecificWorkflows);

        var replacements = new Dictionary<string, byte[]>(GetPathComparer())
        {
            [SettingsManager.ApplicationConfigFilePath] = Encoding.UTF8.GetBytes(applicationJson),
            [Path.Combine(SettingsManager.SettingsFolder, SettingsManager.UploadersConfigFileName)] = Encoding.UTF8.GetBytes(uploadersJson),
            [uploadersTargetPath] = Encoding.UTF8.GetBytes(uploadersJson),
            [Path.Combine(SettingsManager.SettingsFolder, SettingsManager.WorkflowsConfigFileName)] = Encoding.UTF8.GetBytes(workflowsJson),
            [workflowsTargetPath] = Encoding.UTF8.GetBytes(workflowsJson)
        };

        AddRestoredAuxiliaryFiles(archive, replacements);

        Dictionary<string, FileSnapshot> originalFiles = replacements.Keys
            .ToDictionary(path => path, CaptureFile, GetPathComparer());
        string originalInstancesJson = InstanceManager.Instance.ExportConfigurationJson();
        ISecretStore? restoredSecretStore = null;
        Dictionary<SecretIdentity, string?> originalSecretValues = new();

        try
        {
            foreach ((string path, byte[] bytes) in replacements)
            {
                WriteFileAtomically(path, bytes);
            }

            InstanceManager.Instance.ImportConfigurationJson(instancesJson);
            ProviderContextManager.ResetProviderContext();
            SettingsManager.LoadAllSettings();
            InstanceManager.Instance.ReloadConfiguration();

            restoredSecretStore = ProviderContextManager.EnsureProviderContext().Secrets;
            foreach (PortableSecret secret in secrets)
            {
                var identity = new SecretIdentity(secret.ProviderId, secret.SecretKey, secret.Name);
                originalSecretValues[identity] = restoredSecretStore.GetSecret(secret.ProviderId, secret.SecretKey, secret.Name);
                restoredSecretStore.SetSecret(secret.ProviderId, secret.SecretKey, secret.Name, secret.Value);
                string? restoredValue = restoredSecretStore.GetSecret(secret.ProviderId, secret.SecretKey, secret.Name);
                if (!string.Equals(restoredValue, secret.Value, StringComparison.Ordinal))
                {
                    throw new IOException($"The destination secret store rejected '{secret.ProviderId}:{secret.Name}'.");
                }
            }

            return new PortableSettingsRestoreResult(
                inputFilePath,
                secrets.Count,
                replacements.Count + 1,
                archive.Manifest.Warnings);
        }
        catch
        {
            if (restoredSecretStore != null)
            {
                RestoreSecrets(restoredSecretStore, originalSecretValues);
            }

            foreach ((string path, FileSnapshot snapshot) in originalFiles)
            {
                RestoreFile(path, snapshot);
            }

            InstanceManager.Instance.ImportConfigurationJson(originalInstancesJson);
            ProviderContextManager.ResetProviderContext();
            SettingsManager.LoadAllSettings();
            InstanceManager.Instance.ReloadConfiguration();
            throw;
        }
    }

    private static string SanitizeFileNameSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        const string invalidCharacters = "<>:\"/\\|?*";
        var builder = new StringBuilder(value.Length);
        bool previousWasSeparator = false;

        foreach (char character in value.Trim())
        {
            bool replace = char.IsControl(character) || invalidCharacters.Contains(character);
            char safeCharacter = replace ? '-' : character;
            if (safeCharacter == '-' && previousWasSeparator)
            {
                continue;
            }

            builder.Append(safeCharacter);
            previousWasSeparator = safeCharacter == '-';
        }

        string sanitized = builder.ToString().Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static List<PortableSecret> CollectSecrets(ISecretStore secretStore, List<string> warnings)
    {
        var secrets = new Dictionary<SecretIdentity, PortableSecret>();

        if (secretStore is SecretStore concreteStore)
        {
            foreach (PortableSecret secret in concreteStore.ExportIndexedSecrets())
            {
                secrets[new SecretIdentity(secret.ProviderId, secret.SecretKey, secret.Name)] = secret;
            }
        }

        foreach (UploaderInstance instance in InstanceManager.Instance.GetInstances())
        {
            IUploaderProvider? provider = ProviderCatalog.GetProvider(instance.ProviderId);
            if (provider == null)
            {
                warnings.Add($"Destination provider '{instance.ProviderId}' was not loaded; its unindexed secrets may be absent.");
                continue;
            }

            if (provider is not IInstanceSecretBackupProvider backupProvider)
            {
                continue;
            }

            foreach (InstanceSecretReference reference in backupProvider.GetSecretReferences(instance.SettingsJson))
            {
                string? value = secretStore.GetSecret(reference.ProviderId, reference.SecretKey, reference.Name);
                if (value != null)
                {
                    var portableSecret = new PortableSecret(reference.ProviderId, reference.SecretKey, reference.Name, value);
                    secrets[new SecretIdentity(reference.ProviderId, reference.SecretKey, reference.Name)] = portableSecret;
                }
            }
        }

        return secrets.Values
            .OrderBy(secret => secret.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(secret => secret.SecretKey, StringComparer.Ordinal)
            .ThenBy(secret => secret.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddAdditionalSettingsFiles(Dictionary<string, byte[]> content, string outputFilePath)
    {
        if (!Directory.Exists(SettingsManager.SettingsFolder))
        {
            return;
        }

        string normalizedOutputPath = Path.GetFullPath(outputFilePath);
        foreach (string filePath in Directory.EnumerateFiles(SettingsManager.SettingsFolder, "*", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(filePath);
            if (ShouldExcludeSettingsFile(fileName) ||
                string.Equals(Path.GetFullPath(filePath), normalizedOutputPath, GetPathComparison()))
            {
                continue;
            }

            content[AdditionalPrefix + fileName] = File.ReadAllBytes(filePath);
        }
    }

    private static bool ShouldExcludeSettingsFile(string fileName)
    {
        return fileName.Equals(SettingsManager.ApplicationConfigFileName, StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith(SettingsManager.UploadersConfigFileNamePrefix, StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith(SettingsManager.WorkflowsConfigFileNamePrefix, StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith(SettingsManager.SecretsStoreFileNamePrefix, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("uploader-instances.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("NetworkMonitor.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("MobileUploadQueue.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".temp", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCustomUploaderDefinitions(Dictionary<string, byte[]> content)
    {
        if (!Directory.Exists(PathsManager.PluginsFolder))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(PathsManager.PluginsFolder, "*.sxcu", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(PathsManager.PluginsFolder, filePath).Replace('\\', '/');
            if (IsSafeRelativePath(relativePath))
            {
                content[CustomUploadersPrefix + relativePath] = File.ReadAllBytes(filePath);
            }
        }
    }

    private static void WriteArchiveAtomically(
        string outputFilePath,
        Dictionary<string, byte[]> content,
        byte[] manifestBytes)
    {
        string fullOutputPath = Path.GetFullPath(outputFilePath);
        string? directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Backup output path must include a directory.", nameof(outputFilePath));
        }

        Directory.CreateDirectory(directory);
        string tempFilePath = Path.Combine(directory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.temp");
        try
        {
            using (FileStream stream = new(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                WriteZipEntry(zip, ManifestEntryName, manifestBytes);
                foreach ((string path, byte[] bytes) in content.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    WriteZipEntry(zip, path, bytes);
                }
            }

            File.Move(tempFilePath, fullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static void WriteZipEntry(ZipArchive zip, string path, byte[] bytes)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using Stream stream = entry.Open();
        stream.Write(bytes);
    }

    private static LoadedArchive ReadAndValidateArchive(string inputFilePath)
    {
        var file = new FileInfo(inputFilePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Settings backup file was not found.", inputFilePath);
        }

        if (file.Length > MaximumArchiveBytes)
        {
            throw new InvalidDataException("Settings backup exceeds the supported size limit.");
        }

        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long expandedBytes = 0;
        using FileStream stream = file.OpenRead();
        using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);
        if (zip.Entries.Count > MaximumEntryCount)
        {
            throw new InvalidDataException("Settings backup contains too many files.");
        }

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            if (!IsSafeArchivePath(path) || !entries.TryAdd(path, ReadZipEntry(entry, ref expandedBytes)))
            {
                throw new InvalidDataException($"Settings backup contains an invalid or duplicate entry: {entry.FullName}");
            }
        }

        if (!entries.TryGetValue(ManifestEntryName, out byte[]? manifestBytes))
        {
            throw new InvalidDataException("Settings backup manifest is missing.");
        }

        BackupManifest manifest = JsonConvert.DeserializeObject<BackupManifest>(Encoding.UTF8.GetString(manifestBytes))
            ?? throw new InvalidDataException("Settings backup manifest is invalid.");
        if (!string.Equals(manifest.Format, FormatName, StringComparison.Ordinal) || manifest.FormatVersion != FormatVersion)
        {
            throw new InvalidDataException("Settings backup format or version is not supported.");
        }

        if (!manifest.ContainsPlaintextSecrets)
        {
            throw new InvalidDataException("Settings backup secret handling declaration is invalid.");
        }

        var declaredPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (BackupFileManifest declaredFile in manifest.Files)
        {
            if (!IsSafeArchivePath(declaredFile.Path) ||
                !declaredPaths.Add(declaredFile.Path) ||
                !entries.TryGetValue(declaredFile.Path, out byte[]? bytes) ||
                bytes.LongLength != declaredFile.Length ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), declaredFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Settings backup file failed validation: {declaredFile.Path}");
            }
        }

        if (entries.Keys.Any(path => path != ManifestEntryName && !declaredPaths.Contains(path)))
        {
            throw new InvalidDataException("Settings backup contains undeclared files.");
        }

        return new LoadedArchive(manifest, entries);
    }

    private static byte[] ReadZipEntry(ZipArchiveEntry entry, ref long expandedBytes)
    {
        if (entry.Length < 0 || entry.Length > MaximumExpandedBytes - expandedBytes)
        {
            throw new InvalidDataException("Settings backup expands beyond the supported size limit.");
        }

        long remainingBytes = MaximumExpandedBytes - expandedBytes;
        using Stream source = entry.Open();
        using var destination = new MemoryStream((int)entry.Length);
        byte[] buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (destination.Length + bytesRead > remainingBytes)
            {
                throw new InvalidDataException("Settings backup expands beyond the supported size limit.");
            }

            destination.Write(buffer, 0, bytesRead);
        }

        expandedBytes += destination.Length;
        if (expandedBytes > MaximumExpandedBytes)
        {
            throw new InvalidDataException("Settings backup expands beyond the supported size limit.");
        }

        return destination.ToArray();
    }

    private static string GetRequiredText(LoadedArchive archive, string path)
    {
        if (!archive.Entries.TryGetValue(path, out byte[]? bytes))
        {
            throw new InvalidDataException($"Settings backup is missing required content: {path}");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void ValidateSettings<T>(string json, string description)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new XerahS.Common.Converters.SkColorJsonConverter());
            _ = JsonConvert.DeserializeObject<T>(json, settings)
                ?? throw new InvalidDataException($"The {description} payload is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The {description} payload is invalid.", ex);
        }
    }

    private static void ValidateSecrets(List<PortableSecret> secrets)
    {
        var identities = new HashSet<SecretIdentity>();
        foreach (PortableSecret secret in secrets)
        {
            var identity = new SecretIdentity(secret.ProviderId, secret.SecretKey, secret.Name);
            if (!identity.IsValid || secret.Value == null || !identities.Add(identity))
            {
                throw new InvalidDataException("Settings backup contains an invalid or duplicate secret record.");
            }
        }
    }

    private static void AddRestoredAuxiliaryFiles(
        LoadedArchive archive,
        Dictionary<string, byte[]> replacements)
    {
        foreach ((string entryPath, byte[] bytes) in archive.Entries)
        {
            if (entryPath.StartsWith(AdditionalPrefix, StringComparison.Ordinal))
            {
                string relativePath = entryPath[AdditionalPrefix.Length..];
                if (!IsSafeRelativePath(relativePath) || ShouldExcludeSettingsFile(Path.GetFileName(relativePath)))
                {
                    throw new InvalidDataException($"Settings backup contains an unsafe settings path: {entryPath}");
                }

                replacements[GetSafeDestinationPath(SettingsManager.SettingsFolder, relativePath)] = bytes;
            }
            else if (entryPath.StartsWith(CustomUploadersPrefix, StringComparison.Ordinal))
            {
                string relativePath = entryPath[CustomUploadersPrefix.Length..];
                if (!IsSafeRelativePath(relativePath) || !relativePath.EndsWith(".sxcu", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Settings backup contains an unsafe custom uploader path: {entryPath}");
                }

                replacements[GetSafeDestinationPath(PathsManager.PluginsFolder, relativePath)] = bytes;
            }
        }
    }

    private static string GetTargetConfigPath(
        string settingsFolder,
        string prefix,
        string extension,
        string defaultFileName,
        bool useMachineSpecific)
    {
        if (!useMachineSpecific)
        {
            return Path.Combine(settingsFolder, defaultFileName);
        }

        string machineName = FileHelpers.SanitizeFileName(Environment.MachineName);
        return string.IsNullOrWhiteSpace(machineName)
            ? Path.Combine(settingsFolder, defaultFileName)
            : Path.Combine(settingsFolder, $"{prefix}-{machineName}.{extension}");
    }

    private static string GetSafeDestinationPath(string rootPath, string relativePath)
    {
        string fullRoot = Path.GetFullPath(rootPath);
        string destination = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new InvalidDataException("Settings backup contains a path outside the destination folder.");
        }

        return destination;
    }

    private static bool IsSafeArchivePath(string path)
    {
        return IsSafeRelativePath(path) && !path.EndsWith('/');
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':'))
        {
            return false;
        }

        return path.Split('/', '\\').All(part => part.Length > 0 && part != "." && part != "..");
    }

    private static byte[] ReadAllBytes(MemoryStream stream)
    {
        using (stream)
        {
            return stream.ToArray();
        }
    }

    private static FileSnapshot CaptureFile(string path)
    {
        return File.Exists(path)
            ? new FileSnapshot(true, File.ReadAllBytes(path))
            : new FileSnapshot(false, Array.Empty<byte>());
    }

    private static void WriteFileAtomically(string path, byte[] bytes)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidDataException($"Invalid settings destination path: {path}");
        }

        Directory.CreateDirectory(directory);
        string tempFilePath = path + $".{Guid.NewGuid():N}.temp";
        try
        {
            File.WriteAllBytes(tempFilePath, bytes);
            File.Move(tempFilePath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static void RestoreFile(string path, FileSnapshot snapshot)
    {
        if (snapshot.Existed)
        {
            WriteFileAtomically(path, snapshot.Bytes);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void RestoreSecrets(ISecretStore store, Dictionary<SecretIdentity, string?> values)
    {
        foreach ((SecretIdentity identity, string? value) in values)
        {
            if (value == null)
            {
                store.DeleteSecret(identity.ProviderId, identity.SecretKey, identity.Name);
            }
            else
            {
                store.SetSecret(identity.ProviderId, identity.SecretKey, identity.Name, value);
            }
        }
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed class BackupManifest
    {
        public string Format { get; set; } = string.Empty;
        public int FormatVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string ApplicationVersion { get; set; } = string.Empty;
        public bool ContainsPlaintextSecrets { get; set; }
        public int SecretCount { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<BackupFileManifest> Files { get; set; } = new();
    }

    private sealed class BackupFileManifest
    {
        public string Path { get; set; } = string.Empty;
        public long Length { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    private sealed record LoadedArchive(BackupManifest Manifest, Dictionary<string, byte[]> Entries);
    private sealed record FileSnapshot(bool Existed, byte[] Bytes);

    private readonly record struct SecretIdentity(string ProviderId, string SecretKey, string Name)
    {
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ProviderId) &&
            !string.IsNullOrWhiteSpace(SecretKey) &&
            !string.IsNullOrWhiteSpace(Name);

        public bool Equals(SecretIdentity other) =>
            string.Equals(ProviderId, other.ProviderId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(SecretKey, other.SecretKey, StringComparison.Ordinal) &&
            string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(ProviderId ?? string.Empty),
            StringComparer.Ordinal.GetHashCode(SecretKey ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty));
    }
}

public sealed record PortableSettingsBackupResult(
    string FilePath,
    int SecretCount,
    int FileCount,
    IReadOnlyList<string> Warnings);

public sealed record PortableSettingsRestoreResult(
    string FilePath,
    int SecretCount,
    int FileCount,
    IReadOnlyList<string> Warnings);
