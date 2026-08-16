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

using ShareX.ImageEditor.Core.ImageEffects;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.VideoEditor.Hosting;
using XerahS.Core;

namespace XerahS.UI.Services;

/// <summary>
/// Seeds VideoEditor watermark settings from the host image-effect preset
/// when the user already configured a text or image watermark there.
/// </summary>
public static class VideoEditorWatermarkMapper
{
    public static WatermarkSettings? FromDefaultTaskSettings()
    {
        return FromTaskSettings(SettingsManager.DefaultTaskSettings);
    }

    public static WatermarkSettings? FromTaskSettings(TaskSettings? settings)
    {
        return FromEffects(settings?.ImageSettings?.ImageEffectsPreset?.Effects);
    }

    public static WatermarkSettings? FromEffects(IEnumerable<ImageEffect>? effects)
    {
        if (effects == null)
        {
            return null;
        }

        WatermarkSettings? result = null;

        foreach (ImageEffect effect in effects)
        {
            switch (effect)
            {
                case TextWatermarkEffect text when !string.IsNullOrWhiteSpace(text.Text):
                    result ??= new WatermarkSettings();
                    result.Enabled = true;
                    result.Text = text.Text;
                    result.FontSize = text.FontSize > 0 ? (int)Math.Round(text.FontSize) : 24;
                    result.FontColor = ToHex(text.TextColor);
                    ApplyPlacement(result, text.Placement);
                    break;

                case DrawImageEffect image when !string.IsNullOrWhiteSpace(image.ImageLocation) && File.Exists(image.ImageLocation):
                    result ??= new WatermarkSettings();
                    result.Enabled = true;
                    result.ImagePath = image.ImageLocation;
                    result.Opacity = Math.Clamp(image.Opacity / 100.0, 0, 1);
                    ApplyPlacement(result, image.Placement);
                    break;
            }
        }

        return result;
    }

    internal static void ApplyPlacement(WatermarkSettings settings, DrawingPlacement placement)
    {
        (double x, double y) = placement switch
        {
            DrawingPlacement.TopLeft => (0.05, 0.05),
            DrawingPlacement.TopCenter => (0.5, 0.05),
            DrawingPlacement.TopRight => (0.95, 0.05),
            DrawingPlacement.MiddleLeft => (0.05, 0.5),
            DrawingPlacement.MiddleCenter => (0.5, 0.5),
            DrawingPlacement.MiddleRight => (0.95, 0.5),
            DrawingPlacement.BottomLeft => (0.05, 0.95),
            DrawingPlacement.BottomCenter => (0.5, 0.95),
            _ => (0.95, 0.95)
        };

        settings.PositionX = x;
        settings.PositionY = y;
    }

    private static string ToHex(SkiaSharp.SKColor color)
    {
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }
}
