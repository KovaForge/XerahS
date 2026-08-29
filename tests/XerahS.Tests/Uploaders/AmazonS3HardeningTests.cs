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

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using NUnit.Framework;
using ShareX.AmazonS3.Plugin;
using ShareX.AmazonS3.Plugin.ViewModels;
using System.Net;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class AmazonS3HardeningTests
{
    [Test]
    public void Upload_UsesAwsSdkForSinglePutAndPreservesOptions()
    {
        S3ConfigModel config = new()
        {
            BucketName = "xerahs-tests",
            Region = "us-east-1",
            Endpoint = "https://s3.example.invalid",
            ObjectPrefix = "uploads",
            StorageClass = ShareX.AmazonS3.Plugin.S3StorageClass.StandardInfrequentAccess,
            SetPublicACL = true,
            SignedPayload = false,
            MultipartThresholdBytes = 50L * 1024 * 1024
        };
        FakeAmazonS3Client client = new();
        AmazonS3Uploader uploader = new(config, "access-key", "secret-key", null, () => client);
        using MemoryStream stream = new(new byte[] { 1, 2, 3, 4 });

        UploadResult result = uploader.Upload(stream, "test.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.URL, Is.EqualTo("https://s3.example.invalid/xerahs-tests/uploads/test.txt"));
            Assert.That(client.PutRequest, Is.Not.Null);
            Assert.That(client.PutRequest!.BucketName, Is.EqualTo(config.BucketName));
            Assert.That(client.PutRequest.Key, Is.EqualTo("uploads/test.txt"));
            Assert.That(client.PutRequest.InputStream, Is.SameAs(stream));
            Assert.That(client.PutRequest.ContentType, Is.EqualTo("text/plain"));
            Assert.That(client.PutRequest.CannedACL, Is.EqualTo(S3CannedACL.PublicRead));
            Assert.That(client.PutRequest.DisablePayloadSigning, Is.True);
            Assert.That(client.PutRequest.StorageClass, Is.EqualTo(Amazon.S3.S3StorageClass.StandardInfrequentAccess));
        });
    }

    [Test]
    public void ShouldUseMultipart_AlwaysProtectsS3SinglePutLimit()
    {
        S3ConfigModel config = new()
        {
            MultipartThresholdBytes = long.MaxValue
        };
        AmazonS3Uploader uploader = new(config, "access-key", "secret-key");

        Assert.That(uploader.ShouldUseMultipart(5L * 1024 * 1024 * 1024), Is.False);
        Assert.That(uploader.ShouldUseMultipart(5L * 1024 * 1024 * 1024 + 1), Is.True);
    }

    [Test]
    public void Validate_SsoModeKeepsObjectAclDisabled()
    {
        const string secretKey = "sso-config-secret";
        InMemorySecretStore secrets = new();
        AwsSsoSecretStore.SaveToken(secrets, secretKey, new AwsSsoStoredToken
        {
            AccessToken = "temporary-access-token",
            RefreshToken = "temporary-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        });
        S3ConfigModel config = new()
        {
            AuthMode = S3AuthMode.AwsSso,
            SecretKey = secretKey,
            Endpoint = "s3.amazonaws.com",
            Region = "us-east-1",
            CustomDomain = "cdn.example.com",
            UseCustomCNAME = true,
            SetPublicACL = false,
            SetPublicPolicy = true,
            SsoRegion = "us-east-1",
            SsoAccountId = "123456789012",
            SsoRoleName = "Uploader"
        };
        AmazonS3ConfigViewModel viewModel = new();
        viewModel.LoadFromJson(JsonConvert.SerializeObject(config));
        viewModel.SetContext(new TestProviderContext(secrets));

        bool isValid = viewModel.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True, viewModel.StatusMessage);
            Assert.That(viewModel.SetPublicACL, Is.False);
        });
    }

    [Test]
    public void RoleCredentials_AreBoundToSelectedAccountAndRole()
    {
        AwsSsoStoredRoleCredentials credentials = new()
        {
            AccountId = "123456789012",
            RoleName = "Uploader",
            AccessKeyId = "temporary-access-key",
            SecretAccessKey = "temporary-secret-key",
            SessionToken = "temporary-session-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
        };

        Assert.Multiple(() =>
        {
            Assert.That(credentials.IsUsableFor("123456789012", "Uploader"), Is.True);
            Assert.That(credentials.IsUsableFor("999999999999", "Uploader"), Is.False);
            Assert.That(credentials.IsUsableFor("123456789012", "Administrator"), Is.False);
        });
    }

    [Test]
    public void SsoSecretStore_RemovesMalformedCachedJson()
    {
        const string secretKey = "malformed-sso-secret";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("amazons3", secretKey, "ssoToken", "not-json");

        AwsSsoStoredToken? token = AwsSsoSecretStore.LoadToken(secrets, secretKey);

        Assert.That(token, Is.Null);
        Assert.That(secrets.HasSecret("amazons3", secretKey, "ssoToken"), Is.False);
    }

    [Test]
    public void AccessKeySecrets_CanResolveByDestinationAlias()
    {
        InMemorySecretStore secrets = new();
        S3ConfigModel first = new()
        {
            SecretKey = "first-secret-key",
            Endpoint = "s3.amazonaws.com",
            Region = "us-east-1",
            BucketName = "xerahs-tests"
        };
        S3ConfigModel second = new()
        {
            SecretKey = "second-secret-key",
            Endpoint = first.Endpoint,
            Region = first.Region,
            BucketName = first.BucketName
        };
        S3CredentialSecrets.StoreAccessKeyCredentials(secrets, first, "access-key", "secret-key");

        bool found = S3CredentialSecrets.TryGetAccessKeyCredentials(
            secrets,
            second,
            out string accessKeyId,
            out string secretAccessKey);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(accessKeyId, Is.EqualTo("access-key"));
            Assert.That(secretAccessKey, Is.EqualTo("secret-key"));
        });
    }

    private sealed class FakeAmazonS3Client : AmazonS3Client
    {
        public FakeAmazonS3Client()
            : base(new AnonymousAWSCredentials(), new AmazonS3Config
            {
                ServiceURL = "https://example.invalid",
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true
            })
        {
        }

        public PutObjectRequest? PutRequest { get; private set; }

        public override Task<PutObjectResponse> PutObjectAsync(
            PutObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            PutRequest = request;
            return Task.FromResult(new PutObjectResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                ETag = "test-etag"
            });
        }
    }

    private sealed class TestProviderContext : IProviderContext
    {
        public TestProviderContext(ISecretStore secrets)
        {
            Secrets = secrets;
        }

        public ISecretStore Secrets { get; }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<(string ProviderId, string SecretKey, string Name), string> _values = new();

        public string? GetSecret(string providerId, string secretKey, string name)
            => _values.TryGetValue((providerId, secretKey, name), out string? value) ? value : null;

        public void SetSecret(string providerId, string secretKey, string name, string value)
            => _values[(providerId, secretKey, name)] = value;

        public void DeleteSecret(string providerId, string secretKey, string name)
            => _values.Remove((providerId, secretKey, name));

        public bool HasSecret(string providerId, string secretKey, string name)
            => _values.ContainsKey((providerId, secretKey, name));
    }
}
