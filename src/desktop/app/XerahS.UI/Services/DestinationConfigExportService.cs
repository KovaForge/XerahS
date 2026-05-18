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
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Services;

internal static class DestinationConfigExportService
{
    private const int FormatVersion = 1;
    private const int KdfIterations = 210_000;
    private const string FormatName = "XerahS.DestinationConfig";

    public static string BuildEncryptedExport(UploaderInstance instance, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new InvalidOperationException("Passphrase is required.");
        }

        JObject payload = BuildPayload(instance);
        byte[] plainText = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            KdfIterations,
            HashAlgorithmName.SHA256,
            32);
        byte[] cipherText = new byte[plainText.Length];
        byte[] tag = new byte[16];

        using (var aes = new AesGcm(key, tag.Length))
        {
            aes.Encrypt(nonce, plainText, cipherText, tag);
        }

        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plainText);

        var envelope = new JObject
        {
            ["Format"] = FormatName,
            ["FormatVersion"] = FormatVersion,
            ["Encryption"] = new JObject
            {
                ["Method"] = "Passphrase",
                ["Kdf"] = "PBKDF2-HMAC-SHA256",
                ["Iterations"] = KdfIterations,
                ["Salt"] = Convert.ToBase64String(salt),
                ["Cipher"] = "AES-256-GCM",
                ["Nonce"] = Convert.ToBase64String(nonce),
                ["Tag"] = Convert.ToBase64String(tag)
            },
            ["Payload"] = Convert.ToBase64String(cipherText)
        };

        return envelope.ToString(Formatting.Indented);
    }

    private static JObject BuildPayload(UploaderInstance instance)
    {
        if (!string.Equals(instance.ProviderId, "amazons3", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Amazon S3 destinations can be exported to .xsdc currently.");
        }

        JObject settings;
        try
        {
            settings = JObject.Parse(instance.SettingsJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Amazon S3 settings are not valid JSON.", ex);
        }

        if (settings.Value<int?>("AuthMode").GetValueOrDefault(0) != 0)
        {
            throw new InvalidOperationException("AWS SSO destinations cannot be exported to mobile yet. Export an access-key S3 destination instead.");
        }

        string bucketName = settings.Value<string>("BucketName") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new InvalidOperationException("Amazon S3 bucket name is required before exporting to mobile.");
        }

        string secretKey = settings.Value<string>("SecretKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Amazon S3 secret key metadata is missing.");
        }

        var secrets = ProviderCatalog.GetProviderContext()?.Secrets;
        if (secrets == null)
        {
            throw new InvalidOperationException("Secret store is not available.");
        }

        string accessKeyId = secrets.GetSecret("amazons3", secretKey, "accessKeyId") ?? string.Empty;
        string secretAccessKey = secrets.GetSecret("amazons3", secretKey, "secretAccessKey") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
        {
            throw new InvalidOperationException("Amazon S3 credentials are missing from the secret store.");
        }

        string endpoint = settings.Value<string>("Endpoint") ?? string.Empty;
        string region = settings.Value<string>("Region") ?? string.Empty;
        string mobileEndpoint = IsAwsEndpoint(endpoint) ? string.Empty : endpoint;

        return new JObject
        {
            ["Format"] = "XerahS.DestinationConfig.Payload",
            ["FormatVersion"] = FormatVersion,
            ["Destinations"] = new JArray
            {
                new JObject
                {
                    ["ProviderId"] = "amazons3",
                    ["DisplayName"] = string.IsNullOrWhiteSpace(instance.DisplayName) ? "Amazon S3" : instance.DisplayName,
                    ["IsDefault"] = InstanceManager.Instance.IsDefaultInstance(instance.Category, instance.InstanceId),
                    ["Config"] = new JObject
                    {
                        ["AuthMode"] = "AccessKeys",
                        ["AccessKeyId"] = accessKeyId,
                        ["SecretAccessKey"] = secretAccessKey,
                        ["BucketName"] = bucketName,
                        ["Region"] = string.IsNullOrWhiteSpace(region) ? "us-east-1" : region,
                        ["Endpoint"] = mobileEndpoint,
                        ["UsePathStyle"] = settings.Value<bool?>("UsePathStyleUrl") ?? false,
                        ["UseCustomDomain"] = settings.Value<bool?>("UseCustomCNAME") ?? false,
                        ["CustomDomain"] = settings.Value<string>("CustomDomain") ?? string.Empty,
                        ["SignedPayload"] = settings.Value<bool?>("SignedPayload") ?? true,
                        ["SetPublicAcl"] = settings.Value<bool?>("SetPublicACL") ?? false
                    }
                }
            }
        };
    }

    private static bool IsAwsEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return true;
        }

        string host = endpoint.Trim();
        if (Uri.TryCreate(host.Contains("://", StringComparison.Ordinal) ? host : "https://" + host, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }

        return host.Equals("s3.amazonaws.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase);
    }
}
