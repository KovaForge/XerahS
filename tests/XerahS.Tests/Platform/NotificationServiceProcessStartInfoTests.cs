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

using System.Diagnostics;
using NUnit.Framework;
using XerahS.Platform.Linux.Services;
using XerahS.Platform.MacOS.Services;
using XerahS.Services.Abstractions;

namespace XerahS.Tests.Platform;

public class NotificationServiceProcessStartInfoTests
{
    [Test]
    public void LinuxNotificationService_CreateStartInfo_PreservesQuotedArguments_AndMapsUrgency()
    {
        const string title = "Capture \"done\"";
        const string message = "Saved to /tmp/shot\nReady for upload";

        var startInfo = LinuxNotificationService.CreateStartInfo(title, message, NotificationType.Error);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("notify-send"));
            Assert.That(startInfo.RedirectStandardOutput, Is.False);
            Assert.That(startInfo.RedirectStandardError, Is.False);
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "-u", "critical", title, message }));
        });
    }

    [Test]
    public void MacOSNotificationService_CreateStartInfo_EscapesAppleScriptStringLiteral_AndIncludesSubtitleForTypedNotifications()
    {
        const string title = "Capture \"done\"";
        const string message = "Saved to C:\\Shots\\latest\nReady for upload";

        var startInfo = MacOSNotificationService.CreateStartInfo(title, message, NotificationType.Warning);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("osascript"));
            Assert.That(startInfo.RedirectStandardOutput, Is.False);
            Assert.That(startInfo.RedirectStandardError, Is.False);
            Assert.That(startInfo.ArgumentList.Count, Is.EqualTo(2));
            Assert.That(startInfo.ArgumentList[0], Is.EqualTo("-e"));
            Assert.That(startInfo.ArgumentList[1], Is.EqualTo("display notification \"Saved to C:\\\\Shots\\\\latest\\nReady for upload\" with title \"Capture \\\"done\\\"\" subtitle \"Warning\""));
        });
    }

    [Test]
    public void MacOSNotificationService_CreateStartInfo_OmitsSubtitleForInfoNotifications()
    {
        var startInfo = MacOSNotificationService.CreateStartInfo("Capture complete", "Saved", NotificationType.Info);

        Assert.That(startInfo.ArgumentList[1], Is.EqualTo("display notification \"Saved\" with title \"Capture complete\""));
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void LinuxNotificationService_WaitForSuccessfulExit_KillsTimedOutProcess()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            ArgumentList = { "-c", "sleep 5" }
        });

        Assert.That(process, Is.Not.Null);

        var result = LinuxNotificationService.WaitForSuccessfulExit(process!, 50);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(process!.HasExited, Is.True);
        });
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void MacOSNotificationService_WaitForSuccessfulExit_KillsTimedOutProcess()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            ArgumentList = { "-c", "sleep 5" }
        });

        Assert.That(process, Is.Not.Null);

        var result = MacOSNotificationService.WaitForSuccessfulExit(process!, 50);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(process!.HasExited, Is.True);
        });
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void LinuxNotificationService_WaitForSuccessfulExit_ReturnsFalseForNonZeroExit()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            ArgumentList = { "-c", "exit 7" }
        });

        Assert.That(process, Is.Not.Null);

        Assert.That(LinuxNotificationService.WaitForSuccessfulExit(process!, 2000), Is.False);
    }

    [Test]
    public void LinuxNotificationService_CreateActionStartInfo_IncludesWaitAndActionFlags()
    {
        var startInfo = LinuxNotificationService.CreateActionStartInfo("Upload", "Done", "Open URL", NotificationType.Success);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.RedirectStandardOutput, Is.True);
            Assert.That(startInfo.ArgumentList, Does.Contain("--action"));
            Assert.That(startInfo.ArgumentList, Does.Contain("--wait"));
            Assert.That(startInfo.ArgumentList, Does.Contain($"{LinuxNotificationService.DefaultActionKey}=Open URL"));
        });
    }

    [Test]
    public void PortalNotificationService_MapPriority_MapsNotificationTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Success), Is.EqualTo("low"));
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Warning), Is.EqualTo("normal"));
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Error), Is.EqualTo("urgent"));
            Assert.That(PortalNotificationService.MapPriority(NotificationType.Info), Is.EqualTo("normal"));
        });
    }
}
