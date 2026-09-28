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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>
/// Linux <see cref="IOmaSnapService"/>. The capability probe runs once in the background at
/// platform start (never on the UI thread) and is cached in <see cref="LinuxDesktopProfile"/>.
/// Until it passes, <see cref="ShouldHandle"/> is false and XerahS behaves exactly as before.
/// A capture requested while another is still open cancels the open one (a second key press
/// dismisses the overlay, like OmaSnap's own toggle).
/// </summary>
public sealed class OmaSnapService : IOmaSnapService
{
    private readonly LinuxDesktopProfile _profile;
    private readonly Func<IReadOnlyList<string>> _candidates;
    private readonly Func<IReadOnlyList<string>?> _uploadCommand;
    private readonly string? _runtimeRoot;
    private readonly object _gate = new();
    private Task? _probe;
    private OmaSnapClient? _client;
    private OmaSnapStatus _status = OmaSnapStatus.NotProbed;
    private CancellationTokenSource? _activeCapture;

    public OmaSnapService(LinuxDesktopProfile profile)
        : this(profile, () => OmaSnapLocator.GetCandidates(), DefaultUploadCommand, runtimeRoot: null)
    {
    }

    internal OmaSnapService(
        LinuxDesktopProfile profile,
        Func<IReadOnlyList<string>> candidates,
        Func<IReadOnlyList<string>?> uploadCommand,
        string? runtimeRoot)
    {
        _profile = profile;
        _candidates = candidates;
        _uploadCommand = uploadCommand;
        _runtimeRoot = runtimeRoot;
    }

    public OmaSnapStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public LinuxDesktopProfile Profile => _profile;

    /// <summary>Starts the background probe once; later calls return the same task.</summary>
    public Task EnsureProbedAsync()
    {
        lock (_gate)
        {
            _probe ??= Task.Run(ProbeAsync);
            return _probe;
        }
    }

    public bool ShouldHandle(LinuxInteractiveRegionSelectorPreference preference)
    {
        if (!Status.IsAvailable || _profile.IsSandboxed)
        {
            return false;
        }

        return preference switch
        {
            LinuxInteractiveRegionSelectorPreference.OmaSnap => true,
            LinuxInteractiveRegionSelectorPreference.Automatic => _profile.IsOmarchyLike,
            _ => false
        };
    }

    public async Task<OmaSnapCaptureResult> CaptureAsync(OmaSnapCaptureRequest request, CancellationToken cancellationToken = default)
    {
        OmaSnapClient? client = GetClient();
        if (client == null)
        {
            return OmaSnapCaptureResult.Unavailable(Status.Summary);
        }

        CancellationTokenSource capture;
        lock (_gate)
        {
            if (_activeCapture != null)
            {
                // Toggle: the second request closes the open overlay and is itself a cancel.
                DebugHelper.WriteLine("OmaSnap: a capture is already open; closing it.");
                _activeCapture.Cancel();
                return new OmaSnapCaptureResult(OmaSnapOutcome.Cancelled);
            }

            capture = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCapture = capture;
        }

        try
        {
            OmaSnapCaptureResult result = await client.CaptureAsync(request, capture.Token).ConfigureAwait(false);
            DebugHelper.WriteLine($"OmaSnap: stage=OmaSnap, provider=omasnap, target={request.Target}, outcome={result.Outcome}" +
                (result.Error != null ? $", reason={result.Error}" : string.Empty));
            return result;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeCapture, capture))
                {
                    _activeCapture = null;
                }
            }

            capture.Dispose();
        }
    }

    public async Task<OmaSnapCaptureResult> AnnotateAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        OmaSnapClient? client = GetClient();
        if (client == null)
        {
            return OmaSnapCaptureResult.Unavailable(Status.Summary);
        }

        OmaSnapCaptureResult result = await client.AnnotateAsync(imagePath, cancellationToken).ConfigureAwait(false);
        DebugHelper.WriteLine($"OmaSnap: annotate outcome={result.Outcome}" + (result.Error != null ? $", reason={result.Error}" : string.Empty));
        return result;
    }

    public Task<bool> PinAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        OmaSnapClient? client = GetClient();
        if (client == null || !File.Exists(imagePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            return Task.FromResult(client.StartPin(imagePath, _uploadCommand()));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            DebugHelper.WriteLine($"OmaSnap: pin failed to start: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public void Release(OmaSnapCaptureResult result)
    {
        GetClient()?.Release(result);
    }

    private OmaSnapClient? GetClient()
    {
        lock (_gate)
        {
            return _status.IsAvailable ? _client : null;
        }
    }

    private async Task ProbeAsync()
    {
        if (_profile.IsSandboxed || !_profile.IsHyprland)
        {
            // No Hyprland session: OmaSnap cannot run, so do not even start a process.
            _profile.SetOmaSnapProbeResult(null);
            SetStatus(null, new OmaSnapStatus(false, null, null, _profile.IsSandboxed ? "OmaSnap: not used in a sandbox" : "OmaSnap: needs Hyprland"));
            return;
        }

        OmaSnapCapabilities? firstParsed = null;
        foreach (string candidate in _candidates())
        {
            var client = new OmaSnapClient(candidate, _runtimeRoot);
            OmaSnapCapabilities? capabilities = await client.ProbeAsync().ConfigureAwait(false);
            if (capabilities == null)
            {
                continue;
            }

            firstParsed ??= capabilities;
            if (capabilities.IsUsable)
            {
                _profile.SetOmaSnapProbeResult(capabilities);
                SetStatus(client, new OmaSnapStatus(true, capabilities.Version, candidate, capabilities.Summary));
                DebugHelper.WriteLine($"OmaSnap: using {candidate} ({capabilities.Summary}).");
                return;
            }

            DebugHelper.WriteLine($"OmaSnap: {candidate} is not usable ({capabilities.Summary}).");
        }

        _profile.SetOmaSnapProbeResult(firstParsed);
        string summary = firstParsed?.Summary ?? "OmaSnap: not installed";
        SetStatus(null, new OmaSnapStatus(false, firstParsed?.Version, firstParsed?.BinaryPath, summary));
        // A failed probe is a silent fallback plus one diagnostic line.
        DebugHelper.WriteLine($"OmaSnap: unavailable, using the existing capture chain ({summary}).");
    }

    private void SetStatus(OmaSnapClient? client, OmaSnapStatus status)
    {
        lock (_gate)
        {
            _client = client;
            _status = status;
        }
    }

    /// <summary>omaxerahs upload, bundled next to XerahS, so a pin's Upload button uses the user's destination.</summary>
    private static IReadOnlyList<string>? DefaultUploadCommand()
    {
        string omaxerahs = Path.Combine(AppContext.BaseDirectory, "omaxerahs");
        return File.Exists(omaxerahs) ? [omaxerahs, "upload"] : null;
    }
}
