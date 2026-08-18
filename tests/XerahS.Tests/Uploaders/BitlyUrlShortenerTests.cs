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

using System.Collections.Specialized;
using NUnit.Framework;
using ShareX.Bitly.Plugin;
using XerahS.Uploaders;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class BitlyUrlShortenerTests
{
    [Test]
    public void ShortenURL_WhenAccessTokenMissing_SurfacesErrorOnResult()
    {
        var shortener = new BitlyUrlShortener(new BitlyConfigModel());

        UploadResult result = shortener.ShortenURL("https://example.com/long");

        Assert.That(result.ShortenedURL, Is.Null.Or.Empty);
        Assert.That(shortener.IsError, Is.True);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));
        Assert.That(result.ErrorsToString(), Does.Contain("access token"));
    }

    [Test]
    public void ShortenURL_WhenSendRequestThrows_DoesNotRethrow_AndSurfacesError()
    {
        var shortener = new ThrowingBitlyUrlShortener(new BitlyConfigModel
        {
            AccessToken = "test-token"
        });

        UploadResult result = shortener.ShortenURL("https://example.com/long");

        Assert.That(result.ShortenedURL, Is.Null.Or.Empty);
        Assert.That(shortener.IsError, Is.True);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));
        Assert.That(result.ErrorsToString(), Does.Contain("Bitly request failed"));
        Assert.That(result.ErrorsToString(), Does.Contain("simulated network failure"));
    }

    [Test]
    public void ShortenURL_WhenSendRequestReturnsNull_SurfacesEmptyResponseError()
    {
        var shortener = new NullResponseBitlyUrlShortener(new BitlyConfigModel
        {
            AccessToken = "test-token"
        });

        UploadResult result = shortener.ShortenURL("https://example.com/long");

        Assert.That(result.ShortenedURL, Is.Null.Or.Empty);
        Assert.That(shortener.IsError, Is.True);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));
        Assert.That(result.ErrorsToString(), Does.Contain("empty response"));
    }

    [Test]
    public void ShortenURL_WhenApiReturnsLink_SetsShortenedURL()
    {
        var shortener = new SuccessBitlyUrlShortener(new BitlyConfigModel
        {
            AccessToken = "test-token"
        });

        UploadResult result = shortener.ShortenURL("https://example.com/long");

        Assert.That(result.ShortenedURL, Is.EqualTo("https://bit.ly/abc123"));
        Assert.That(shortener.IsError, Is.False);
        Assert.That(result.Errors.Count, Is.EqualTo(0));
    }

    private sealed class ThrowingBitlyUrlShortener : BitlyUrlShortener
    {
        public ThrowingBitlyUrlShortener(BitlyConfigModel config) : base(config)
        {
        }

        protected override string? SendBitlyRequest(string json, NameValueCollection headers)
        {
            throw new InvalidOperationException("simulated network failure");
        }
    }

    private sealed class NullResponseBitlyUrlShortener : BitlyUrlShortener
    {
        public NullResponseBitlyUrlShortener(BitlyConfigModel config) : base(config)
        {
        }

        protected override string? SendBitlyRequest(string json, NameValueCollection headers)
        {
            return null;
        }
    }

    private sealed class SuccessBitlyUrlShortener : BitlyUrlShortener
    {
        public SuccessBitlyUrlShortener(BitlyConfigModel config) : base(config)
        {
        }

        protected override string? SendBitlyRequest(string json, NameValueCollection headers)
        {
            return """{"created_at":"2026-01-01T00:00:00+0000","id":"bit.ly/abc123","link":"https://bit.ly/abc123","long_url":"https://example.com/long"}""";
        }
    }
}
