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

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public sealed class WebHelpersCancellationTests
{
    [Test]
    public void DownloadStringAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Regression: FFmpeg/GitHub URL discovery must honour CancellationToken on
        // WebHelpers.DownloadStringAsync so long downloads can abort during lookup.
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        // HttpClient surfaces TaskCanceledException (derived from OperationCanceledException)
        // when the token is already cancelled — accept either cancel-derived type.
        Assert.That(
            async () => await WebHelpers.DownloadStringAsync("https://example.invalid/xerahs-cancel-test", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task DownloadStringAsync_NoTokenOverload_StillCompilesAndAcceptsUrl()
    {
        // Back-compat overload must remain; pre-cancelled path is covered above.
        // This only asserts the parameterless overload exists and does not throw
        // ArgumentNullException on a non-null URL (network failure is fine).
        try
        {
            await WebHelpers.DownloadStringAsync("https://example.invalid/xerahs-compat-test");
        }
        catch (Exception ex)
        {
            Assert.That(ex, Is.Not.TypeOf<ArgumentNullException>());
            Assert.That(ex, Is.Not.TypeOf<MissingMethodException>());
        }
    }
}
