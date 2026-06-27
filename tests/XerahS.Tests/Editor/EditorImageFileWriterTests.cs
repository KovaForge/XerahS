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
using ShareX.ImageEditor.Presentation.Views;
using SkiaSharp;

namespace XerahS.Tests.Editor;

[TestFixture]
public sealed class EditorImageFileWriterTests
{
    [Test]
    public void SaveEncodedData_WhenOverwritingLargerFile_TruncatesDestination()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "xerahs-editor-save-" + Guid.NewGuid().ToString("N") + ".png");

        try
        {
            File.WriteAllBytes(tempFile, Enumerable.Repeat((byte)0x41, 4096).ToArray());

            using SKData data = SKData.CreateCopy(new byte[] { 1, 2, 3, 4, 5 });

            EditorImageFileWriter.SaveEncodedData(tempFile, data);

            Assert.That(File.ReadAllBytes(tempFile), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
