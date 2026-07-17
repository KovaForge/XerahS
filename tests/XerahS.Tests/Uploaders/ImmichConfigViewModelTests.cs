#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty
    of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using ShareX.Immich.Plugin.ViewModels;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class ImmichConfigViewModelTests
{
    [Test]
    public void SetAlbumOptionsPlaceholder_withNameOnly_doesNotOverwriteNameWithIdFromOnSelectedAlbumChanged()
    {
        // Arrange: config saved with AlbumName but no AlbumId (album was deleted server-side
        // but the name is still meaningful for re-creating or re-selecting). Previously,
        // SetAlbumOptionsPlaceholder would set SelectedAlbum to the placeholder option,
        // triggering OnSelectedAlbumChanged which reset AlbumName to the placeholder Name
        // value ("", not the original saved name), causing the album name to be lost on the
        // next config round-trip even though it was never invalid.
        var viewModel = new ImmichConfigViewModel();
        const string expectedAlbumName = "ShareX Uploads"; // default in config

        // Act: simulate loading config that has only AlbumName, no AlbumId
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "AddToAlbum": true,
                "AlbumId": "",
                "AlbumName": "ShareX Uploads"
            }
            """);

        // Assert: AlbumName is preserved, not overwritten by the placeholder option's name
        Assert.That(viewModel.AlbumName, Is.EqualTo(expectedAlbumName));
    }

    [Test]
    public void SetAlbumOptionsPlaceholder_withIdAndName_restoresBoth()
    {
        // Arrange: config has both AlbumId and AlbumName (valid saved album)
        var viewModel = new ImmichConfigViewModel();

        // Act
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "AddToAlbum": true,
                "AlbumId": "abc123",
                "AlbumName": "My Trip Photos"
            }
            """);

        // Assert: both ID and name are preserved
        Assert.That(viewModel.AlbumName, Is.EqualTo("My Trip Photos"));
        Assert.That(viewModel.SelectedAlbum, Is.Not.Null);
        Assert.That(viewModel.SelectedAlbum!.Id, Is.EqualTo("abc123"));
        Assert.That(viewModel.SelectedAlbum.Name, Is.EqualTo("My Trip Photos"));
    }

    [Test]
    public void SetAlbumOptionsPlaceholder_withEmptyIds_clearsSelection()
    {
        // Arrange: config has no album selection at all
        var viewModel = new ImmichConfigViewModel();

        // Act
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "AddToAlbum": false,
                "AlbumId": "",
                "AlbumName": ""
            }
            """);

        // Assert: selection is cleared
        Assert.That(viewModel.SelectedAlbum, Is.Null);
        Assert.That(viewModel.AlbumName, Is.Empty);
    }

    [Test]
    public void ToJson_manualAlbumNameEdit_clearsStaleSelectedAlbumAndPersistsTypedName()
    {
        // Regression: selecting album A then typing a different free-form AlbumName used to
        // keep SelectedAlbum set. ToJson/Validate called SyncSelectedAlbumIntoFields which
        // restored album A's name and persisted album A's ID, so auto-create never saw the
        // newly typed name.
        var viewModel = new ImmichConfigViewModel();
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "AddToAlbum": true,
                "AutoCreateAlbum": true,
                "AlbumId": "album-a",
                "AlbumName": "Album A"
            }
            """);

        Assert.That(viewModel.SelectedAlbum, Is.Not.Null);
        Assert.That(viewModel.SelectedAlbum!.Id, Is.EqualTo("album-a"));

        // Act: user edits the free-form name toward a new auto-create target.
        viewModel.AlbumName = "Brand New Album";

        Assert.That(viewModel.SelectedAlbum, Is.Null, "manual name edit must clear stale picker selection");

        string json = viewModel.ToJson();
        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<ShareX.Immich.Plugin.ImmichConfigModel>(json);

        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.AlbumId, Is.Empty);
        Assert.That(roundTripped.AlbumName, Is.EqualTo("Brand New Album"));
    }

    [Test]
    public void ToJson_selectingAlbum_stillCopiesAlbumNameFromSelection()
    {
        // Guard: picker selection must still populate AlbumName (OnSelectedAlbumChanged).
        var viewModel = new ImmichConfigViewModel();
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "AddToAlbum": true,
                "AlbumId": "",
                "AlbumName": ""
            }
            """);

        viewModel.SelectedAlbum = new ShareX.Immich.Plugin.ViewModels.ImmichAlbumOption("abc123", "My Trip Photos", 12);

        Assert.That(viewModel.AlbumName, Is.EqualTo("My Trip Photos"));
        Assert.That(viewModel.SelectedAlbum, Is.Not.Null);

        string json = viewModel.ToJson();
        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<ShareX.Immich.Plugin.ImmichConfigModel>(json);

        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.AlbumId, Is.EqualTo("abc123"));
        Assert.That(roundTripped.AlbumName, Is.EqualTo("My Trip Photos"));
    }

    [Test]
    public void ToJson_clampsNonPositiveExpireAfterDaysToSeven()
    {
        // Arrange: load a config with ExpireAfterDays = 0 (invalid; Validate would reject).
        // LoadFromJson defensively clamps invalid values to 7 (line 402 in
        // ImmichConfigViewModel.cs), and ToJson must mirror that clamp because
        // ToJson runs on every PropertyChanged event in
        // UploaderInstanceViewModel.cs#276 BEFORE Validate is consulted. Without the
        // mirror clamp, an invalid value entered via the UI is persisted to JSON before
        // Validate ever runs, leading to an invalid round-trip.
        var viewModel = new ImmichConfigViewModel();
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "ShareMode": 2,
                "UseShareExpiry": true,
                "ExpireAfterDays": 0
            }
            """);

        // Act: serialize. ToJson mirrors the load clamp and rewrites 0 -> 7.
        string json = viewModel.ToJson();
        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<ShareX.Immich.Plugin.ImmichConfigModel>(json);

        // Assert: ExpireAfterDays was clamped to 7 on save (defensive symmetry with load).
        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.ExpireAfterDays, Is.EqualTo(7));
    }

    [Test]
    public void ToJson_clampsNegativeExpireAfterDaysToSeven()
    {
        // Same idea: negative values from the UI must also be clamped on save.
        var viewModel = new ImmichConfigViewModel();
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "ShareMode": 2,
                "UseShareExpiry": true,
                "ExpireAfterDays": -3
            }
            """);

        string json = viewModel.ToJson();
        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<ShareX.Immich.Plugin.ImmichConfigModel>(json);

        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.ExpireAfterDays, Is.EqualTo(7));
    }

    [Test]
    public void ToJson_preservesValidExpireAfterDays()
    {
        // Sanity guard: a valid value (e.g. 30) must round-trip unchanged.
        var viewModel = new ImmichConfigViewModel();
        viewModel.LoadFromJson(/*lang=json*/ """
            {
                "ServerUrl": "https://immich.example.com",
                "SecretKey": "00000000000000000000000000000000",
                "ShareMode": 2,
                "UseShareExpiry": true,
                "ExpireAfterDays": 30
            }
            """);

        string json = viewModel.ToJson();
        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<ShareX.Immich.Plugin.ImmichConfigModel>(json);

        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.ExpireAfterDays, Is.EqualTo(30));
    }
}
