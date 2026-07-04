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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ShareX.Immich.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class ImmichClientTests
{
    private const string ServerUrl = "https://immich.example.invalid";
    private const string ApiKey = "00000000000000000000000000000000";

    [Test]
    public void CreateSharedLinkAsync_IndividualMode_NullAssetIds_ThrowsInvalidOperationException()
    {
        // Arrange: client with no real HTTP expectations because validation
        // happens before any HTTP call.
        var client = new ImmichClient(ServerUrl, ApiKey);

        // Act + Assert: passing a null assetIds collection for INDIVIDUAL
        // shared links must throw before the request is sent. Previously the
        // payload was serialised with `assetIds = null`, surfacing as a
        // generic 400 from the Immich server wrapped in a confusing
        // `InvalidOperationException("Immich did not return a shared link.")`.
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.CreateSharedLinkAsync(
                shareMode: ImmichShareMode.Asset,
                assetIds: null,
                albumId: null,
                slug: null,
                password: null,
                useExpiry: false,
                expireAfterDays: 0,
                allowDownload: true,
                allowUpload: false,
                showMetadata: true,
                cancellation: CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("individual shared link"));
        Assert.That(ex.Message, Does.Contain("asset"));
    }

    [Test]
    public void CreateSharedLinkAsync_IndividualMode_EmptyAssetIds_ThrowsInvalidOperationException()
    {
        // Arrange: empty collection must fail validation too.
        var client = new ImmichClient(ServerUrl, ApiKey);

        // Act + Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.CreateSharedLinkAsync(
                shareMode: ImmichShareMode.Asset,
                assetIds: Array.Empty<string>(),
                albumId: null,
                slug: null,
                password: null,
                useExpiry: false,
                expireAfterDays: 0,
                allowDownload: true,
                allowUpload: false,
                showMetadata: true,
                cancellation: CancellationToken.None));
    }

    [Test]
    public void CreateSharedLinkAsync_AlbumMode_NullAssetIds_DoesNotValidateAssetIds()
    {
        // Arrange: ALBUM-mode shared links don't require assetIds; passing null
        // should fail later (during HTTP) but must NOT fail the new
        // assetIds-required guard, so we don't get a misleading error here.
        var client = new ImmichClient(ServerUrl, ApiKey);

        // Act + Assert: any thrown exception must not be the "individual
        // shared link requires at least one asset ID" guard message.
        try
        {
            client.CreateSharedLinkAsync(
                shareMode: ImmichShareMode.Album,
                assetIds: null,
                albumId: null,
                slug: null,
                password: null,
                useExpiry: false,
                expireAfterDays: 0,
                allowDownload: true,
                allowUpload: false,
                showMetadata: true,
                cancellation: CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            Assert.That(ex.Message, Does.Not.Contain("individual shared link"));
        }
        catch (Exception)
        {
            // Any non-InvalidOperationException (e.g. HTTP / DNS) is acceptable
            // here — the assertion only protects against the new guard firing.
        }
    }
}