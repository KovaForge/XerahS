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

using System.Runtime.InteropServices;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Mobile;

public class MobileDiagnosticService : IDiagnosticService
{
    private const string FolderName = "CaptureTroubleshooting";

    public string WriteRegionCaptureDiagnostics(string personalFolder)
        => WriteDiagnostics(personalFolder, "mobile-region-capture");

    public string WriteRecordingDiagnostics(string personalFolder)
        => WriteDiagnostics(personalFolder, "mobile-recording");

    private static string WriteDiagnostics(string personalFolder, string diagnosticType)
    {
        if (string.IsNullOrWhiteSpace(personalFolder))
        {
            return string.Empty;
        }

        try
        {
            string folder = Path.Combine(personalFolder, FolderName);
            Directory.CreateDirectory(folder);

            string fileName = $"{diagnosticType}-diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            string filePath = Path.Combine(folder, fileName);

            File.WriteAllText(filePath, BuildReport(diagnosticType), Encoding.UTF8);
            return filePath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildReport(string diagnosticType)
    {
        StringBuilder sb = new();
        sb.AppendLine("============================================================");
        sb.AppendLine("                   MOBILE DIAGNOSTICS");
        sb.AppendLine("============================================================");
        sb.AppendLine($"DiagnosticType: {diagnosticType}");
        sb.AppendLine($"TimestampLocal: {DateTime.Now:O}");
        sb.AppendLine($"TimestampUtc: {DateTime.UtcNow:O}");
        sb.AppendLine($"OSVersion: {Environment.OSVersion.VersionString}");
        sb.AppendLine($"FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OSArchitecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"MachineName: {Environment.MachineName}");
        sb.AppendLine($"ProcessPath: {Environment.ProcessPath ?? "unknown"}");
        sb.AppendLine($"CurrentDirectory: {Environment.CurrentDirectory}");
        sb.AppendLine();
        sb.AppendLine("Mobile platform diagnostics are currently limited.");
        sb.AppendLine("This report confirms runtime environment details and that the diagnostic request completed.");
        return sb.ToString();
    }
}
