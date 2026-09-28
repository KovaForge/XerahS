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

using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public sealed class HandoffFileGateTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "xerahs-handoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task MissingFile_ReportsMissing()
    {
        string path = Path.Combine(_directory, "gone.png");
        HandoffFileCheck check = await HandoffFileGate.WaitForReadyAsync(path, TimeSpan.FromMilliseconds(250));
        Assert.That(check.State, Is.EqualTo(HandoffFileState.Missing));
        Assert.That(check.Describe(), Does.Contain("File not found"));
    }

    [Test]
    public async Task EmptyFile_ReportsEmpty()
    {
        string path = Path.Combine(_directory, "empty.png");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        HandoffFileCheck check = await HandoffFileGate.WaitForReadyAsync(path, TimeSpan.FromMilliseconds(250));
        Assert.That(check.State, Is.EqualTo(HandoffFileState.Empty));
    }

    [Test]
    public async Task StableFile_IsReady()
    {
        string path = Path.Combine(_directory, "shot.png");
        await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 });
        HandoffFileCheck check = await HandoffFileGate.WaitForReadyAsync(path);
        Assert.That(check.IsReady, Is.True);
        Assert.That(check.Length, Is.EqualTo(3));
    }

    [Test]
    public async Task FileAppearingLate_IsReady()
    {
        string path = Path.Combine(_directory, "late.png");
        Task<HandoffFileCheck> wait = HandoffFileGate.WaitForReadyAsync(path, TimeSpan.FromSeconds(3));
        await Task.Delay(200);
        await File.WriteAllBytesAsync(path, new byte[] { 9 });
        HandoffFileCheck check = await wait;
        Assert.That(check.IsReady, Is.True);
    }
}
