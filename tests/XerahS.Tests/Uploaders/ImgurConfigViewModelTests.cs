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
using ShareX.Imgur.Plugin;
using ShareX.Imgur.Plugin.ViewModels;
using System.IO;
using System.Reflection;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class ImgurConfigViewModelTests
{
    [Test]
    public void LoadFromJson_BuildsUploaderWithPersistedClientId()
    {
        const string secretKey = "imgur-secret";
        InMemorySecretStore secrets = new();
        OAuth2Token token = new()
        {
            access_token = "access-token",
            refresh_token = "refresh-token",
            expires_in = 3600
        };
        token.UpdateExpireDate();
        secrets.SetSecret("imgur", secretKey, "oauthToken", JsonConvert.SerializeObject(token));

        ImgurConfigViewModel viewModel = new();
        viewModel.SetContext(new TestProviderContext(secrets));

        viewModel.LoadFromJson(JsonConvert.SerializeObject(new ImgurConfigModel
        {
            SecretKey = secretKey,
            ClientId = "client-123",
            AccountType = AccountType.User
        }));

        ImgurUploader uploader = GetUploader(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsLoggedIn, Is.True);
            Assert.That(uploader.AuthInfo.Client_ID, Is.EqualTo("client-123"));
            Assert.That(uploader.CheckAuthorization(), Is.True);
        });
    }

    [Test]
    public void ToJson_PersistsCurrentUiSelectionsIntoRebuiltUploader()
    {
        ImgurConfigViewModel viewModel = new();
        viewModel.SetContext(new TestProviderContext(new InMemorySecretStore()));
        viewModel.LoadFromJson("{}");

        viewModel.ClientId = "client-456";
        viewModel.AccountTypeIndex = (int)AccountType.Anonymous;
        viewModel.ThumbnailTypeIndex = (int)ImgurThumbnailType.Huge_Thumbnail;
        viewModel.UseDirectLink = false;
        viewModel.UseGifv = false;
        viewModel.UploadToSelectedAlbum = true;
        viewModel.SelectedAlbum = new ImgurAlbumData { id = "album-1", title = "Album 1" };

        string json = viewModel.ToJson();
        InvokeEnsureUploader(viewModel, rebuild: true);

        ImgurUploader uploader = GetUploader(viewModel);
        ImgurConfigModel saved = JsonConvert.DeserializeObject<ImgurConfigModel>(json)!;
        ImgurConfigModel uploaderConfig = GetUploaderConfig(uploader);

        Assert.Multiple(() =>
        {
            Assert.That(saved.ClientId, Is.EqualTo("client-456"));
            Assert.That(saved.DirectLink, Is.False);
            Assert.That(saved.UseGIFV, Is.False);
            Assert.That(saved.UploadToSelectedAlbum, Is.True);
            Assert.That(saved.SelectedAlbum?.id, Is.EqualTo("album-1"));
            Assert.That(uploaderConfig.ClientId, Is.EqualTo("client-456"));
            Assert.That(uploaderConfig.DirectLink, Is.False);
            Assert.That(uploaderConfig.UseGIFV, Is.False);
            Assert.That(uploaderConfig.UploadToSelectedAlbum, Is.True);
            Assert.That(uploaderConfig.SelectedAlbum?.id, Is.EqualTo("album-1"));
        });
    }

    [Test]
    public void TryPrepareStreamForRetry_ResetsSeekableStreamToStart()
    {
        using MemoryStream stream = new(new byte[] { 1, 2, 3, 4 });
        stream.Position = 3;

        bool prepared = InvokeTryPrepareStreamForRetry(stream);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.True);
            Assert.That(stream.Position, Is.Zero);
        });
    }

    [Test]
    public void TryPrepareStreamForRetry_ReturnsFalseForNonSeekableStream()
    {
        using NonSeekableReadStream stream = new(new byte[] { 1, 2, 3, 4 });

        bool prepared = InvokeTryPrepareStreamForRetry(stream);

        Assert.That(prepared, Is.False);
    }

    private static ImgurUploader GetUploader(ImgurConfigViewModel viewModel)
    {
        FieldInfo? field = typeof(ImgurConfigViewModel).GetField("_uploader", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Assert.That(field!.GetValue(viewModel), Is.TypeOf<ImgurUploader>());
        return (ImgurUploader)field.GetValue(viewModel)!;
    }

    private static ImgurConfigModel GetUploaderConfig(ImgurUploader uploader)
    {
        FieldInfo? field = typeof(ImgurUploader).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Assert.That(field!.GetValue(uploader), Is.TypeOf<ImgurConfigModel>());
        return (ImgurConfigModel)field.GetValue(uploader)!;
    }

    private static void InvokeEnsureUploader(ImgurConfigViewModel viewModel, bool rebuild)
    {
        MethodInfo? method = typeof(ImgurConfigViewModel).GetMethod("EnsureUploader", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method!.Invoke(viewModel, new object[] { rebuild });
    }

    private static bool InvokeTryPrepareStreamForRetry(Stream stream)
    {
        MethodInfo? method = typeof(ImgurUploader).GetMethod("TryPrepareStreamForRetry", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method!.Invoke(null, new object[] { stream })!;
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

    private sealed class NonSeekableReadStream : MemoryStream
    {
        public NonSeekableReadStream(byte[] buffer) : base(buffer)
        {
        }

        public override bool CanSeek => false;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
