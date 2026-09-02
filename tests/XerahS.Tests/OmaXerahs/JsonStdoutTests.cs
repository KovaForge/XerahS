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

using System.Text.Json;
using NUnit.Framework;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
public class JsonStdoutTests
{
    [Test]
    public void Serialize_FailureResponse_IsExactlyOneJsonObject()
    {
        string json = JsonStdout.Serialize(CliFailureResponse.Create(CliErrorCodes.UnsupportedType, "File is not a supported image type."));

        Assert.That(JsonStdout.IsSingleJsonObject(json, out string? error), Is.True, error);
        using var document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(document.RootElement.GetProperty("ok").GetBoolean(), Is.False);
        Assert.That(document.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(document.RootElement.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("unsupported_type"));
        Assert.That(json.Contains('\n'), Is.False);
    }

    [Test]
    public void IsSingleJsonObject_RejectsTrailingTokens()
    {
        bool ok = JsonStdout.IsSingleJsonObject("{\"ok\":true}{\"ok\":false}", out string? error);

        Assert.That(ok, Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void IsSingleJsonObject_RejectsNonObjectRoot()
    {
        bool ok = JsonStdout.IsSingleJsonObject("[1,2]", out string? error);

        Assert.That(ok, Is.False);
        Assert.That(error, Does.Contain("object"));
    }
}
