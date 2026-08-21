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

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using XerahS.UI.Helpers;

namespace XerahS.Tests.UI;

[TestFixture]
public class SilentRunStartupPolicyTests
{
    [Test]
    public void ShouldHideMainWindowToTray_WhenSettingEnabled_HidesOnFirstOpen()
    {
        Assert.That(
            SilentRunStartupPolicy.ShouldHideMainWindowToTray(
                silentRunEnabled: true, isExiting: false, alreadyApplied: false),
            Is.True);
    }

    [Test]
    public void ShouldHideMainWindowToTray_WhenSettingDisabled_DoesNotHide()
    {
        Assert.That(
            SilentRunStartupPolicy.ShouldHideMainWindowToTray(
                silentRunEnabled: false, isExiting: false, alreadyApplied: false),
            Is.False);
    }

    [Test]
    public void ShouldHideMainWindowToTray_WhenAlreadyApplied_DoesNotHideAgain()
    {
        Assert.That(
            SilentRunStartupPolicy.ShouldHideMainWindowToTray(
                silentRunEnabled: true, isExiting: false, alreadyApplied: true),
            Is.False);
    }

    [Test]
    public void ShouldHideMainWindowToTray_WhenExiting_DoesNotHide()
    {
        Assert.That(
            SilentRunStartupPolicy.ShouldHideMainWindowToTray(
                silentRunEnabled: true, isExiting: true, alreadyApplied: false),
            Is.False);
    }

    [Test]
    public void ShouldActivateWindowOnNavigate_DuringConstruction_IsFalse()
    {
        Assert.That(SilentRunStartupPolicy.ShouldActivateWindowOnNavigate(suppressWindowActivation: true), Is.False);
    }

    [Test]
    public void ShouldActivateWindowOnNavigate_AfterConstruction_IsTrue()
    {
        Assert.That(SilentRunStartupPolicy.ShouldActivateWindowOnNavigate(suppressWindowActivation: false), Is.True);
    }

    [AvaloniaTest]
    public void ApplyHiddenToTray_HidesVisibleWindow_AndRemovesTaskbarButton()
    {
        var window = new Window
        {
            ShowInTaskbar = true,
            ShowActivated = true,
            Width = 200,
            Height = 200
        };

        window.Show();
        Assert.That(window.IsVisible, Is.True);

        SilentRunStartupPolicy.ApplyHiddenToTray(window);

        Assert.That(window.IsVisible, Is.False);
        Assert.That(window.ShowInTaskbar, Is.False);
        Assert.That(window.ShowActivated, Is.False);
    }
}
