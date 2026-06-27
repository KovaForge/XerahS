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

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace XerahS.UI.Onboarding.Controls;

/// <summary>
/// Represents a single step item in the progress indicator.
/// </summary>
public class StepItem : AvaloniaObject
{
    public static readonly StyledProperty<int> StepIndexProperty =
        AvaloniaProperty.Register<StepItem, int>(nameof(StepIndex));

    public static readonly StyledProperty<int> StepNumberProperty =
        AvaloniaProperty.Register<StepItem, int>(nameof(StepNumber));

    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<StepItem, bool>(nameof(IsCurrent));

    public static readonly StyledProperty<bool> IsCompletedProperty =
        AvaloniaProperty.Register<StepItem, bool>(nameof(IsCompleted));

    public static readonly StyledProperty<bool> IsFutureProperty =
        AvaloniaProperty.Register<StepItem, bool>(nameof(IsFuture));

    public static readonly StyledProperty<bool> HasNextProperty =
        AvaloniaProperty.Register<StepItem, bool>(nameof(HasNext));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StepItem, string?>(nameof(Label));

    public int StepIndex
    {
        get => GetValue(StepIndexProperty);
        set => SetValue(StepIndexProperty, value);
    }

    public int StepNumber
    {
        get => GetValue(StepNumberProperty);
        set => SetValue(StepNumberProperty, value);
    }

    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    public bool IsCompleted
    {
        get => GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public bool IsFuture
    {
        get => GetValue(IsFutureProperty);
        set => SetValue(IsFutureProperty, value);
    }

    public bool HasNext
    {
        get => GetValue(HasNextProperty);
        set => SetValue(HasNextProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

/// <summary>
/// A row-of-dots progress indicator for the onboarding wizard.
/// Shows completed (checkmark), current (ring + pulse), and future (number) states.
/// </summary>
public partial class ProgressIndicator : UserControl
{
    public static readonly StyledProperty<int> CurrentStepIndexProperty =
        AvaloniaProperty.Register<ProgressIndicator, int>(nameof(CurrentStepIndex), 0);

    public static readonly StyledProperty<int> TotalStepsProperty =
        AvaloniaProperty.Register<ProgressIndicator, int>(nameof(TotalSteps), 5);

    public static readonly StyledProperty<ObservableCollection<StepItem>> StepItemsProperty =
        AvaloniaProperty.Register<ProgressIndicator, ObservableCollection<StepItem>>(
            nameof(StepItems), new ObservableCollection<StepItem>());

    public static readonly StyledProperty<string?> StepLabel1Property =
        AvaloniaProperty.Register<ProgressIndicator, string?>(nameof(StepLabel1), "Welcome");

    public static readonly StyledProperty<string?> StepLabel2Property =
        AvaloniaProperty.Register<ProgressIndicator, string?>(nameof(StepLabel2), "Save Location");

    public static readonly StyledProperty<string?> StepLabel3Property =
        AvaloniaProperty.Register<ProgressIndicator, string?>(nameof(StepLabel3), "Hotkeys");

    public static readonly StyledProperty<string?> StepLabel4Property =
        AvaloniaProperty.Register<ProgressIndicator, string?>(nameof(StepLabel4), "Upload");

    public static readonly StyledProperty<string?> StepLabel5Property =
        AvaloniaProperty.Register<ProgressIndicator, string?>(nameof(StepLabel5), "Done");

    public static readonly StyledProperty<string?> StepLabel6Property =
        AvaloniaProperty.Register<ProgressIndicator, string?>(nameof(StepLabel6), "Done");

    // Accent colors (light theme defaults)
    private static readonly SolidColorBrush AccentBrush = new(Color.Parse("#00B4A6"));
    private static readonly SolidColorBrush BorderColorBrush = new(Color.Parse("#E2E5EA"));
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush WhiteBrush = new(Colors.White);

    public int CurrentStepIndex
    {
        get => GetValue(CurrentStepIndexProperty);
        set => SetValue(CurrentStepIndexProperty, value);
    }

    public int TotalSteps
    {
        get => GetValue(TotalStepsProperty);
        set => SetValue(TotalStepsProperty, value);
    }

    public ObservableCollection<StepItem> StepItems
    {
        get => GetValue(StepItemsProperty);
        private set => SetValue(StepItemsProperty, value);
    }

    public string? StepLabel1
    {
        get => GetValue(StepLabel1Property);
        set => SetValue(StepLabel1Property, value);
    }

    public string? StepLabel2
    {
        get => GetValue(StepLabel2Property);
        set => SetValue(StepLabel2Property, value);
    }

    public string? StepLabel3
    {
        get => GetValue(StepLabel3Property);
        set => SetValue(StepLabel3Property, value);
    }

    public string? StepLabel4
    {
        get => GetValue(StepLabel4Property);
        set => SetValue(StepLabel4Property, value);
    }

    public string? StepLabel5
    {
        get => GetValue(StepLabel5Property);
        set => SetValue(StepLabel5Property, value);
    }

    public string? StepLabel6
    {
        get => GetValue(StepLabel6Property);
        set => SetValue(StepLabel6Property, value);
    }

    public ProgressIndicator()
    {
        InitializeComponent();
        DataContext = this;
        BuildStepItems();

        CurrentStepIndexProperty.Changed.AddClassHandler<ProgressIndicator>((_, _) => UpdateStepStates());
        TotalStepsProperty.Changed.AddClassHandler<ProgressIndicator>((indicator, _) => indicator.BuildStepItems());
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private string[] GetStepLabels()
    {
        return new[]
        {
            StepLabel1 ?? "Welcome",
            StepLabel2 ?? "Save Location",
            StepLabel3 ?? "Hotkeys",
            StepLabel4 ?? "Upload",
            StepLabel5 ?? "Done"
        };
    }

    private void BuildStepItems()
    {
        var labels = GetStepLabels();
        var items = new ObservableCollection<StepItem>();

        for (int i = 0; i < TotalSteps; i++)
        {
            items.Add(new StepItem
            {
                StepIndex = i,
                StepNumber = i + 1,
                Label = i < labels.Length ? labels[i] : null,
                HasNext = i < TotalSteps - 1
            });
        }

        StepItems = items;
        UpdateStepStates();
    }

    private void UpdateStepStates()
    {
        if (StepItems == null) return;

        for (int i = 0; i < StepItems.Count; i++)
        {
            var item = StepItems[i];
            item.IsCurrent = i == CurrentStepIndex;
            item.IsCompleted = i < CurrentStepIndex;
            item.IsFuture = i > CurrentStepIndex;
        }

        // Update visual elements after a layout pass
        Dispatcher.UIThread.Post(UpdateDotVisuals, DispatcherPriority.Normal);
    }

    private void UpdateDotVisuals()
    {
        if (StepItemsControl?.ItemsPanelRoot is not Panel itemsPanel) return;

        for (int i = 0; i < StepItems.Count; i++)
        {
            var item = StepItems[i];
            var container = StepItemsControl.ContainerFromIndex(i) as ContentPresenter;
            if (container == null) continue;

            // Find the ellipse and textblock
            var ellipse = container.FindDescendantOfType<Avalonia.Controls.Shapes.Ellipse>();
            var numberText = container.FindDescendantOfType<TextBlock>();
            var connector = container.FindDescendantOfType<Avalonia.Controls.Shapes.Rectangle>();

            if (ellipse == null || numberText == null) continue;

            // Remove old classes
            ellipse.Classes.Clear();
            ellipse.Classes.Add("WizardProgressDot");

            if (item.IsCompleted)
            {
                // Completed: filled accent circle with white checkmark hint
                ellipse.Stroke = AccentBrush;
                ellipse.StrokeThickness = 0;
                ellipse.Fill = AccentBrush;
                numberText.IsVisible = false;
            }
            else if (item.IsCurrent)
            {
                // Current: accent ring
                ellipse.Stroke = AccentBrush;
                ellipse.StrokeThickness = 2.5;
                ellipse.Fill = TransparentBrush;
                ellipse.Classes.Add("WizardProgressDotCurrent");
                numberText.IsVisible = false;
            }
            else
            {
                // Future: border-only circle with number
                ellipse.Stroke = BorderColorBrush;
                ellipse.StrokeThickness = 2;
                ellipse.Fill = TransparentBrush;
                ellipse.Classes.Add("WizardProgressDotFuture");
                numberText.IsVisible = true;
            }

            // Update connector line
            if (connector != null)
            {
                connector.Fill = item.IsCompleted ? AccentBrush : BorderColorBrush;
            }
        }
    }
}
