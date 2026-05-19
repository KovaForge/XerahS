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

using System.Reflection;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using NUnit.Framework;
using XerahS.UI.Views;

namespace XerahS.Tests.Views;

[TestFixture]
public sealed class UploadContentWindowDragDropTests
{
    [Test]
    public void GetDroppedStorageItems_UsesDataTransferFileCollection()
    {
        using var tempDirectory = new TempDirectory();
        string filePath = System.IO.Path.Combine(tempDirectory.Path, "dropped-file.txt");
        string folderPath = System.IO.Path.Combine(tempDirectory.Path, "dropped-folder");
        File.WriteAllText(filePath, "drag payload");
        Directory.CreateDirectory(folderPath);

        IStorageFile file = CreateBclStorageFile(filePath);
        IStorageFolder folder = CreateBclStorageFolder(folderPath);
        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateFile(file));
        dataTransfer.Add(DataTransferItem.CreateFile(folder));

        var items = UploadContentWindow.GetDroppedStorageItems(dataTransfer);

        Assert.Multiple(() =>
        {
            Assert.That(UploadContentWindow.HasDroppedFiles(dataTransfer), Is.True);
            Assert.That(items, Is.EqualTo(new IStorageItem[] { file, folder }));
        });
    }

    [Test]
    public void GetDroppedStorageItems_FallsBackToRawFileItems()
    {
        using var tempDirectory = new TempDirectory();
        string filePath = System.IO.Path.Combine(tempDirectory.Path, "raw-file.txt");
        File.WriteAllText(filePath, "raw payload");

        IStorageFile file = CreateBclStorageFile(filePath);
        var dataTransfer = new RawOnlyDataTransfer(file);

        var items = UploadContentWindow.GetDroppedStorageItems(dataTransfer);

        Assert.That(items, Is.EqualTo(new IStorageItem[] { file }));
    }

    private static IStorageFile CreateBclStorageFile(string path)
    {
        Type type = typeof(IStorageItem).Assembly.GetType("Avalonia.Platform.Storage.FileIO.BclStorageFile", throwOnError: true)!;
        return (IStorageFile)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public, binder: null, args: new object[] { new FileInfo(path) }, culture: null)!;
    }

    private static IStorageFolder CreateBclStorageFolder(string path)
    {
        Type type = typeof(IStorageItem).Assembly.GetType("Avalonia.Platform.Storage.FileIO.BclStorageFolder", throwOnError: true)!;
        return (IStorageFolder)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public, binder: null, args: new object[] { new DirectoryInfo(path) }, culture: null)!;
    }

    private sealed class RawOnlyDataTransfer(IStorageItem item) : IDataTransfer
    {
        public IReadOnlyList<DataFormat> Formats { get; } = new[] { DataFormat.File };
        public IReadOnlyList<IDataTransferItem> Items { get; } = new[] { new RawOnlyDataTransferItem(item) };

        public void Dispose()
        {
        }
    }

    private sealed class RawOnlyDataTransferItem(IStorageItem item) : IDataTransferItem
    {
        public IReadOnlyList<DataFormat> Formats { get; } = new[] { DataFormat.File };

        public object? TryGetRaw(DataFormat format) =>
            Equals(format, DataFormat.File) ? item : null;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xerahs-upload-drop-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
