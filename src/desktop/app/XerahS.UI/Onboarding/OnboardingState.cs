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

namespace XerahS.UI.Onboarding;

/// <summary>
/// Transient state holder for the onboarding wizard.
/// Collects user preferences across all steps before committing to settings.
/// </summary>
public sealed class OnboardingState
{
    // Step 1: Save Location
    public string ScreenshotsFolder { get; set; } = "";
    public bool CreateDateSubfolders { get; set; } = true;

    // Step 3: Hotkeys
    public HotkeyInfo? PrimaryCaptureHotkey { get; set; }
    public List<HotkeyInfo> AdditionalHotkeys { get; set; } = new();

    // Step 4: Upload
    public string? SelectedUploaderId { get; set; }

    // Step 5: OCR
    public List<string> SelectedOcrLanguages { get; set; } = new();
    public bool DownloadOcrInBackground { get; set; }

    // Tracking
    public HashSet<int> SkippedSteps { get; set; } = new();
    public int LastCompletedStepIndex { get; set; } = -1;
}
