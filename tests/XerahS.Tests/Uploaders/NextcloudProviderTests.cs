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
using System.Reflection;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class NextcloudProviderTests
{
    [Test]
    public void ValidateSettings_AcceptsLegacyConfigWhenUserIdIsPresent()
    {
        const string secretKey = "nextcloud-secret";
        NextcloudProvider provider = CreateProvider(secretKey, "app-password");
        string settingsJson = JsonConvert.SerializeObject(new NextcloudConfigModel
        {
            ServerUrl = "https://cloud.example.com",
            LoginName = string.Empty,
            UserId = "alice",
            SecretKey = secretKey
        });

        bool isValid = provider.ValidateSettings(settingsJson);

        Assert.That(isValid, Is.True);
    }

    [Test]
    public void ValidateSettings_RejectsConfigWithoutLoginIdentity()
    {
        const string secretKey = "nextcloud-secret";
        NextcloudProvider provider = CreateProvider(secretKey, "app-password");
        string settingsJson = JsonConvert.SerializeObject(new NextcloudConfigModel
        {
            ServerUrl = "https://cloud.example.com",
            LoginName = string.Empty,
            UserId = string.Empty,
            SecretKey = secretKey
        });

        bool isValid = provider.ValidateSettings(settingsJson);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void ExtractRelativePath_StripsServerBasePathFromAbsoluteHref()
    {
        string relativePath = InvokeExtractRelativePath(
            "https://cloud.example.com/nextcloud/remote.php/dav/files/alice/ShareX/2026/cat.png",
            "/remote.php/dav/files/alice/",
            "alice");

        Assert.That(relativePath, Is.EqualTo("ShareX/2026/cat.png"));
    }

    [Test]
    public void ExtractRelativePath_HandlesEncodedUserIdWithoutLosingRootFolder()
    {
        string relativePath = InvokeExtractRelativePath(
            "/nextcloud/remote.php/dav/files/john%2Fdoe/Screenshots",
            "/remote.php/dav/files/john/doe/",
            "john/doe");

        Assert.That(relativePath, Is.EqualTo("Screenshots"));
    }

    private static NextcloudProvider CreateProvider(string secretKey, string appPassword)
    {
        NextcloudProvider provider = new();
        InMemorySecretStore secrets = new();
        secrets.SetSecret("nextcloud", secretKey, "appPassword", appPassword);
        provider.SetContext(new TestProviderContext(secrets));
        return provider;
    }

    private static string InvokeExtractRelativePath(string href, string hrefPrefix, string userId)
    {
        MethodInfo? method = typeof(NextcloudClient).GetMethod("ExtractRelativePath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        object? result = method!.Invoke(null, new object[] { href, hrefPrefix, userId });
        Assert.That(result, Is.TypeOf<string>());
        return (string)result!;
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
