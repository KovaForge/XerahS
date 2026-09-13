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

// Source spec: KFIP0010 §"Pre-softening target" + KFIP0019 §"Quantitative X Recompression Pipeline"
// Source code: docs/proposals/kfip/KFIP0020-capture-and-go-unified-gesture.md §B
//
// Pure-function pre-softening for the X / Twitter feed pipeline.
// Picks the smallest X-fit variant that fits the captured region's aspect
// ratio with no more than one axis downsampled, and downsample only when
// either axis exceeds the 1600px capture ceiling.
//
// No XerahS.Core, XerahS.Common, or platform dependency. Decoupled so
// it can be tested without the Avalonia UI shim.

namespace XerahS.Core.Media;

/// <summary>
/// The X / Twitter pre-softening presets adopted verbatim from KFIP0010
/// and sharpened by KFIP0019's screensnap.pro data. Long-axis bounds
/// follow X's documented downsampling threshold (~1600 px).
/// </summary>
public enum XTargetProfile
{
    /// <summary>Twitter "summary_large_image" card. 1200x628 (1.91:1).</summary>
    TwitterCard,        // 1200 x 628

    /// <summary>Single in-feed image, 16:9. 1200x675.</summary>
    InFeed16x9,         // 1200 x 675

    /// <summary>Two-image post slot, 2:1. 1200x600.</summary>
    TwoUp2x1,           // 1200 x 600

    /// <summary>Three-image lead slot, 16:9 (matches InFeed16x9).</summary>
    ThreeUpLead,        // 1200 x 675

    /// <summary>Four-image grid cell, ~16:9 (matches InFeed16x9).</summary>
    FourUpCell,         // 1200 x 675
}

/// <summary>
/// Resolved target dimensions for the chosen profile.
/// </summary>
/// <param name="TargetWidth">Width in pixels (always &lt;= 1600).</param>
/// <param name="TargetHeight">Height in pixels (always &lt;= 1600).</param>
/// <param name="Profile">The X profile variant selected.</param>
/// <param name="WillUpscale">
/// True if the source region is smaller than the target on at least one axis.
/// Per spec, we never upscale; <see cref="XPreSoftenPlanner.Plan"/> returns
/// the source dimensions in this case via <see cref="AdjustedWidth"/>
/// and <see cref="AdjustedHeight"/>.
/// </param>
public readonly record struct XTargetSize(
    int TargetWidth,
    int TargetHeight,
    XTargetProfile Profile,
    bool WillUpscale)
{
    /// <summary>
    /// The actual width to render. Equals <see cref="TargetWidth"/> unless
    /// <see cref="WillUpscale"/> is true, in which case it equals the source width.
    /// </summary>
    public int AdjustedWidth { get; init; } = TargetWidth;

    /// <summary>
    /// The actual height to render. Equals <see cref="TargetHeight"/> unless
    /// <see cref="WillUpscale"/> is true, in which case it equals the source height.
    /// </summary>
    public int AdjustedHeight { get; init; } = TargetHeight;
}

/// <summary>
/// A captured region expressed in raw pixels. Both axes must be positive.
/// </summary>
public readonly record struct XCapturedRegion(int Width, int Height);

/// <summary>
/// Pure-function planner that maps a captured region to an X target profile.
///
/// Decision rules (from KFIP0010 / KFIP0019):
/// 1. If the source long axis exceeds 1600 px, downsample to the smallest
///    X-fit variant (16:9 / 2:1 / 1.91:1) whose aspect ratio is within
///    a tight tolerance of the source.
/// 2. If the source long axis is &lt;= 1600 px on both axes, do not upscale:
///    emit the target profile but flag WillUpscale = true so the renderer
///    can preserve original fidelity while still tagging the post slot.
/// 3. Aspect-ratio bucket selection:
///    - aspect &gt; 2.0        -> TwoUp2x1
///    - 1.85 &lt; aspect &lt;= 2.0 -> TwitterCard
///    - 1.30 &lt; aspect &lt;= 1.85 -> InFeed16x9 / ThreeUpLead / FourUpCell (all share 1200x675)
///    - aspect &lt;= 1.30      -> InFeed16x9 (still 16:9, X's preferred single-image slot)
/// </summary>
public static class XPreSoftenPlanner
{
    /// <summary>Aspect tolerance for matching source against target ratios.</summary>
    public const double AspectTolerance = 0.05;

    /// <summary>Long-axis ceiling; values &gt; this trigger downsample.</summary>
    public const int LongAxisCeiling = 1600;

    /// <summary>
    /// Plan the X-target dimensions for a captured region.
    /// </summary>
    /// <param name="region">The captured region, in pixels.</param>
    /// <returns>
    /// The resolved target size, profile, and whether the renderer should
    /// upscale (which it should not do per spec — caller is expected to
    /// honour the AdjustedWidth/AdjustedHeight when WillUpscale is true).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when region width or height is non-positive.
    /// </exception>
    public static XTargetSize Plan(XCapturedRegion region)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Height);

        double aspect = (double)region.Width / region.Height;
        XTargetProfile profile = PickProfile(aspect);
        var (width, height) = DimensionsFor(profile);

        // KFIP0020 review item N1: aspect ratio buckets above are exhaustive;
        // for tall portraits aspect < 1.0 we still pick InFeed16x9 (X's preferred
        // single slot) rather than upscaling. The caller will choose a wider
        // canvas if it wants a portrait, but per KFIP0010 §"Pre-softening
        // target", X will crop 4:5 to ~16:9 in feed preview anyway, so emit
        // 16:9 verbatim and let X crop.
        if (aspect < 1.0)
        {
            profile = XTargetProfile.InFeed16x9;
            (width, height) = DimensionsFor(profile);
        }

        bool willUpscale = region.Width <= width && region.Height <= height;
        if (willUpscale)
        {
            return new XTargetSize(width, height, profile, true)
            {
                AdjustedWidth = region.Width,
                AdjustedHeight = region.Height,
            };
        }

        long longAxis = Math.Max(region.Width, region.Height);
        if (longAxis <= LongAxisCeiling)
        {
            // Already small; render at source size but tag the profile slot.
            return new XTargetSize(region.Width, region.Height, profile, false);
        }

        // Downsample by scaling the source so its long axis matches the
        // profile's long axis. Both axes scale by the same factor so the
        // source aspect ratio is preserved on output.
        int longAxisSource = Math.Max(region.Width, region.Height);
        int longAxisTarget = Math.Max(width, height);
        double scale = (double)longAxisTarget / longAxisSource;
        int newWidth = (int)Math.Round(region.Width * scale);
        int newHeight = (int)Math.Round(region.Height * scale);
        return new XTargetSize(newWidth, newHeight, profile, false);
    }

    private static XTargetProfile PickProfile(double aspect) => aspect switch
    {
        > 2.0 => XTargetProfile.TwoUp2x1,
        > 1.85 => XTargetProfile.TwitterCard,
        > 1.30 => XTargetProfile.InFeed16x9,
        _ => XTargetProfile.InFeed16x9, // 4:5 -> X crops to 16:9 in feed preview anyway
    };

    private static (int Width, int Height) DimensionsFor(XTargetProfile profile) => profile switch
    {
        XTargetProfile.TwitterCard => (1200, 628),
        XTargetProfile.InFeed16x9 => (1200, 675),
        XTargetProfile.ThreeUpLead => (1200, 675),
        XTargetProfile.FourUpCell => (1200, 675),
        XTargetProfile.TwoUp2x1 => (1200, 600),
        _ => (1200, 675),
    };
}
