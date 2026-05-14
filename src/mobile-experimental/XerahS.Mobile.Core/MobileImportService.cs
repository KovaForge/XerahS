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

using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.Core;

public sealed class MobileImportResult
{
    public List<string> UploadPaths { get; } = [];
    public List<string> Messages { get; } = [];
}

public static class MobileImportService
{
    private const string AmazonS3ProviderId = "amazons3";

    public static MobileImportResult ImportFiles(IEnumerable<string> filePaths, string? xsdcPassphrase = null)
    {
        var result = new MobileImportResult();

        foreach (var filePath in filePaths)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".sxcu":
                        result.Messages.Add(ImportCustomUploader(filePath));
                        break;
                    case ".xsdc":
                        result.Messages.Add(string.IsNullOrWhiteSpace(xsdcPassphrase)
                            ? "Destination config import requires a passphrase."
                            : ImportDestinationConfig(filePath, xsdcPassphrase));
                        break;
                    default:
                        result.UploadPaths.Add(filePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"[MobileImport] Failed to import {filePath}");
                result.Messages.Add($"Failed to import {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        return result;
    }

    public static async Task<string> ImportRemoteCustomUploaderAsync(Uri uri, string cacheFolder)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return "Invalid import link. Missing or invalid remote .sxcu URL.";

        Directory.CreateDirectory(cacheFolder);
        var fileName = string.IsNullOrWhiteSpace(Path.GetFileName(uri.LocalPath))
            ? $"remote_{Guid.NewGuid():N}.sxcu"
            : Path.GetFileName(uri.LocalPath);
        if (!fileName.EndsWith(".sxcu", StringComparison.OrdinalIgnoreCase))
            fileName += ".sxcu";

        var targetPath = Path.Combine(cacheFolder, fileName);
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(uri).ConfigureAwait(false);
        await File.WriteAllBytesAsync(targetPath, bytes).ConfigureAwait(false);
        return ImportCustomUploader(targetPath);
    }

    public static string ImportCustomUploader(string filePath)
    {
        var loaded = CustomUploaderRepository.LoadFromFile(filePath);
        if (!loaded.IsValid)
            return $"The file {Path.GetFileName(filePath)} is not a valid .sxcu definition.";

        var item = loaded.Item;
        var config = SettingsManager.UploadersConfig ?? throw new InvalidOperationException("Uploaders config is not loaded.");
        var list = config.CustomUploadersList;
        var existingIndex = list.FindIndex(existing =>
            string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.RequestURL, item.RequestURL, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            list[existingIndex] = item;
            SaveCustomUploaderFile(item);
            if (existingIndex == config.CustomImageUploaderSelected)
                ActivateCustomUploader(item, existingIndex);
            SettingsManager.SaveUploadersConfig();
            return $"Updated custom uploader: {item}";
        }

        list.Add(item);
        var newIndex = list.Count - 1;
        var savedPath = SaveCustomUploaderFile(item);
        if (list.Count == 1)
        {
            config.CustomImageUploaderSelected = 0;
            ActivateCustomUploader(item, newIndex, savedPath);
        }

        SettingsManager.SaveUploadersConfig();
        return $"Imported custom uploader: {item}";
    }

    public static string ImportDestinationConfig(string filePath, string passphrase)
    {
        var payload = DecryptDestinationConfig(File.ReadAllText(filePath), passphrase);
        var destinations = payload["Destinations"] as JArray ?? throw new InvalidOperationException("No mobile-compatible destination was found in this .xsdc file.");
        var destination = destinations
            .OfType<JObject>()
            .FirstOrDefault(item =>
                string.Equals(item.Value<string>("ProviderId"), AmazonS3ProviderId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item["Config"]?.Value<string>("AuthMode"), "AccessKeys", StringComparison.OrdinalIgnoreCase));

        if (destination == null)
            throw new InvalidOperationException("No mobile-compatible destination was found in this .xsdc file.");

        ImportS3Destination(destination);
        var displayName = destination.Value<string>("DisplayName");
        return $"Imported destination config: {(!string.IsNullOrWhiteSpace(displayName) ? displayName : "Amazon S3")}";
    }

    private static string SaveCustomUploaderFile(CustomUploaderItem item)
    {
        var pluginsFolder = PathsManager.PluginsFolder;
        Directory.CreateDirectory(pluginsFolder);

        var baseFileName = item.GetFileName();
        var filePath = Path.Combine(pluginsFolder, baseFileName);
        var suffix = 1;
        while (File.Exists(filePath))
        {
            var loaded = CustomUploaderRepository.LoadFromFile(filePath);
            if (loaded.IsValid &&
                string.Equals(loaded.Item.Name, item.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loaded.Item.RequestURL, item.RequestURL, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            filePath = Path.Combine(
                pluginsFolder,
                $"{Path.GetFileNameWithoutExtension(baseFileName)}_{suffix}{Path.GetExtension(baseFileName)}");
            suffix++;
        }

        CustomUploaderRepository.SaveToFile(item, filePath);
        return filePath;
    }

    private static void ActivateCustomUploader(CustomUploaderItem item, int index, string? filePath = null)
    {
        filePath ??= SaveCustomUploaderFile(item);
        var loadedUploader = new LoadedCustomUploader(item, filePath);
        var provider = new CustomUploaderProvider(loadedUploader);
        ProviderCatalog.RegisterProvider(provider);

        var instanceManager = InstanceManager.Instance;
        var existing = instanceManager.GetInstancesByCategory(UploaderCategory.Image)
            .FirstOrDefault(i => i.ProviderId == provider.ProviderId);
        if (existing == null)
        {
            instanceManager.AddInstance(new UploaderInstance
            {
                ProviderId = provider.ProviderId,
                Category = UploaderCategory.Image,
                DisplayName = item.ToString(),
                SettingsJson = provider.GetDefaultSettings(UploaderCategory.Image)
            });
            existing = instanceManager.GetInstancesByCategory(UploaderCategory.Image)
                .FirstOrDefault(i => i.ProviderId == provider.ProviderId);
        }

        if (existing == null) return;
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, existing.InstanceId);
        SettingsManager.UploadersConfig.CustomImageUploaderSelected = index;
        SettingsManager.DefaultTaskSettings.DestinationInstanceId = existing.InstanceId;
        SettingsManager.SaveWorkflowsConfig();
    }

    private static JObject DecryptDestinationConfig(string json, string passphrase)
    {
        var envelope = JObject.Parse(json);
        if (!string.Equals(envelope.Value<string>("Format"), "XerahS.DestinationConfig", StringComparison.Ordinal) ||
            envelope.Value<int?>("FormatVersion") != 1)
        {
            throw new InvalidOperationException("The .xsdc file is not a valid XerahS destination config.");
        }

        var encryption = envelope["Encryption"] as JObject ?? throw new InvalidOperationException("This .xsdc encryption method is not supported.");
        if (!string.Equals(encryption.Value<string>("Method"), "Passphrase", StringComparison.Ordinal) ||
            !string.Equals(encryption.Value<string>("Kdf"), "PBKDF2-HMAC-SHA256", StringComparison.Ordinal) ||
            !string.Equals(encryption.Value<string>("Cipher"), "AES-256-GCM", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("This .xsdc encryption method is not supported.");
        }

        try
        {
            var salt = Convert.FromBase64String(encryption.Value<string>("Salt") ?? "");
            var nonce = Convert.FromBase64String(encryption.Value<string>("Nonce") ?? "");
            var tag = Convert.FromBase64String(encryption.Value<string>("Tag") ?? "");
            var cipherText = Convert.FromBase64String(envelope.Value<string>("Payload") ?? "");
            var iterations = encryption.Value<int>("Iterations");

            var key = Rfc2898DeriveBytes.Pbkdf2(
                passphrase,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32);
            var plainText = new byte[cipherText.Length];
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, cipherText, tag, plainText);
            return JObject.Parse(System.Text.Encoding.UTF8.GetString(plainText));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            throw new InvalidOperationException("The passphrase is incorrect or the file is damaged.", ex);
        }
    }

    private static void ImportS3Destination(JObject destination)
    {
        var config = destination["Config"] as JObject ?? throw new InvalidOperationException("No mobile-compatible destination was found in this .xsdc file.");
        var secrets = ProviderCatalog.GetProviderContext()?.Secrets ?? throw new InvalidOperationException("Secret storage is not available.");
        var secretKey = Guid.NewGuid().ToString("N");
        secrets.SetSecret(AmazonS3ProviderId, secretKey, "accessKeyId", config.Value<string>("AccessKeyId") ?? "");
        secrets.SetSecret(AmazonS3ProviderId, secretKey, "secretAccessKey", config.Value<string>("SecretAccessKey") ?? "");

        var settingsJson = new JObject
        {
            ["AuthMode"] = 0,
            ["SecretKey"] = secretKey,
            ["BucketName"] = config.Value<string>("BucketName") ?? "",
            ["Region"] = config.Value<string>("Region") ?? "us-east-1",
            ["Endpoint"] = config.Value<string>("Endpoint") ?? "s3.amazonaws.com",
            ["ObjectPrefix"] = "ShareX/%y/%mo",
            ["UsePathStyleUrl"] = config.Value<bool?>("UsePathStyle") ?? false,
            ["UseCustomCNAME"] = config.Value<bool?>("UseCustomDomain") ?? false,
            ["CustomDomain"] = config.Value<string>("CustomDomain") ?? "",
            ["StorageClass"] = 0,
            ["SetPublicACL"] = config.Value<bool?>("SetPublicAcl") ?? false,
            ["SetPublicPolicy"] = false,
            ["SignedPayload"] = config.Value<bool?>("SignedPayload") ?? true,
            ["RemoveExtensionImage"] = false,
            ["RemoveExtensionVideo"] = false,
            ["RemoveExtensionText"] = false
        };

        var instanceManager = InstanceManager.Instance;
        var existing = instanceManager.GetInstances().FirstOrDefault(i =>
            string.Equals(i.ProviderId, AmazonS3ProviderId, StringComparison.OrdinalIgnoreCase) &&
            i.Category == UploaderCategory.File);
        if (existing != null)
        {
            existing.DisplayName = destination.Value<string>("DisplayName") ?? "Amazon S3";
            existing.SettingsJson = settingsJson.ToString(Formatting.Indented);
            instanceManager.UpdateInstance(existing);
            if (destination.Value<bool?>("IsDefault") == true)
                instanceManager.SetDefaultInstance(UploaderCategory.File, existing.InstanceId);
            return;
        }

        var instance = new UploaderInstance
        {
            ProviderId = AmazonS3ProviderId,
            Category = UploaderCategory.File,
            DisplayName = destination.Value<string>("DisplayName") ?? "Amazon S3",
            SettingsJson = settingsJson.ToString(Formatting.Indented),
            FileTypeRouting = new FileTypeScope { AllFileTypes = true }
        };
        instanceManager.AddInstance(instance);
        instanceManager.SetDefaultInstance(UploaderCategory.File, instance.InstanceId);
    }
}
