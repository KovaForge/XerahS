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
using NUnit.Framework;
using ShareX.Nextcloud.Plugin;
using ShareX.Nextcloud.Plugin.ViewModels;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class NextcloudConfigViewModelTests
{
    [Test]
    public void LoadFromJson_PromotesLegacyUserIdIntoLoginIdentity()
    {
        const string secretKey = "nextcloud-secret";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("nextcloud", secretKey, "appPassword", "app-password");

        NextcloudConfigViewModel viewModel = new();
        viewModel.SetContext(new TestProviderContext(secrets));
        viewModel.LoadFromJson(JsonConvert.SerializeObject(new NextcloudConfigModel
        {
            SecretKey = secretKey,
            ServerUrl = "https://cloud.example.com",
            LoginName = string.Empty,
            UserId = "alice"
        }));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.LoginName, Is.EqualTo("alice"));
            Assert.That(viewModel.UserId, Is.EqualTo("alice"));
            Assert.That(viewModel.IsConnected, Is.True);
            Assert.That(viewModel.Validate(), Is.True);
        });
    }

    [Test]
    public void LoadFromJson_ClearsStaleSecretsWhenNewConfigHasNoStoredCredentials()
    {
        const string firstSecretKey = "nextcloud-secret-a";
        const string secondSecretKey = "nextcloud-secret-b";
        InMemorySecretStore secrets = new();
        secrets.SetSecret("nextcloud", firstSecretKey, "appPassword", "app-password");
        secrets.SetSecret("nextcloud", firstSecretKey, "sharePassword", "share-password");

        NextcloudConfigViewModel viewModel = new();
        viewModel.SetContext(new TestProviderContext(secrets));
        viewModel.LoadFromJson(JsonConvert.SerializeObject(new NextcloudConfigModel
        {
            SecretKey = firstSecretKey,
            ServerUrl = "https://cloud.example.com",
            LoginName = "alice",
            UserId = "alice"
        }));

        viewModel.LoadFromJson(JsonConvert.SerializeObject(new NextcloudConfigModel
        {
            SecretKey = secondSecretKey,
            ServerUrl = "https://cloud.example.com",
            LoginName = "bob",
            UserId = "bob"
        }));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AppPassword, Is.Empty);
            Assert.That(viewModel.SharePassword, Is.Empty);
            Assert.That(viewModel.IsConnected, Is.False);
            Assert.That(viewModel.Validate(), Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo("Nextcloud app password is required. Use browser login or create an app password in Nextcloud security settings."));
        });
    }

    [Test]
    public void ClearStoredCredentials_ResetsConnectionStateAndCapabilitySummary()
    {
        NextcloudConfigViewModel viewModel = new();
        viewModel.SetContext(new TestProviderContext(new InMemorySecretStore()));
        viewModel.LoadFromJson(JsonConvert.SerializeObject(new NextcloudConfigModel
        {
            SecretKey = "nextcloud-secret",
            ServerUrl = "https://cloud.example.com",
            LoginName = "alice",
            UserId = "alice",
            DisplayName = "Alice",
            ServerVersion = "30.0.0",
            ServerProductName = "OwnCloud But Not Really",
            SupportsPublicShares = false,
            SupportsSharePasswords = true,
            SupportsExpireDate = true,
            SupportsChunking = true,
            SupportsSearch = true
        }));

        viewModel.ClearStoredCredentialsCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsConnected, Is.False);
            Assert.That(viewModel.ConnectionSummary, Is.EqualTo("No Nextcloud account connected."));
            Assert.That(viewModel.ServerVersion, Is.Empty);
            Assert.That(viewModel.ServerProductName, Is.EqualTo("Nextcloud"));
            Assert.That(viewModel.CapabilitiesSummary, Is.EqualTo("Capabilities will appear after profile refresh."));
            Assert.That(viewModel.SupportsSharePasswords, Is.False);
            Assert.That(viewModel.SupportsExpireDate, Is.False);
            Assert.That(viewModel.SupportsChunking, Is.False);
            Assert.That(viewModel.SupportsSearch, Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo("Stored Nextcloud credentials were cleared."));
        });
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
