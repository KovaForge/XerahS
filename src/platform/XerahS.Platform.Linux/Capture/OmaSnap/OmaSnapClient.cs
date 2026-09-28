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

using System.Text.Json;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>
/// Talks to one omasnap binary through the host-mode contract (omasnap 1.22.0+): runs it,
/// waits for the user (captures have no timeout; only the probe does), and turns the result
/// JSON into an <see cref="OmaSnapCaptureResult"/>. Every run gets a private runtime folder
/// that <see cref="Release"/> deletes once the pipeline has taken the PNG.
/// </summary>
internal sealed class OmaSnapClient
{
    public const string HostName = "xerahs";
    public const string UploadCommandEnvironmentVariable = "OMASNAP_HOST_UPLOAD_COMMAND";
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private const int ExitOk = 0;
    private const int ExitUsage = 2;
    private const int ExitCancelled = 3;
    private const string OutputFileName = "capture.png";
    private const string ResultFileName = "result.json";

    private readonly string _runtimeRoot;

    public OmaSnapClient(string binaryPath, string? runtimeRoot = null)
    {
        BinaryPath = binaryPath;
        _runtimeRoot = runtimeRoot ?? ResolveRuntimeRoot(Environment.GetEnvironmentVariable);
    }

    public string BinaryPath { get; }

    public async Task<OmaSnapCapabilities?> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await OmaSnapProcess.RunAsync(BinaryPath, ["--host-capabilities"], null, ProbeTimeout, cancellationToken).ConfigureAwait(false);
            if (run.TimedOut)
            {
                DebugHelper.WriteLine($"OmaSnap: probe timed out ({BinaryPath}).");
                return null;
            }

            return OmaSnapCapabilities.Parse(FirstJsonLine(run.StandardOutput), BinaryPath);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            DebugHelper.WriteLine($"OmaSnap: probe could not run {BinaryPath}: {ex.Message}");
            return null;
        }
    }

    public Task<OmaSnapCaptureResult> CaptureAsync(OmaSnapCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return RunHostedAsync(folder => BuildCaptureArguments(request, Path.Combine(folder, OutputFileName), Path.Combine(folder, ResultFileName)), cancellationToken);
    }

    public Task<OmaSnapCaptureResult> AnnotateAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        return RunHostedAsync(folder => BuildAnnotateArguments(imagePath, Path.Combine(folder, OutputFileName), Path.Combine(folder, ResultFileName)), cancellationToken);
    }

    public bool StartPin(string imagePath, IReadOnlyList<string>? uploadCommand)
    {
        var environment = new Dictionary<string, string?>
        {
            [UploadCommandEnvironmentVariable] = uploadCommand is { Count: > 0 } ? JoinCommand(uploadCommand) : null
        };
        return OmaSnapProcess.StartDetached(BinaryPath, ["--pin", imagePath], environment);
    }

    public void Release(OmaSnapCaptureResult result)
    {
        if (string.IsNullOrEmpty(result.ImagePath))
        {
            return;
        }

        string? folder = Path.GetDirectoryName(result.ImagePath);
        if (folder == null || !IsUnderRuntimeRoot(folder))
        {
            return;
        }

        TryDeleteDirectory(folder);
    }

    private async Task<OmaSnapCaptureResult> RunHostedAsync(Func<string, IReadOnlyList<string>> buildArguments, CancellationToken cancellationToken)
    {
        string folder = CreateRunFolder();
        IReadOnlyList<string> arguments = buildArguments(folder);
        string outputPath = Path.Combine(folder, OutputFileName);
        string resultPath = Path.Combine(folder, ResultFileName);
        DebugHelper.WriteLine($"OmaSnap: running {BinaryPath} {string.Join(' ', arguments)}");

        OmaSnapCaptureResult result;
        try
        {
            // Hosted runs must never pick up an upload command from the user's environment.
            var environment = new Dictionary<string, string?> { [UploadCommandEnvironmentVariable] = null };
            var run = await OmaSnapProcess.RunAsync(BinaryPath, arguments, environment, timeout: null, cancellationToken).ConfigureAwait(false);
            if (run.Cancelled)
            {
                result = new OmaSnapCaptureResult(OmaSnapOutcome.Cancelled);
            }
            else
            {
                string json = File.Exists(resultPath) ? await File.ReadAllTextAsync(resultPath, CancellationToken.None).ConfigureAwait(false) : string.Empty;
                result = ParseResult(json, run.ExitCode, outputPath, run.StandardError);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            result = new OmaSnapCaptureResult(OmaSnapOutcome.Failed) { Error = $"Could not run omasnap: {ex.Message}" };
        }

        if (result.Outcome != OmaSnapOutcome.Succeeded)
        {
            TryDeleteDirectory(folder);
        }

        return result;
    }

    internal static IReadOnlyList<string> BuildCaptureArguments(OmaSnapCaptureRequest request, string outputPath, string resultPath)
    {
        var arguments = new List<string>(HostArguments(outputPath, resultPath));
        if (request.Region is { IsValid: true } region)
        {
            // A fixed rectangle is a non-interactive region capture; it never opens the editor.
            arguments.Add("--capture-region");
            arguments.Add("--region");
            arguments.Add(region.ToString());
            return arguments;
        }

        switch (request.Target)
        {
            case OmaSnapCaptureTarget.Region:
                arguments.Add("--capture-region");
                break;
            case OmaSnapCaptureTarget.Window:
                arguments.Add("--capture-window");
                break;
            case OmaSnapCaptureTarget.Fullscreen:
                arguments.Add("--capture-fullscreen");
                break;
            case OmaSnapCaptureTarget.Scroll:
                arguments.Add("--scroll");
                break;
        }

        if (request.OpenEditor && request.Target != OmaSnapCaptureTarget.Scroll)
        {
            arguments.Add("--editor");
            arguments.Add("overlay");
        }

        return arguments;
    }

    internal static IReadOnlyList<string> BuildAnnotateArguments(string imagePath, string outputPath, string resultPath)
    {
        var arguments = new List<string>(HostArguments(outputPath, resultPath))
        {
            "--file",
            imagePath,
            "--editor",
            "overlay"
        };
        return arguments;
    }

    private static IEnumerable<string> HostArguments(string outputPath, string resultPath)
    {
        yield return "--host";
        yield return HostName;
        yield return "--output";
        yield return outputPath;
        yield return "--result-json";
        yield return resultPath;
        yield return "--no-recents";
    }

    /// <summary>
    /// Interprets a finished run. The result JSON is authoritative; the exit code covers a
    /// process that died before reporting (0 ok, 2 usage, 3 cancelled, anything else failed).
    /// </summary>
    internal static OmaSnapCaptureResult ParseResult(string? json, int exitCode, string outputPath, string? standardError = null)
    {
        JsonElement root = default;
        bool parsed = false;
        JsonDocument? document = null;
        try
        {
            string? line = FirstJsonLine(json);
            if (line != null)
            {
                document = JsonDocument.Parse(line);
                root = document.RootElement;
                parsed = root.ValueKind == JsonValueKind.Object &&
                         root.TryGetProperty("schemaVersion", out var schema) &&
                         schema.ValueKind == JsonValueKind.Number &&
                         schema.GetInt32() == 1;
            }
        }
        catch (JsonException)
        {
            parsed = false;
        }

        try
        {
            if (!parsed)
            {
                return exitCode switch
                {
                    ExitCancelled => new OmaSnapCaptureResult(OmaSnapOutcome.Cancelled),
                    ExitUsage => new OmaSnapCaptureResult(OmaSnapOutcome.Failed) { Error = Describe("omasnap rejected the arguments", standardError) },
                    ExitOk => new OmaSnapCaptureResult(OmaSnapOutcome.Failed) { Error = "omasnap exited without a result." },
                    _ => new OmaSnapCaptureResult(OmaSnapOutcome.Failed) { Error = Describe($"omasnap failed (exit code {exitCode})", standardError) }
                };
            }

            string status = GetString(root, "status") ?? string.Empty;
            if (status == "cancelled")
            {
                return new OmaSnapCaptureResult(OmaSnapOutcome.Cancelled);
            }

            if (status != "ok")
            {
                return new OmaSnapCaptureResult(OmaSnapOutcome.Failed) { Error = GetString(root, "error") ?? $"omasnap reported '{status}'." };
            }

            string path = GetString(root, "path") ?? outputPath;
            if (!File.Exists(path))
            {
                return new OmaSnapCaptureResult(OmaSnapOutcome.Failed) { Error = $"omasnap reported success but {path} does not exist." };
            }

            string? windowClass = null;
            string? windowTitle = null;
            if (root.TryGetProperty("window", out var window) && window.ValueKind == JsonValueKind.Object)
            {
                windowClass = GetString(window, "class");
                windowTitle = GetString(window, "title");
            }

            OmaSnapRegion? region = null;
            if (root.TryGetProperty("region", out var regionElement) && regionElement.ValueKind == JsonValueKind.Object)
            {
                region = new OmaSnapRegion(GetInt(regionElement, "x"), GetInt(regionElement, "y"), GetInt(regionElement, "width"), GetInt(regionElement, "height"));
            }

            return new OmaSnapCaptureResult(OmaSnapOutcome.Succeeded)
            {
                ImagePath = path,
                WindowClass = string.IsNullOrWhiteSpace(windowClass) ? null : windowClass,
                WindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? null : windowTitle,
                Region = region,
                Scale = root.TryGetProperty("scale", out var scale) && scale.ValueKind == JsonValueKind.Number ? scale.GetDouble() : 1.0,
                Monitor = GetString(root, "monitor"),
                Annotated = root.TryGetProperty("annotated", out var annotated) && annotated.ValueKind == JsonValueKind.True
            };
        }
        finally
        {
            document?.Dispose();
        }
    }

    /// <summary>
    /// Private runtime folder: $XDG_RUNTIME_DIR/xerahs/omasnap, or /tmp/xerahs-USER/omasnap.
    /// </summary>
    internal static string ResolveRuntimeRoot(Func<string, string?> getEnvironmentVariable)
    {
        string? runtimeDir = getEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDir) && Path.IsPathRooted(runtimeDir))
        {
            return Path.Combine(runtimeDir, "xerahs", "omasnap");
        }

        string user = getEnvironmentVariable("USER") ?? Environment.UserName;
        return Path.Combine(Path.GetTempPath(), $"xerahs-{user}", "omasnap");
    }

    /// <summary>Joins argv back into the quoted form omasnap splits (OMASNAP_HOST_UPLOAD_COMMAND).</summary>
    internal static string JoinCommand(IReadOnlyList<string> command)
    {
        return string.Join(' ', command.Select(QuoteArgument));
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.All(c => char.IsLetterOrDigit(c) || "/._-=:+@,".Contains(c)))
        {
            return argument;
        }

        return "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    private string CreateRunFolder()
    {
        string? parent = Path.GetDirectoryName(_runtimeRoot);
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
        {
            CreatePrivateDirectory(parent);
        }

        CreatePrivateDirectory(_runtimeRoot);
        string folder = Path.Combine(_runtimeRoot, Guid.NewGuid().ToString("N"));
        CreatePrivateDirectory(folder);
        return folder;
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private bool IsUnderRuntimeRoot(string folder)
    {
        string root = Path.GetFullPath(_runtimeRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(folder).StartsWith(root, StringComparison.Ordinal);
    }

    private static void TryDeleteDirectory(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugHelper.WriteLine($"OmaSnap: could not delete runtime folder {folder}: {ex.Message}");
        }
    }

    private static string? FirstJsonLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('{'))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static string Describe(string message, string? standardError)
    {
        string? detail = standardError?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return string.IsNullOrWhiteSpace(detail) ? message + "." : $"{message}: {detail}";
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int GetInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
    }
}
