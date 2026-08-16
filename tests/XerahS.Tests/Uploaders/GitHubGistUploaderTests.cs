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
using ShareX.GitHubGist.Plugin;
using XerahS.Uploaders;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class GitHubGistUploaderTests
{
    [TestCase(null, true, "https://api.github.com")]
    [TestCase("", true, "https://api.github.com")]
    [TestCase("   ", true, "https://api.github.com")]
    [TestCase("https://gist.example.com/api/", true, "https://gist.example.com/api")]
    [TestCase("http://localhost:8080", true, "http://localhost:8080")]
    [TestCase("ftp://gist.example.com", false, "")]
    [TestCase("file:///etc/passwd", false, "")]
    [TestCase("not a url", false, "")]
    [TestCase("javascript:alert(1)", false, "")]
    public void TryResolveApiBase_AcceptsOnlyAbsoluteHttpOrHttpsUrls(
        string? customUrlApi,
        bool expectedOk,
        string expectedApiBase)
    {
        bool ok = GitHubGistUploader.TryResolveApiBase(customUrlApi, out string apiBase);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.EqualTo(expectedOk));
            Assert.That(apiBase, Is.EqualTo(expectedApiBase));
        });
    }

    [Test]
    public void UploadText_RejectsInvalidCustomApiUrlWithoutPosting()
    {
        GitHubGistConfigModel config = new()
        {
            CustomURLAPI = "ftp://gist.example.com"
        };
        OAuth2Info auth = new("client-id", "client-secret")
        {
            Token = new OAuth2Token { access_token = "token" }
        };
        GitHubGistUploader uploader = new(config, auth);

        UploadResult result = uploader.UploadText("hello", "note.txt");

        Assert.Multiple(() =>
        {
            Assert.That(result.URL, Is.Null.Or.Empty);
            Assert.That(uploader.Errors.Count, Is.GreaterThan(0));
            Assert.That(uploader.ToErrorString(), Does.Contain("Custom Gist API URL"));
        });
    }
}
