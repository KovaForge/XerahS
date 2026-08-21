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
using XerahS.History;

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class HistoryOcrIndexStoreTests
{
    [Test]
    public void UpsertText_CachesAndNormalizesRecognizedText()
    {
        using var workspace = new TemporaryHistoryWorkspace();
        var store = new HistoryOcrIndexStore(workspace.DatabasePath);

        store.UpsertText(42, workspace.FilePath("capture.png"), null, "  Invoice   4812  \r\n\r\n Total\tDue  ", "test", "en");

        string? text = store.GetText(42);

        Assert.That(text, Is.EqualTo($"Invoice 4812{Environment.NewLine}Total Due"));
        Assert.That(store.CountIndexed(), Is.EqualTo(1));
    }

    [Test]
    public void Search_FindsIndexedScreenshotTextCaseInsensitively()
    {
        using var workspace = new TemporaryHistoryWorkspace();
        var store = new HistoryOcrIndexStore(workspace.DatabasePath);

        store.UpsertText(1, workspace.FilePath("first.png"), null, "Receipt Alpha", "test", "en");
        store.UpsertText(2, workspace.FilePath("second.png"), null, "Invoice Beta", "test", "en");

        List<HistoryOcrSearchMatch> matches = store.Search("invoice");

        Assert.That(matches.Select(match => match.HistoryItemId), Is.EquivalentTo(new[] { 2L }));
    }

    [Test]
    public void GetTexts_ReturnsOnlyIndexedRows()
    {
        using var workspace = new TemporaryHistoryWorkspace();
        var store = new HistoryOcrIndexStore(workspace.DatabasePath);

        store.UpsertText(1, workspace.FilePath("first.png"), null, "visible text", "test", "en");
        store.MarkStatus(2, workspace.FilePath("second.png"), "ocr_failed");

        Dictionary<long, string> texts = store.GetTexts([1, 2, 3]);

        Assert.That(texts, Has.Count.EqualTo(1));
        Assert.That(texts[1], Is.EqualTo("visible text"));
    }

    private sealed class TemporaryHistoryWorkspace : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"xerahs-ocr-index-tests-{Guid.NewGuid():N}");

        public TemporaryHistoryWorkspace()
        {
            Directory.CreateDirectory(_directory);
            DatabasePath = Path.Combine(_directory, "history.db");
        }

        public string DatabasePath { get; }

        public string FilePath(string fileName)
        {
            return Path.Combine(_directory, fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
