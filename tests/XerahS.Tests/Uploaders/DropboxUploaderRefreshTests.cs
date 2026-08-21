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
using ShareX.Dropbox.Plugin;
using XerahS.Uploaders;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class DropboxUploaderRefreshTests
{
    [Test]
    public void NeedsRefresh_WhenExpireDateUnknown_AndOnlyBareRefreshToken_ReturnsFalse()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 0,
            ExpireDate = DateTime.MinValue
        };

        Assert.That(DropboxUploader.NeedsRefresh(token), Is.False);
    }

    [Test]
    public void NeedsRefresh_WhenExpireDateUnknown_AndFiniteLifetimeWithRefreshToken_ReturnsTrue()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 14400,
            ExpireDate = DateTime.MinValue
        };

        Assert.That(DropboxUploader.NeedsRefresh(token), Is.True);
    }

    [Test]
    public void NeedsRefresh_WhenExpireDateUnknown_AndFiniteLifetimeWithoutRefreshToken_ReturnsFalse()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "",
            expires_in = 14400,
            ExpireDate = DateTime.MinValue
        };

        Assert.That(DropboxUploader.NeedsRefresh(token), Is.False);
    }

    [Test]
    public void NeedsRefresh_WhenExpireDateInPast_ReturnsTrue()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 14400,
            ExpireDate = DateTime.UtcNow.AddMinutes(-5)
        };

        Assert.That(DropboxUploader.NeedsRefresh(token), Is.True);
    }

    [Test]
    public void NeedsRefresh_WhenExpireDateInFuture_ReturnsFalse()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 14400,
            ExpireDate = DateTime.UtcNow.AddHours(1)
        };

        Assert.That(DropboxUploader.NeedsRefresh(token), Is.False);
    }

    [Test]
    public void CanUseAccessTokenWithoutRefresh_WhenExpireDateUnknown_ReturnsTrue()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 0,
            ExpireDate = DateTime.MinValue
        };

        Assert.That(DropboxUploader.CanUseAccessTokenWithoutRefresh(token), Is.True);
    }

    [Test]
    public void CanUseAccessTokenWithoutRefresh_WhenExpired_ReturnsFalse()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 14400,
            ExpireDate = DateTime.UtcNow.AddMinutes(-1)
        };

        Assert.That(DropboxUploader.CanUseAccessTokenWithoutRefresh(token), Is.False);
    }

    [Test]
    public void CanUseAccessTokenWithoutRefresh_WhenStillValid_ReturnsTrue()
    {
        OAuth2Token token = new()
        {
            access_token = "access",
            refresh_token = "refresh",
            expires_in = 14400,
            ExpireDate = DateTime.UtcNow.AddHours(2)
        };

        Assert.That(DropboxUploader.CanUseAccessTokenWithoutRefresh(token), Is.True);
    }

    [Test]
    public void CanUseAccessTokenWithoutRefresh_WhenAccessTokenMissing_ReturnsFalse()
    {
        OAuth2Token token = new()
        {
            access_token = "",
            refresh_token = "refresh",
            expires_in = 0,
            ExpireDate = DateTime.MinValue
        };

        Assert.That(DropboxUploader.CanUseAccessTokenWithoutRefresh(token), Is.False);
    }

    [Test]
    public void CheckAuthorization_WhenBareRefreshTokenAndUnknownExpiry_DoesNotForceRefreshFailure()
    {
        // Bare refresh_token + ExpireDate=MinValue + expires_in=0 must not force a
        // network refresh. CheckAuthorization should accept the existing access token.
        OAuth2Info auth = new("client-id", "client-secret")
        {
            Token = new OAuth2Token
            {
                access_token = "still-valid-access",
                refresh_token = "refresh-only",
                expires_in = 0,
                ExpireDate = DateTime.MinValue
            }
        };

        DropboxUploader uploader = new(new DropboxConfigModel(), auth);

        bool authorized = uploader.CheckAuthorization();

        Assert.That(authorized, Is.True);
        Assert.That(uploader.IsError, Is.False);
        Assert.That(uploader.Errors.Count, Is.EqualTo(0));
    }

    [Test]
    public void CheckAuthorization_WhenKnownExpiredAndNoRefreshToken_FailsWithDiagnostic()
    {
        OAuth2Info auth = new("client-id", "client-secret")
        {
            Token = new OAuth2Token
            {
                access_token = "expired-access",
                refresh_token = "",
                expires_in = 14400,
                ExpireDate = DateTime.UtcNow.AddMinutes(-10)
            }
        };

        DropboxUploader uploader = new(new DropboxConfigModel(), auth);

        bool authorized = uploader.CheckAuthorization();

        Assert.That(authorized, Is.False);
        Assert.That(uploader.IsError, Is.True);
        Assert.That(uploader.Errors.ToString(), Does.Contain("refresh failed"));
    }
}
