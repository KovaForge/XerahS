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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShareX.XBackBone.Plugin;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class XBackBoneProviderTests
{
    [Test]
    public void Provider_ExposesExpectedMetadataAndCategories()
    {
        XBackBoneProvider provider = new();

        Assert.Multiple(() =>
        {
            Assert.That(provider.ProviderId, Is.EqualTo("xbackbone"));
            Assert.That(provider.Name, Is.EqualTo("XBackBone"));
            Assert.That(provider.ConfigModelType, Is.EqualTo(typeof(XBackBoneConfigModel)));
            Assert.That(provider.SupportedCategories, Is.EqualTo(new[]
            {
                UploaderCategory.Image,
                UploaderCategory.Text,
                UploaderCategory.File
            }));
            Assert.That(provider, Is.Not.InstanceOf<IUploaderExplorer>());
            Assert.That(provider.GetSupportedFileTypes().Keys, Is.EquivalentTo(provider.SupportedCategories));
        });
    }

    [Test]
    public void DefaultSettings_UsesStable3AndContainsNoToken()
    {
        XBackBoneProvider provider = new();

        string settingsJson = provider.GetDefaultSettings(UploaderCategory.File);
        XBackBoneConfigModel? config = JsonConvert.DeserializeObject<XBackBoneConfigModel>(settingsJson);
        JObject json = JObject.Parse(settingsJson);

        Assert.Multiple(() =>
        {
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.SecretKey, Is.Not.Empty);
            Assert.That(config.ServerUrl, Is.Empty);
            Assert.That(config.ApiGeneration, Is.EqualTo(XBackBoneApiGeneration.Stable3));
            Assert.That(json.Properties().Select(property => property.Name), Is.EquivalentTo(new[]
            {
                nameof(XBackBoneConfigModel.SecretKey),
                nameof(XBackBoneConfigModel.ServerUrl),
                nameof(XBackBoneConfigModel.ApiGeneration)
            }));
            Assert.That(settingsJson, Does.Not.Contain("ApiToken"));
            Assert.That(settingsJson, Does.Not.Contain("\"Token\""));
        });
    }

    [TestCase("https://xbackbone.example.invalid", XBackBoneApiGeneration.Stable3, true)]
    [TestCase("http://xbackbone.example.invalid/subpath", XBackBoneApiGeneration.ApiV1, true)]
    [TestCase("", XBackBoneApiGeneration.Stable3, false)]
    [TestCase("xbackbone.example.invalid", XBackBoneApiGeneration.Stable3, false)]
    [TestCase("ftp://xbackbone.example.invalid", XBackBoneApiGeneration.Stable3, false)]
    [TestCase("https://xbackbone.example.invalid", (XBackBoneApiGeneration)99, false)]
    public void ValidateSettings_RequiresAbsoluteHttpUrlStoredTokenAndKnownGeneration(
        string serverUrl,
        XBackBoneApiGeneration apiGeneration,
        bool expected)
    {
        const string secretKey = "xbackbone-test-secret";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("xbackbone", secretKey, "apiToken", "fake-token");
        XBackBoneProvider provider = CreateProvider(secrets);
        string settingsJson = JsonConvert.SerializeObject(new XBackBoneConfigModel
        {
            SecretKey = secretKey,
            ServerUrl = serverUrl,
            ApiGeneration = apiGeneration
        });

        bool actual = provider.ValidateSettings(settingsJson);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ValidateSettings_RejectsMissingStoredToken()
    {
        XBackBoneProvider provider = CreateProvider(new InMemorySecretStore());
        string settingsJson = JsonConvert.SerializeObject(new XBackBoneConfigModel
        {
            SecretKey = "missing-token-key",
            ServerUrl = "https://xbackbone.example.invalid",
            ApiGeneration = XBackBoneApiGeneration.Stable3
        });

        bool actual = provider.ValidateSettings(settingsJson);

        Assert.That(actual, Is.False);
    }

    [TestCase("ApiToken")]
    [TestCase("Token")]
    public void TryMigrateSecrets_MovesLegacyPlaintextAndRemovesProperty(string legacyPropertyName)
    {
        const string secretKey = "legacy-secret-key";
        const string token = "legacy-token|opaque";
        JObject input = new()
        {
            [nameof(XBackBoneConfigModel.SecretKey)] = secretKey,
            [nameof(XBackBoneConfigModel.ServerUrl)] = "https://xbackbone.example.invalid",
            [nameof(XBackBoneConfigModel.ApiGeneration)] = (int)XBackBoneApiGeneration.Stable3,
            [legacyPropertyName] = token
        };
        XBackBoneProvider provider = new();
        InMemorySecretStore secrets = new();

        bool changed = provider.TryMigrateSecrets(
            input.ToString(Formatting.None),
            secrets,
            out string updatedSettingsJson,
            out int migratedSecretCount);
        JObject updated = JObject.Parse(updatedSettingsJson);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(migratedSecretCount, Is.EqualTo(1));
            Assert.That(secrets.GetSecret("xbackbone", secretKey, "apiToken"), Is.EqualTo(token));
            Assert.That(updated.Property("ApiToken"), Is.Null);
            Assert.That(updated.Property("Token"), Is.Null);
            Assert.That(updatedSettingsJson, Does.Not.Contain(token));
            Assert.That(updated.Value<string>(nameof(XBackBoneConfigModel.SecretKey)), Is.EqualTo(secretKey));
        });
    }

    [Test]
    public void TryMigrateSecrets_AddsSecretKeyWhenLegacyConfigHasNone()
    {
        XBackBoneProvider provider = new();
        InMemorySecretStore secrets = new();

        bool changed = provider.TryMigrateSecrets(
            /*lang=json*/ "{ \"ServerUrl\": \"https://xbackbone.example.invalid\", \"Token\": \"fake-token\" }",
            secrets,
            out string updatedSettingsJson,
            out int migratedSecretCount);
        JObject updated = JObject.Parse(updatedSettingsJson);
        string? generatedSecretKey = updated.Value<string>(nameof(XBackBoneConfigModel.SecretKey));

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(migratedSecretCount, Is.EqualTo(1));
            Assert.That(generatedSecretKey, Is.Not.Null.And.Not.Empty);
            Assert.That(secrets.GetSecret("xbackbone", generatedSecretKey!, "apiToken"), Is.EqualTo("fake-token"));
        });
    }

    private static XBackBoneProvider CreateProvider(ISecretStore secrets)
    {
        XBackBoneProvider provider = new();
        provider.SetContext(new TestProviderContext(secrets));
        return provider;
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
