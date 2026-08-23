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

#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public sealed class ProgressStreamContentTests
{
    [Test]
    public async Task Serialize_ReportsProgressAndCopiesBytes()
    {
        byte[] payload = { 1, 2, 3, 4, 5, 6, 7 };
        using MemoryStream source = new MemoryStream(payload);
        List<int> chunks = new();

        using ProgressStreamContent content = new ProgressStreamContent(source, 0, payload.Length, bufferSize: 3, chunks.Add);
        byte[] actual = await content.ReadAsByteArrayAsync();

        Assert.That(actual, Is.EqualTo(payload));
        Assert.That(chunks, Is.EqualTo(new[] { 3, 3, 1 }));
    }

    [Test]
    public void Serialize_ShortStream_ThrowsEndOfStream()
    {
        using MemoryStream source = new MemoryStream(new byte[] { 1, 2 });
        using ProgressStreamContent content = new ProgressStreamContent(source, 0, contentLength: 8, bufferSize: 4, progressReporter: null);

        Assert.That(
            async () => await content.ReadAsByteArrayAsync(),
            Throws.TypeOf<HttpRequestException>().With.InnerException.TypeOf<EndOfStreamException>());
    }
}
