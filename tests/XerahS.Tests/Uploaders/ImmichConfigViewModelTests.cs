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
}
