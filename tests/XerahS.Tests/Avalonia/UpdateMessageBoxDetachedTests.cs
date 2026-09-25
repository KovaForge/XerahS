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
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.Tests.Avalonia;

/// <summary>
/// UpdateMessageBox is a ModalContent UserControl; close contract lives on the view-model.
/// </summary>
[TestFixture]
public class UpdateMessageBoxViewModelTests
{
    [Test]
    public void CreateViewModel_MapsCheckerFields()
    {
        UpdateMessageBoxViewModel vm = UpdateMessageBox.CreateViewModel(new TestUpdateChecker
        {
            CurrentVersion = new Version(0, 22, 170),
            LatestVersion = new Version(0, 23, 28),
            IsPortable = false
        });

        Assert.That(vm.CurrentVersion, Is.EqualTo("0.22.170"));
        Assert.That(vm.LatestVersion, Is.EqualTo("0.23.28"));
        Assert.That(vm.IsPortable, Is.False);
    }

    [Test]
    public void YesCommand_RequestsCloseTrue()
    {
        UpdateMessageBoxViewModel vm = UpdateMessageBox.CreateViewModel(CreateChecker());
        bool? result = null;
        vm.RequestClose = value => result = value;

        vm.YesCommand.Execute(null);

        Assert.That(result, Is.True);
    }

    [Test]
    public void NoCommand_RequestsCloseFalse()
    {
        UpdateMessageBoxViewModel vm = UpdateMessageBox.CreateViewModel(CreateChecker());
        bool? result = null;
        vm.RequestClose = value => result = value;

        vm.NoCommand.Execute(null);

        Assert.That(result, Is.False);
    }

    private static TestUpdateChecker CreateChecker() => new()
    {
        CurrentVersion = new Version(0, 22, 170),
        LatestVersion = new Version(0, 23, 28),
        IsPortable = false
    };

    private sealed class TestUpdateChecker : UpdateChecker
    {
        public override Task CheckUpdateAsync() => Task.CompletedTask;
    }
}
