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

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.UI.Onboarding.Steps;

/// <summary>
/// Step 6: Completion and Summary view with animated checkmark.
/// </summary>
public partial class CompleteStepView : UserControl
{
    public CompleteStepView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlaySuccessAnimation();
    }

    private async void PlaySuccessAnimation()
    {
        // Small delay to let layout settle
        await Task.Delay(50);

        if (CheckMark == null || SuccessCircle == null)
            return;

        try
        {
            // Animate the success circle
            if (SuccessCircle is Ellipse circle)
            {
                var fadeAnim = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(400),
                    Delay = TimeSpan.FromMilliseconds(200),
                    FillMode = FillMode.Forward,
                    Easing = new CubicBezierEasing(0, 0, 0.25, 1),
                    Children =
                    {
                        new KeyFrame { Cue = Cue.Parse("0%"), Setters = { new Setter(Visual.OpacityProperty, 0.6) } },
                        new KeyFrame { Cue = Cue.Parse("100%"), Setters = { new Setter(Visual.OpacityProperty, 1.0) } }
                    }
                };
                _ = fadeAnim.RunAsync(circle);
            }

            // Animate the checkmark stroke (draw effect via opacity)
            if (CheckMark is Path path)
            {
                path.Opacity = 0;

                var drawAnim = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(600),
                    Delay = TimeSpan.FromMilliseconds(300),
                    FillMode = FillMode.Forward,
                    Easing = new CubicBezierEasing(0, 0, 0.25, 1),
                    Children =
                    {
                        new KeyFrame { Cue = Cue.Parse("0%"), Setters = { new Setter(Visual.OpacityProperty, 0.0) } },
                        new KeyFrame { Cue = Cue.Parse("100%"), Setters = { new Setter(Visual.OpacityProperty, 1.0) } }
                    }
                };
                _ = drawAnim.RunAsync(path);
            }

            // No bounce animation - kept simple
        }
        catch
        {
            // Animation failed silently - show checkmark immediately
            if (SuccessCircle is Ellipse c) c.Opacity = 1;
            if (CheckMark is Path p) p.Opacity = 1;
        }
    }
}
