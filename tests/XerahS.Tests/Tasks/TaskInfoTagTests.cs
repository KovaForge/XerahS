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
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Tasks;

[TestFixture]
[NonParallelizable]
public sealed class TaskInfoTagTests
{
    [SetUp]
    public void SetUp()
    {
        ClearInstances();
    }

    [TearDown]
    public void TearDown()
    {
        ClearInstances();
    }

    [Test]
    public void GetTags_IncludesNonEmptyOcrText()
    {
        var info = new TaskInfo
        {
            Metadata = new TaskMetadata
            {
                OcrText = "bonjour"
            }
        };

        var tags = info.GetTags();

        Assert.That(tags, Is.Not.Null);
        Assert.That(tags!, Contains.Key(nameof(TaskMetadata.OcrText)).WithValue("bonjour"));
    }

    [Test]
    public void GetTags_SkipsWhitespaceOnlyOcrText()
    {
        var info = new TaskInfo
        {
            Metadata = new TaskMetadata
            {
                OcrText = "  \t  "
            }
        };

        var tags = info.GetTags();

        Assert.That(tags, Is.Null);
    }

    [Test]
    public void UploaderHost_ReturnsDefaultInstanceDisplayNameWhenDestinationIsNotExplicit()
    {
        var defaultInstance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.File,
            DisplayName = "Default File Host",
            IsAvailable = true
        };

        InstanceManager.Instance.AddInstance(defaultInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, defaultInstance.InstanceId);

        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.File
        };

        Assert.That(info.UploaderHost, Is.EqualTo("Default File Host"));
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }
}
