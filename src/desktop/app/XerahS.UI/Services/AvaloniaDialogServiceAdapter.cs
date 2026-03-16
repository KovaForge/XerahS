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
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using XerahS.Services.Abstractions;

namespace XerahS.UI.Services;

/// <summary>
/// Avalonia implementation of the framework-agnostic <see cref="IDialogService"/>.
/// ViewModels depend only on the abstraction; this class lives in the UI layer.
/// </summary>
public sealed class AvaloniaDialogServiceAdapter : IDialogService
{
    public Task ShowMessageAsync(string title, string message)
    {
        return ShowSimpleDialogAsync(title, message, showCancel: false);
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        return await ShowSimpleDialogAsync(title, message, showCancel: true);
    }

    public Task ShowErrorAsync(string title, string error)
    {
        return ShowSimpleDialogAsync(title, error, showCancel: false, accentBrush: Brushes.Red);
    }

    public Task ShowWarningAsync(string title, string warning)
    {
        return ShowSimpleDialogAsync(title, warning, showCancel: false, accentBrush: Brushes.Orange);
    }

    public async Task<string?> ShowInputAsync(string title, string label, string? defaultValue = null)
    {
        string? result = null;

        var dialog = CreateDialog(title, 420, 200);
        var textBox = new TextBox { Text = defaultValue ?? "", Watermark = label };

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 14 });
        panel.Children.Add(textBox);

        var buttonRow = CreateButtonRow();
        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(20, 8), IsDefault = false };
        var okBtn = new Button { Content = "OK", Padding = new Thickness(20, 8), IsDefault = true };

        cancelBtn.Click += (_, _) => dialog.Close();
        okBtn.Click += (_, _) => { result = textBox.Text; dialog.Close(); };

        buttonRow.Children.Add(cancelBtn);
        buttonRow.Children.Add(okBtn);
        panel.Children.Add(buttonRow);
        dialog.Content = panel;

        await ShowDialogAsync(dialog);
        return result;
    }

    public Task<T?> ShowSelectionAsync<T>(string title, string label, IEnumerable<T> items) where T : class
    {
        // Minimal implementation — can be expanded with a proper ListBox-based picker
        return Task.FromResult<T?>(default);
    }

    private async Task<bool> ShowSimpleDialogAsync(string title, string message, bool showCancel, IBrush? accentBrush = null)
    {
        bool result = false;
        var dialog = CreateDialog(title, 420, 190);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            FontSize = 14
        };
        if (accentBrush != null)
        {
            messageBlock.Foreground = accentBrush;
        }
        panel.Children.Add(messageBlock);

        var buttonRow = CreateButtonRow();

        if (showCancel)
        {
            var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(20, 8), IsDefault = true };
            cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };
            buttonRow.Children.Add(cancelBtn);
        }

        var okBtn = new Button
        {
            Content = showCancel ? "OK" : "Close",
            Padding = new Thickness(20, 8),
            IsDefault = !showCancel
        };
        okBtn.Click += (_, _) => { result = true; dialog.Close(); };
        buttonRow.Children.Add(okBtn);

        panel.Children.Add(buttonRow);
        dialog.Content = panel;

        await ShowDialogAsync(dialog);
        return result;
    }

    private static Window CreateDialog(string title, double width, double height)
    {
        return new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
    }

    private static StackPanel CreateButtonRow()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
    }

    private static async Task ShowDialogAsync(Window dialog)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { } mainWindow)
        {
            await dialog.ShowDialog(mainWindow);
        }
        else
        {
            dialog.Show();
            var tcs = new TaskCompletionSource<bool>();
            dialog.Closed += (_, _) => tcs.TrySetResult(true);
            await tcs.Task;
        }
    }
}
