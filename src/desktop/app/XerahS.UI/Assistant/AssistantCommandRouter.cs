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

using System.Text.RegularExpressions;

namespace XerahS.UI.Assistant;

public enum AssistantDeterministicIntentKind
{
    Unknown,
    LatestScreenshotPaths,
    CopyLatestScreenshotPath,
    OpenLatestScreenshot,
    RevealLatestScreenshot
}

public sealed record AssistantDeterministicIntent(
    AssistantDeterministicIntentKind Kind,
    int Limit,
    bool CopyRequested);

public sealed class AssistantCommandRouter
{
    public const int MaxLatestScreenshotLimit = 10;

    private static readonly Regex LastScreenshotPathsRegex = new(
        @"\b(?:give\s+me\s+)?(?:the\s+)?(?:local\s+)?(?:file\s+)?paths?\s+(?:of\s+)?(?:my\s+)?(?:last|latest)\s+(?<limit>\d{1,2})\s+screenshot(?:s)?\b|\b(?:last|latest)\s+(?<limit2>\d{1,2})\s+screenshot(?:s)?\s+(?:paths?|file\s+paths?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LatestScreenshotPathRegex = new(
        @"\b(?:latest|last|most\s+recent)\s+screenshot\b.*\b(?:path|file\s+path)\b|\b(?:path|file\s+path)\b.*\b(?:latest|last|most\s+recent)\s+screenshot\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CopyLatestScreenshotPathRegex = new(
        @"\bcopy\b.*(?:\b(?:latest|last|most\s+recent)\s+screenshot\b.*\b(?:path|file\s+path)\b|\b(?:path|file\s+path)\b.*\b(?:latest|last|most\s+recent)\s+screenshot\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OpenLatestScreenshotRegex = new(
        @"\bopen\b.*\b(?:latest|last|most\s+recent)\s+screenshot\b.*\b(?:editor|edit|image\s+editor)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RevealLatestScreenshotRegex = new(
        @"\b(?:reveal|show)\b.*\b(?:latest|last|most\s+recent)\s+(?:capture|screenshot)\b.*\b(?:folder|explorer|finder|files)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public AssistantDeterministicIntent Parse(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.Unknown, 0, CopyRequested: false);
        }

        if (CopyLatestScreenshotPathRegex.IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.CopyLatestScreenshotPath, 1, CopyRequested: true);
        }

        if (OpenLatestScreenshotRegex.IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.OpenLatestScreenshot, 1, CopyRequested: false);
        }

        if (RevealLatestScreenshotRegex.IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.RevealLatestScreenshot, 1, CopyRequested: false);
        }

        var lastMatch = LastScreenshotPathsRegex.Match(prompt);
        if (lastMatch.Success)
        {
            string limitText = lastMatch.Groups["limit"].Success
                ? lastMatch.Groups["limit"].Value
                : lastMatch.Groups["limit2"].Value;

            int limit = int.TryParse(limitText, out int parsed)
                ? Math.Clamp(parsed, 1, MaxLatestScreenshotLimit)
                : 5;

            return new AssistantDeterministicIntent(
                AssistantDeterministicIntentKind.LatestScreenshotPaths,
                limit,
                CopyRequested: prompt.Contains("copy", StringComparison.OrdinalIgnoreCase));
        }

        if (LatestScreenshotPathRegex.IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(
                AssistantDeterministicIntentKind.LatestScreenshotPaths,
                1,
                CopyRequested: prompt.Contains("copy", StringComparison.OrdinalIgnoreCase));
        }

        return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.Unknown, 0, CopyRequested: false);
    }

    public static IReadOnlyList<string> GetSuggestions() =>
    [
        "Give me the local file path of my last 5 screenshots",
        "Copy the path of the latest screenshot",
        "Open the latest screenshot in the editor",
        "Reveal the latest capture in Explorer"
    ];
}
