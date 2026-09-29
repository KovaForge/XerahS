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

using NUnit.Framework;
using ShareX.AmazonS3.Plugin;
using ShareX.Dropbox.Plugin;
using ShareX.GitHubGist.Plugin;
using ShareX.Imgur.Plugin;
using ShareX.Immich.Plugin;
using ShareX.Nextcloud.Plugin;
using ShareX.XBackBone.Plugin;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class InstanceSecretBackupProviderTests
{
    private const string SecretKey = "backup-secret-key";

    [Test]
    public void AmazonS3AccessKeys_EnumeratesPrimaryAndDestinationAliasReferences()
    {
        IInstanceSecretBackupProvider provider = new AmazonS3Provider();

        IReadOnlyList<InstanceSecretReference> references = provider.GetSecretReferences(
            $$"""{"SecretKey":"{{SecretKey}}","AuthMode":0,"BucketName":"backup-bucket","Region":"us-east-1","Endpoint":"s3.amazonaws.com"}""");

        Assert.Multiple(() =>
        {
            Assert.That(references, Has.Count.EqualTo(4));
            Assert.That(references, Does.Contain(new InstanceSecretReference("amazons3", SecretKey, "accessKeyId")));
            Assert.That(references, Does.Contain(new InstanceSecretReference("amazons3", SecretKey, "secretAccessKey")));
            Assert.That(references.Count(item => item.SecretKey.StartsWith("destination:", StringComparison.Ordinal)), Is.EqualTo(2));
        });
    }

    [Test]
    public void AmazonS3Sso_EnumeratesAllSsoReferences()
    {
        IInstanceSecretBackupProvider provider = new AmazonS3Provider();

        IReadOnlyList<InstanceSecretReference> references = provider.GetSecretReferences(
            $$"""{"SecretKey":"{{SecretKey}}","AuthMode":1}""");

        Assert.That(references, Is.EquivalentTo(new[]
        {
            new InstanceSecretReference("amazons3", SecretKey, "ssoClient"),
            new InstanceSecretReference("amazons3", SecretKey, "ssoToken"),
            new InstanceSecretReference("amazons3", SecretKey, "ssoRoleCredentials")
        }));
    }

    [TestCaseSource(nameof(StandardProviderCases))]
    public void StandardProvider_EnumeratesExpectedReferences(
        IInstanceSecretBackupProvider provider,
        InstanceSecretReference[] expected)
    {
        IReadOnlyList<InstanceSecretReference> references = provider.GetSecretReferences(
            $$"""{"SecretKey":"{{SecretKey}}"}""");

        Assert.That(references, Is.EquivalentTo(expected));
    }

    [TestCaseSource(nameof(AllProviders))]
    public void InvalidOrMissingSecretKey_ReturnsEmpty(IInstanceSecretBackupProvider provider)
    {
        Assert.Multiple(() =>
        {
            Assert.That(provider.GetSecretReferences("not-json"), Is.Empty);
            Assert.That(provider.GetSecretReferences("{}"), Is.Empty);
            Assert.That(provider.GetSecretReferences(string.Empty), Is.Empty);
        });
    }

    private static IEnumerable<TestCaseData> StandardProviderCases()
    {
        yield return Case(new DropboxProvider(), "dropbox", "clientId", "clientSecret", "oauthToken");
        yield return new TestCaseData(
            new GitHubGistProvider(),
            new[]
            {
                Ref("gist", "clientId"),
                Ref("gist", "clientSecret"),
                Ref("gist", "oauthToken"),
                Ref("github", "clientId"),
                Ref("github", "clientSecret"),
                Ref("github", "oauthToken")
            });
        yield return Case(new ImgurProvider(), "imgur", "clientSecret", "oauthToken");
        yield return Case(new NextcloudProvider(), "nextcloud", "appPassword", "sharePassword");
        yield return Case(new ImmichProvider(), "immich", "apiKey", "apiToken", "sharePassword");
        yield return Case(new XBackBoneProvider(), "xbackbone", "apiToken");
    }

    private static IEnumerable<IInstanceSecretBackupProvider> AllProviders()
    {
        yield return new AmazonS3Provider();
        yield return new DropboxProvider();
        yield return new GitHubGistProvider();
        yield return new ImgurProvider();
        yield return new NextcloudProvider();
        yield return new ImmichProvider();
        yield return new XBackBoneProvider();
    }

    private static TestCaseData Case(
        IInstanceSecretBackupProvider provider,
        string providerId,
        params string[] names)
    {
        return new TestCaseData(
            provider,
            names.Select(name => new InstanceSecretReference(providerId, SecretKey, name)).ToArray());
    }

    private static InstanceSecretReference Ref(string providerId, string name)
    {
        return new InstanceSecretReference(providerId, SecretKey, name);
    }
}
