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
using XerahS.Core;
using XerahS.Core.Tasks;

namespace XerahS.Tests.Tasks;

[TestFixture]
public sealed class WorkerTaskRecordingHistoryTests
{
    [Test]
    public void CreateRecordingHistoryItem_PreservesRecordingMetadataTags()
    {
        var info = new TaskInfo
        {
            Metadata = new TaskMetadata
            {
                UploadURL = "https://example.com/capture.mp4",
                WindowTitle = "Quarterly review",
                ProcessName = "obsidian",
                OcrText = "agenda item"
            }
        };

        var historyItem = WorkerTask.CreateRecordingHistoryItem(info, "/tmp/capture.mp4");

        Assert.Multiple(() =>
        {
            Assert.That(historyItem.FilePath, Is.EqualTo("/tmp/capture.mp4"));
            Assert.That(historyItem.FileName, Is.EqualTo("capture.mp4"));
            Assert.That(historyItem.Type, Is.EqualTo("Video"));
            Assert.That(historyItem.URL, Is.EqualTo("https://example.com/capture.mp4"));
            Assert.That(historyItem.Tags, Contains.Key("WindowTitle").WithValue("Quarterly review"));
            Assert.That(historyItem.Tags, Contains.Key("ProcessName").WithValue("obsidian"));
            Assert.That(historyItem.Tags, Contains.Key(nameof(TaskMetadata.OcrText)).WithValue("agenda item"));
        });
    }

    [Test]
    public void CreateRecordingHistoryItem_SkipsEmptyTags()
    {
        var info = new TaskInfo
        {
            Metadata = new TaskMetadata()
        };

        var historyItem = WorkerTask.CreateRecordingHistoryItem(info, "/tmp/capture.mp4");

        Assert.That(historyItem.Tags, Is.Empty);
    }
}
