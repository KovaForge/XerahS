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

using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>
/// <see cref="IHostedCaptureEngine"/> backed by OmaSnap host mode. Unavailable (and never started)
/// unless <see cref="LinuxDesktopProfile"/> reports a usable probe.
/// </summary>
internal sealed class OmaSnapCaptureEngine : IHostedCaptureEngine
{
    public const string Id = "omasnap";

    private readonly LinuxDesktopProfile _profile;
    private readonly Func<string, OmaSnapClient> _createClient;

    private readonly Func<string> _runtimeFolder;

    public OmaSnapCaptureEngine(
        LinuxDesktopProfile profile,
        Func<string, OmaSnapClient>? createClient = null,
        Func<string>? runtimeFolder = null)
    {
        _profile = profile;
        _createClient = createClient ?? (path => new OmaSnapClient(path));
        _runtimeFolder = runtimeFolder ?? OmaSnapRuntimeFolder.Ensure;
    }

    public string EngineId => Id;

    public HostedCaptureEngineStatus CurrentStatus => ToStatus(_profile.OmaSnapCapabilities);

    public async Task<HostedCaptureEngineStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        OmaSnapCapabilities capabilities = await _profile.EnsureOmaSnapProbedAsync(cancellationToken).ConfigureAwait(false);
        return ToStatus(capabilities);
    }

    public async Task<HostedCaptureResult> CaptureAsync(HostedCaptureRequest request, CancellationToken cancellationToken = default)
    {
        var (client, capabilities, reason) = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        if (client == null)
        {
            return HostedCaptureResult.Unavailable(reason!);
        }

        if (!capabilities!.SupportsTarget(request.Target))
        {
            return HostedCaptureResult.Unavailable($"OmaSnap {capabilities.Version} does not support target '{OmaSnapArguments.TargetName(request.Target)}'.");
        }

        return await client.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HostedCaptureResult> AnnotateAsync(string imagePath, string editor = "overlay", CancellationToken cancellationToken = default)
    {
        var (client, capabilities, reason) = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        if (client == null)
        {
            return HostedCaptureResult.Unavailable(reason!);
        }

        if (capabilities!.Editor.Length > 0 && !capabilities.Editor.Contains(editor, StringComparer.OrdinalIgnoreCase))
        {
            return HostedCaptureResult.Unavailable($"OmaSnap {capabilities.Version} has no '{editor}' editor.");
        }

        return await client.AnnotateAsync(imagePath, editor, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> PinAsync(string imagePath, IReadOnlyList<string>? uploadCommand = null, CancellationToken cancellationToken = default)
    {
        var (client, capabilities, _) = await GetClientAsync(cancellationToken).ConfigureAwait(false);
        if (client == null || !capabilities!.Pin)
        {
            return false;
        }

        // The pin outlives the caller (its Upload button reads the file later), so it gets its own
        // copy in the private runtime folder. Copies older than a day are removed on the next pin.
        string pinPath;
        try
        {
            string pinsFolder = Path.Combine(_runtimeFolder(), "pins");
            Directory.CreateDirectory(pinsFolder);
            RemoveStalePins(pinsFolder);
            pinPath = Path.Combine(pinsFolder, $"pin-{Guid.NewGuid():N}.png");
            File.Copy(imagePath, pinPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            XerahS.Common.DebugHelper.WriteLine($"OmaSnap: cannot stage pin image ({ex.Message}).");
            return false;
        }

        return client.StartPin(pinPath, uploadCommand);
    }

    private static void RemoveStalePins(string pinsFolder)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-1);
        foreach (string file in Directory.EnumerateFiles(pinsFolder, "pin-*.png"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    public void Release(HostedCaptureResult result)
    {
        string? path = _profile.OmaSnapPath;
        if (path != null)
        {
            _createClient(path).Release(result);
        }
    }

    private async Task<(OmaSnapClient? Client, OmaSnapCapabilities? Capabilities, string? Reason)> GetClientAsync(CancellationToken cancellationToken)
    {
        OmaSnapCapabilities capabilities = await _profile.EnsureOmaSnapProbedAsync(cancellationToken).ConfigureAwait(false);
        if (!capabilities.IsUsable || _profile.OmaSnapPath == null)
        {
            return (null, capabilities, capabilities.Describe());
        }

        return (_createClient(_profile.OmaSnapPath), capabilities, null);
    }

    private static HostedCaptureEngineStatus ToStatus(OmaSnapCapabilities? capabilities)
    {
        if (capabilities == null)
        {
            return HostedCaptureEngineStatus.NotAvailable(Id, "OmaSnap not probed yet");
        }

        return new HostedCaptureEngineStatus(capabilities.IsUsable, Id, capabilities.Version, capabilities.Describe(), capabilities.RawJson);
    }
}
