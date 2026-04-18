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
using System.Collections.Specialized;
using System.Net;
using XerahS.Uploaders;
using UploadHttpMethod = XerahS.Uploaders.HttpMethod;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class RequestHelpersTests
{
    [Test]
    public void CreateWebRequest_ParsesCookieHeaderWithoutWhitespaceSeparators()
    {
        NameValueCollection headers = new()
        {
            ["Cookie"] = "session=abc123;theme=dark"
        };

        HttpWebRequest request = RequestHelpers.CreateWebRequest(UploadHttpMethod.GET, "https://example.com/upload", headers);

        Assert.That(request.CookieContainer, Is.Not.Null);
        CookieCollection cookies = request.CookieContainer!.GetCookies(new Uri("https://example.com/upload"));

        Assert.That(cookies["session"]?.Value, Is.EqualTo("abc123"));
        Assert.That(cookies["theme"]?.Value, Is.EqualTo("dark"));
    }

    [Test]
    public void CreateWebRequest_PreservesCookieValuesContainingEquals()
    {
        NameValueCollection headers = new()
        {
            ["Cookie"] = "token=abc=def=="
        };

        HttpWebRequest request = RequestHelpers.CreateWebRequest(UploadHttpMethod.GET, "https://example.com/upload", headers);

        Assert.That(request.CookieContainer, Is.Not.Null);
        CookieCollection cookies = request.CookieContainer!.GetCookies(new Uri("https://example.com/upload"));

        Assert.That(cookies["token"]?.Value, Is.EqualTo("abc=def=="));
    }
}
