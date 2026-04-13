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
using XerahS.Assistant.Models;
using XerahS.Assistant.Services;

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class AssistantPrivacyGuardTests
{
    private readonly AssistantPrivacyGuard _guard = new();

    [Test]
    public void MetadataHistoryLookup_DoesNotRequireConfirmation()
    {
        var decision = _guard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.HistoryLatest,
            AssistantPrivacyScope.MetadataOnly,
            ItemCount: 5));

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.RequiresConfirmation, Is.False);
    }

    [Test]
    public void Upload_RequiresConfirmation()
    {
        var decision = _guard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.UploadFile,
            AssistantPrivacyScope.ExternalShare,
            FilePath: @"C:\shots\capture.png"));

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.RequiresConfirmation, Is.True);
        Assert.That(decision.ConfirmationCopy, Does.Contain("Upload"));
    }

    [Test]
    public void CloudImageRequest_RequiresConfirmation()
    {
        var decision = _guard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.OcrRun,
            AssistantPrivacyScope.CloudImage,
            FilePath: @"C:\shots\capture.png",
            IsLocalOcr: false));

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.RequiresConfirmation, Is.True);
        Assert.That(decision.ConfirmationCopy, Does.Contain("Send image"));
    }

    [Test]
    public void UnknownTool_IsBlocked()
    {
        var decision = _guard.Evaluate(new AssistantPrivacyCheck(
            "shell.execute",
            AssistantPrivacyScope.LocalContent));

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Does.Contain("Unknown"));
    }

    [Test]
    public void UnknownHistoryFileOpen_IsBlocked()
    {
        var decision = _guard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.FileReveal,
            AssistantPrivacyScope.LocalContent,
            FilePath: @"C:\outside\capture.png",
            IsKnownHistoryItem: false));

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.Reason, Does.Contain("known XerahS history"));
    }

    [Test]
    public void LongClipboardWrite_RequiresConfirmation()
    {
        var decision = _guard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.ClipboardCopyText,
            AssistantPrivacyScope.LocalContent,
            Text: new string('x', 1001),
            UserExplicitlyRequested: true));

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.RequiresConfirmation, Is.True);
    }
}
