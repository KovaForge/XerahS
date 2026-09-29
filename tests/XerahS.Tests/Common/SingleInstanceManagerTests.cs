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

using System.IO.Pipes;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public class SingleInstanceManagerTests
{
    [Test]
    public void GetPlatformPipeName_DefaultContract_IsWithinUnixDomainSocketLimit()
    {
        string pipeName = SingleInstanceManager.GetPlatformPipeName(AppContracts.SingleInstance.PipeName);

        Assert.That(pipeName, Is.Not.Null.And.Not.Empty);

        if (OperatingSystem.IsWindows())
        {
            Assert.That(pipeName, Is.EqualTo(AppContracts.SingleInstance.PipeName));
            return;
        }

        Assert.That(Path.IsPathRooted(pipeName), Is.True);
        Assert.That(pipeName.Length, Is.LessThanOrEqualTo(SingleInstanceManager.UnixDomainSocketPathMaxLength));
        Assert.That(
            pipeName.Length,
            Is.LessThanOrEqualTo(104),
            "macOS AF_UNIX sun_path is 104 bytes; a longer path throws ArgumentOutOfRangeException at startup.");
    }

    [Test]
    public void GetPlatformPipeName_PersonalFolderSuffix_StaysWithinUnixDomainSocketLimit()
    {
        string logicalName = $"{AppContracts.SingleInstance.PipeName}-0123456789ABCDEF";
        string pipeName = SingleInstanceManager.GetPlatformPipeName(logicalName);

        if (OperatingSystem.IsWindows())
        {
            Assert.That(pipeName, Is.EqualTo(logicalName));
            return;
        }

        Assert.That(pipeName.Length, Is.LessThanOrEqualTo(SingleInstanceManager.UnixDomainSocketPathMaxLength));
        Assert.That(pipeName, Is.Not.EqualTo(SingleInstanceManager.GetPlatformPipeName(AppContracts.SingleInstance.PipeName)));
    }

    [Test]
    public void GetPlatformPipeName_IsStableForTheSameLogicalName()
    {
        string logicalName = AppContracts.SingleInstance.PipeName;
        Assert.That(
            SingleInstanceManager.GetPlatformPipeName(logicalName),
            Is.EqualTo(SingleInstanceManager.GetPlatformPipeName(logicalName)));
    }

    [Test]
    public void GetPlatformPipeName_AllowsNamedPipeServerStreamOnUnix()
    {
        string logicalName = $"{AppContracts.SingleInstance.PipeName}-{Guid.NewGuid():N}";
        string pipeName = SingleInstanceManager.GetPlatformPipeName(logicalName);

        if (OperatingSystem.IsWindows())
        {
            Assert.That(pipeName, Is.EqualTo(logicalName));
            return;
        }

        Assert.That(pipeName.Length, Is.LessThanOrEqualTo(SingleInstanceManager.UnixDomainSocketPathMaxLength));

        try
        {
            using NamedPipeServerStream server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            Assert.That(server.IsConnected, Is.False);
        }
        finally
        {
            try
            {
                if (File.Exists(pipeName))
                {
                    File.Delete(pipeName);
                }
            }
            catch
            {
            }
        }
    }
}
