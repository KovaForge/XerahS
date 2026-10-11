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
using XerahS.OmaXerahs.Services;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
public class UploadHostRoutingTests
{
    [Test]
    public void RoutesImages_AllFileTypes()
    {
        Assert.That(UploadHost.RoutesImages(new FileTypeScope { AllFileTypes = true }), Is.True);
    }

    [TestCase("png")]
    [TestCase(".PNG")]
    [TestCase("jpeg")]
    [TestCase(".webp")]
    public void RoutesImages_ImageExtension(string extension)
    {
        Assert.That(UploadHost.RoutesImages(new FileTypeScope { FileExtensions = [extension] }), Is.True);
    }

    [Test]
    public void RoutesImages_NotForOtherTypesOrNoRouting()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UploadHost.RoutesImages(new FileTypeScope { FileExtensions = ["zip", ".mp4", "txt"] }), Is.False);
            Assert.That(UploadHost.RoutesImages(new FileTypeScope()), Is.False);
            Assert.That(UploadHost.RoutesImages(null), Is.False);
        });
    }
}
