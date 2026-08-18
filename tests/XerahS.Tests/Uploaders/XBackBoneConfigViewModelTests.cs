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
using ShareX.XBackBone.Plugin.ViewModels;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class XBackBoneConfigViewModelTests
{
    [Test]
    public void ToJson_PersistsTokenOnlyInSecretStore()
    {
        const string secretKey = "view-model-secret-key";
        const string apiToken = "fake-token|opaque";
        InMemorySecretStore secrets = new();
        XBackBoneConfigViewModel viewModel = CreateViewModel(secrets, new XBackBoneConfigModel
        {
            SecretKey = secretKey,
            ServerUrl = "https://xbackbone.example.invalid/subpath/",
            ApiGeneration = XBackBoneApiGeneration.ApiV1
        });
        viewModel.ApiToken = apiToken;

        string settingsJson = viewModel.ToJson();
        JObject json = JObject.Parse(settingsJson);

        Assert.Multiple(() =>
        {
            Assert.That(secrets.GetSecret("xbackbone", secretKey, "apiToken"), Is.EqualTo(apiToken));
            Assert.That(json.Properties().Select(property => property.Name), Is.EquivalentTo(new[]
            {
                nameof(XBackBoneConfigModel.SecretKey),
                nameof(XBackBoneConfigModel.ServerUrl),
                nameof(XBackBoneConfigModel.ApiGeneration)
            }));
            Assert.That(json.Value<string>(nameof(XBackBoneConfigModel.ServerUrl)),
                Is.EqualTo("https://xbackbone.example.invalid/subpath"));
            Assert.That(settingsJson, Does.Not.Contain(apiToken));
            Assert.That(settingsJson, Does.Not.Contain("ApiToken"));
            Assert.That(settingsJson, Does.Not.Contain("\"Token\""));
        });
    }

    [Test]
    public void LoadFromJson_RestoresStoredTokenAndGeneration()
    {
        const string secretKey = "stored-token-key";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("xbackbone", secretKey, "apiToken", "stored-token|opaque");
        XBackBoneConfigViewModel viewModel = CreateViewModel(secrets, new XBackBoneConfigModel
        {
            SecretKey = secretKey,
            ServerUrl = "https://xbackbone.example.invalid",
            ApiGeneration = XBackBoneApiGeneration.ApiV1
        });

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ApiToken, Is.EqualTo("stored-token|opaque"));
            Assert.That(viewModel.ApiGenerationIndex, Is.EqualTo(1));
            Assert.That(viewModel.TokenSummary, Does.Contain("stored securely"));
            Assert.That(viewModel.Validate(), Is.True);
        });
    }

    [Test]
    public void LoadFromJson_DifferentSecretKeyClearsStaleToken()
    {
        const string firstSecretKey = "first-token-key";
        const string secondSecretKey = "second-token-key";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("xbackbone", firstSecretKey, "apiToken", "first-token");
        XBackBoneConfigViewModel viewModel = CreateViewModel(secrets, new XBackBoneConfigModel
        {
            SecretKey = firstSecretKey,
            ServerUrl = "https://xbackbone.example.invalid",
            ApiGeneration = XBackBoneApiGeneration.Stable3
        });
        Assert.That(viewModel.ApiToken, Is.EqualTo("first-token"));

        viewModel.LoadFromJson(JsonConvert.SerializeObject(new XBackBoneConfigModel
        {
            SecretKey = secondSecretKey,
            ServerUrl = "https://xbackbone.example.invalid",
            ApiGeneration = XBackBoneApiGeneration.Stable3
        }));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ApiToken, Is.Empty);
            Assert.That(viewModel.TokenSummary, Is.EqualTo("No API token is stored."));
            Assert.That(viewModel.Validate(), Is.False);
        });
    }

    [Test]
    public void ClearStoredToken_RemovesSecretAndUpdatesValidationState()
    {
        const string secretKey = "clear-token-key";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("xbackbone", secretKey, "apiToken", "stored-token");
        XBackBoneConfigViewModel viewModel = CreateViewModel(secrets, new XBackBoneConfigModel
        {
            SecretKey = secretKey,
            ServerUrl = "https://xbackbone.example.invalid",
            ApiGeneration = XBackBoneApiGeneration.Stable3
        });

        viewModel.ClearStoredTokenCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(secrets.HasSecret("xbackbone", secretKey, "apiToken"), Is.False);
            Assert.That(viewModel.ApiToken, Is.Empty);
            Assert.That(viewModel.TokenSummary, Is.EqualTo("No API token is stored."));
            Assert.That(viewModel.StatusMessage, Is.EqualTo("Stored XBackBone API token was cleared."));
            Assert.That(viewModel.Validate(), Is.False);
        });
    }

    [TestCase("", 0, "token", "valid http:// or https:// URL")]
    [TestCase("xbackbone.example.invalid", 0, "token", "valid http:// or https:// URL")]
    [TestCase("ftp://xbackbone.example.invalid", 0, "token", "valid http:// or https:// URL")]
    [TestCase("https://xbackbone.example.invalid", -1, "token", "supported XBackBone API generation")]
    [TestCase("https://xbackbone.example.invalid", 2, "token", "supported XBackBone API generation")]
    [TestCase("https://xbackbone.example.invalid", 0, "", "API token is required")]
    [TestCase("https://xbackbone.example.invalid", 0, "   ", "API token is required")]
    public void Validate_RejectsInvalidConfiguration(
        string serverUrl,
        int apiGenerationIndex,
        string apiToken,
        string expectedMessage)
    {
        XBackBoneConfigViewModel viewModel = new()
        {
            ServerUrl = serverUrl,
            ApiGenerationIndex = apiGenerationIndex,
            ApiToken = apiToken
        };

        bool valid = viewModel.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(viewModel.StatusMessage, Does.Contain(expectedMessage));
        });
    }

    [TestCase("https://xbackbone.example.invalid/?page=1#uploads", "https://xbackbone.example.invalid")]
    [TestCase("http://xbackbone.example.invalid/subpath/", "http://xbackbone.example.invalid/subpath")]
    public void Validate_AcceptsHttpAndHttpsAndNormalizesUrl(string serverUrl, string expectedServerUrl)
    {
        XBackBoneConfigViewModel viewModel = new()
        {
            ServerUrl = serverUrl,
            ApiGenerationIndex = 0,
            ApiToken = "fake-token"
        };

        bool valid = viewModel.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(viewModel.ServerUrl, Is.EqualTo(expectedServerUrl));
            Assert.That(viewModel.StatusMessage, Is.Null);
        });
    }

    [Test]
    public void LoadFromJson_UnknownGenerationIsRejectedByValidation()
    {
        InMemorySecretStore secrets = new();
        const string secretKey = "unknown-generation-key";
        secrets.SetSecret("xbackbone", secretKey, "apiToken", "fake-token");
        XBackBoneConfigViewModel viewModel = CreateViewModel(secrets, new XBackBoneConfigModel
        {
            SecretKey = secretKey,
            ServerUrl = "https://xbackbone.example.invalid",
            ApiGeneration = (XBackBoneApiGeneration)99
        });

        bool valid = viewModel.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ApiGenerationIndex, Is.EqualTo(-1));
            Assert.That(valid, Is.False);
            Assert.That(viewModel.StatusMessage, Does.Contain("supported XBackBone API generation"));
        });
    }

    private static XBackBoneConfigViewModel CreateViewModel(
        ISecretStore secrets,
        XBackBoneConfigModel config)
    {
        XBackBoneConfigViewModel viewModel = new();
        viewModel.SetContext(new TestProviderContext(secrets));
        viewModel.LoadFromJson(JsonConvert.SerializeObject(config));
        return viewModel;
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
