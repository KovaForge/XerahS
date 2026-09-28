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

using System.Drawing;
using System.Globalization;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>Builds OmaSnap host-mode argument lists. Always passed via ArgumentList, never a shell string.</summary>
internal static class OmaSnapArguments
{
    public const string HostName = "xerahs";

    public static string TargetName(HostedCaptureTarget target) => target switch
    {
        HostedCaptureTarget.Smart => "smart",
        HostedCaptureTarget.Region => "region",
        HostedCaptureTarget.Window => "windows",
        HostedCaptureTarget.Fullscreen => "fullscreen",
        HostedCaptureTarget.Scroll => "scroll",
        _ => "smart"
    };

    public static IReadOnlyList<string> Capture(HostedCaptureRequest request, string outputPath, string resultPath)
    {
        var args = new List<string> { "--host", HostName, "--output", outputPath, "--result-json", resultPath };

        switch (request.Target)
        {
            case HostedCaptureTarget.Smart:
                args.Add("smart");
                break;
            case HostedCaptureTarget.Region:
                args.Add("--capture-region");
                break;
            case HostedCaptureTarget.Window:
                args.Add("--capture-window");
                break;
            case HostedCaptureTarget.Fullscreen:
                args.Add("--capture-fullscreen");
                break;
            case HostedCaptureTarget.Scroll:
                args.Add("--scroll");
                break;
        }

        if (request.Region is Rectangle region && region.Width > 0 && region.Height > 0)
        {
            args.Add("--region");
            args.Add(FormatRegion(region));
        }

        if (!string.IsNullOrWhiteSpace(request.Editor))
        {
            args.Add("--editor");
            args.Add(NormalizeEditor(request.Editor));
        }

        if (request.DisableRecents)
        {
            args.Add("--no-recents");
        }

        return args;
    }

    public static IReadOnlyList<string> Annotate(string inputPath, string outputPath, string resultPath, string editor)
    {
        return
        [
            "--host", HostName,
            "--file", inputPath,
            "--output", outputPath,
            "--result-json", resultPath,
            "--editor", NormalizeEditor(editor)
        ];
    }

    public static IReadOnlyList<string> Pin(string imagePath) => ["--host", HostName, "--pin", imagePath];

    public static IReadOnlyList<string> Probe() => ["--host-capabilities"];

    public static string FormatRegion(Rectangle region) =>
        string.Create(CultureInfo.InvariantCulture, $"{region.X},{region.Y},{region.Width},{region.Height}");

    private static string NormalizeEditor(string editor) =>
        string.Equals(editor, "window", StringComparison.OrdinalIgnoreCase) ? "window" : "overlay";
}
