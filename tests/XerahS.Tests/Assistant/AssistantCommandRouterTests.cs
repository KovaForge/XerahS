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
using XerahS.UI.Assistant;

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class AssistantCommandRouterTests
{
    private readonly AssistantCommandRouter _router = new();

    [Test]
    public void LastFiveScreenshots_ParsesHistoryLookup()
    {
        var intent = _router.Parse("give me local file path of last 5 screenshots");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.LatestScreenshotPaths));
        Assert.That(intent.Limit, Is.EqualTo(5));
    }

    [Test]
    public void LastFiveScreenshots_LocalFilePathAfterScreenshots_ParsesHistoryLookup()
    {
        var intent = _router.Parse("give me last 5 screenshots local file path separated by ;");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.LatestScreenshotPaths));
        Assert.That(intent.Limit, Is.EqualTo(5));
        Assert.That(intent.Separator, Is.EqualTo(";"));
    }

    [Test]
    public void LastFiveScreenshots_LocalFilepathSingleWord_ParsesHistoryLookup()
    {
        var intent = _router.Parse("give me the last 5 screenshots local filepath separated by ;");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.LatestScreenshotPaths));
        Assert.That(intent.Limit, Is.EqualTo(5));
        Assert.That(intent.Separator, Is.EqualTo(";"));
    }

    [Test]
    public void LastFiveScreenshots_SemicolonsWordSeparator_IsNormalized()
    {
        var intent = _router.Parse("last 5 screenshot paths separated by semicolons.");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.LatestScreenshotPaths));
        Assert.That(intent.Limit, Is.EqualTo(5));
        Assert.That(intent.Separator, Is.EqualTo(";"));
    }

    [Test]
    public void LastFiveScreenshots_ArticleBeforeSemicolon_IsNormalized()
    {
        var intent = _router.Parse("last 5 screenshot paths separated by a semicolon.");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.LatestScreenshotPaths));
        Assert.That(intent.Limit, Is.EqualTo(5));
        Assert.That(intent.Separator, Is.EqualTo(";"));
    }

    [Test]
    public void LastScreenshotLimit_IsClampedToTen()
    {
        var intent = _router.Parse("give me local file path of last 25 screenshots");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.LatestScreenshotPaths));
        Assert.That(intent.Limit, Is.EqualTo(10));
    }

    [Test]
    public void CopyLatestScreenshotPath_ParsesCopyIntent()
    {
        var intent = _router.Parse("copy the path of the latest screenshot");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.CopyLatestScreenshotPath));
        Assert.That(intent.CopyRequested, Is.True);
    }

    [Test]
    public void OpenLatestScreenshot_ParsesOpenIntent()
    {
        var intent = _router.Parse("open the most recent screenshot in the editor");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.OpenLatestScreenshot));
    }

    [Test]
    public void RevealLatestCapture_ParsesRevealIntent()
    {
        var intent = _router.Parse("reveal the latest capture in explorer");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.RevealLatestScreenshot));
    }

    [Test]
    public void OcrLatestScreenshot_ParsesOcrIntent()
    {
        var intent = _router.Parse("extract text from the latest screenshot");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.OcrLatestScreenshot));
        Assert.That(intent.CopyRequested, Is.False);
    }

    [Test]
    public void CopyOcrLatestScreenshot_ParsesCopyOcrIntent()
    {
        var intent = _router.Parse("copy OCR text from the latest screenshot");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.CopyOcrLatestScreenshot));
        Assert.That(intent.CopyRequested, Is.True);
    }

    [Test]
    public void UploadLatestScreenshot_ParsesUploadIntent()
    {
        var intent = _router.Parse("upload the latest screenshot");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.UploadLatestScreenshot));
        Assert.That(intent.Limit, Is.EqualTo(1));
    }

    [Test]
    public void RunWorkflow_ParsesWorkflowName()
    {
        var intent = _router.Parse("run workflow region capture");

        Assert.That(intent.Kind, Is.EqualTo(AssistantDeterministicIntentKind.RunWorkflow));
        Assert.That(intent.Argument, Is.EqualTo("region capture"));
    }
}
