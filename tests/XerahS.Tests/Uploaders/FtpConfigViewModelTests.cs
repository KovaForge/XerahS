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
using ShareX.Ftp.Plugin.ViewModels;
using XerahS.Uploaders;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class FtpConfigViewModelTests
{
    [Test]
    public void ProtocolChange_UpdatesDefaultPortWhenUserHasNotCustomizedIt()
    {
        var viewModel = new FtpConfigViewModel();

        viewModel.Protocol = FTPProtocol.SFTP;

        Assert.That(viewModel.Port, Is.EqualTo(22));
    }

    [Test]
    public void ProtocolChange_PreservesCustomPort()
    {
        var viewModel = new FtpConfigViewModel
        {
            Port = 2021
        };

        viewModel.Protocol = FTPProtocol.SFTP;

        Assert.That(viewModel.Port, Is.EqualTo(2021));
    }

    [Test]
    public void Validate_RejectsOutOfRangePort()
    {
        var viewModel = new FtpConfigViewModel
        {
            Host = "example.com",
            Port = 0
        };

        bool isValid = viewModel.Validate();

        Assert.That(isValid, Is.False);
        Assert.That(viewModel.StatusMessage, Is.EqualTo("Port must be between 1 and 65535."));
    }
}
