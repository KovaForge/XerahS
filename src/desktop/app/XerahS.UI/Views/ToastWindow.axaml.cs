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

using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

/// <summary>
/// Toast notification window
/// </summary>
public partial class ToastWindow : OverlayWindow
{
    private ToastViewModel? _viewModel;
    private ToastConfig? _config;
    private bool _isDragging;
    private Avalonia.Point _dragStart;
    private PointerPressedEventArgs? _dragStartEventArgs;
    private Border? _urlOverlay;
    private Border? _flyoutHost;

    public ToastWindow()
    {
        InitializeComponent();

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _urlOverlay = this.FindControl<Border>("UrlOverlay");
        _flyoutHost = this.FindControl<Border>("FlyoutHost");
        if (_flyoutHost != null && _viewModel != null)
        {
            _flyoutHost.Tag = new ToastMenuContext(_viewModel);
            if (_flyoutHost.ContextFlyout is MenuFlyout menuFlyout)
            {
                menuFlyout.Opened += OnFlyoutOpened;
                menuFlyout.Closed += OnFlyoutClosed;
            }
        }
    }

    public void Initialize(ToastConfig config, IDesktopTaskManager? taskManager = null)
    {
        _config = config;

        // Set window size
        Width = config.Size.Width;
        Height = config.Size.Height;

        // Position window based on placement
        PositionWindow(config.Placement, config.Offset, config.Size);

        // Adjust position to use the screen that actually contains the window (for multi-monitor correctness).
        // On multi-monitor setups the primary screen working area may not match the screen the toast lands on.
        AdjustPositionToScreenBounds();

        // Create and bind ViewModel
        _viewModel = new ToastViewModel(config, taskManager);
        DataContext = _viewModel;

        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.OpacityChanged += OnOpacityChanged;
    }

    private void AdjustPositionToScreenBounds()
    {
        // Find the screen that contains the largest portion of this window
        var screen = Screens.ScreenFromPoint(new PixelPoint(Position.X + (int)(Width / 2), Position.Y + (int)(Height / 2)))
                     ?? Screens.ScreenFromPoint(new PixelPoint(Position.X, Position.Y))
                     ?? Screens.Primary;

        if (screen == null) return;

        var workingArea = screen.WorkingArea;
        var w = (int)Width;
        var h = (int)Height;

        // Clamp so the window stays within the screen's working area
        int x = Math.Max(workingArea.X, Math.Min(Position.X, workingArea.X + workingArea.Width - w));
        int y = Math.Max(workingArea.Y, Math.Min(Position.Y, workingArea.Y + workingArea.Height - h));

        Position = new PixelPoint(x, y);
    }

    private void PositionWindow(ContentPlacement placement, int offset, SizeI size)
    {
        // Get primary screen working area
        var screen = Screens.Primary;
        if (screen == null) return;

        var workingArea = screen.WorkingArea;
        double x = 0, y = 0;

        switch (placement)
        {
            case ContentPlacement.TopLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + offset;
                break;

            case ContentPlacement.TopCenter:
                x = workingArea.X + (workingArea.Width - size.Width) / 2;
                y = workingArea.Y + offset;
                break;

            case ContentPlacement.TopRight:
                x = workingArea.X + workingArea.Width - size.Width - offset;
                y = workingArea.Y + offset;
                break;

            case ContentPlacement.MiddleLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + (workingArea.Height - size.Height) / 2;
                break;

            case ContentPlacement.MiddleCenter:
                x = workingArea.X + (workingArea.Width - size.Width) / 2;
                y = workingArea.Y + (workingArea.Height - size.Height) / 2;
                break;

            case ContentPlacement.MiddleRight:
                x = workingArea.X + workingArea.Width - size.Width - offset;
                y = workingArea.Y + (workingArea.Height - size.Height) / 2;
                break;

            case ContentPlacement.BottomLeft:
                x = workingArea.X + offset;
                y = workingArea.Y + workingArea.Height - size.Height - offset;
                break;

            case ContentPlacement.BottomCenter:
                x = workingArea.X + (workingArea.Width - size.Width) / 2;
                y = workingArea.Y + workingArea.Height - size.Height - offset;
                break;

            case ContentPlacement.BottomRight:
            default:
                x = workingArea.X + workingArea.Width - size.Width - offset;
                y = workingArea.Y + workingArea.Height - size.Height - offset;
                break;
        }

        Position = new PixelPoint((int)x, (int)y);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _dragStart = point.Position;
            _isDragging = true;
            _dragStartEventArgs = e;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (_isDragging && TryGetClickAction(_dragStart, point.Position, point.Properties.PointerUpdateKind, out var action))
        {
            switch (action)
            {
                case ToastPointerAction.LeftClick:
                    _viewModel?.ExecuteLeftClick();
                    break;
                case ToastPointerAction.MiddleClick:
                    _viewModel?.ExecuteMiddleClick();
                    break;
                case ToastPointerAction.RightClick:
                    _viewModel?.ExecuteRightClick();
                    break;
            }
        }

        _isDragging = false;
        _dragStartEventArgs = null;
    }
    internal static bool TryGetClickAction(Avalonia.Point dragStart, Avalonia.Point releasePosition, PointerUpdateKind pointerUpdateKind, out ToastPointerAction action)
    {
        var distance = Math.Sqrt(
            Math.Pow(releasePosition.X - dragStart.X, 2) +
            Math.Pow(releasePosition.Y - dragStart.Y, 2));

        if (distance >= 20)
        {
            action = ToastPointerAction.None;
            return false;
        }

        action = pointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonReleased => ToastPointerAction.LeftClick,
            PointerUpdateKind.MiddleButtonReleased => ToastPointerAction.MiddleClick,
            PointerUpdateKind.RightButtonReleased => ToastPointerAction.RightClick,
            _ => ToastPointerAction.None
        };

        return action != ToastPointerAction.None;
    }

    internal enum ToastPointerAction
    {
        None,
        LeftClick,
        MiddleClick,
        RightClick
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _config == null) return;

        var point = e.GetCurrentPoint(this);
        var distance = Math.Sqrt(
            Math.Pow(point.Position.X - _dragStart.X, 2) +
            Math.Pow(point.Position.Y - _dragStart.Y, 2));

        // Start drag-and-drop if dragged far enough
        if (distance > 20 && !string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            _isDragging = false;

            var topLevel = TopLevel.GetTopLevel(this);
            var storageProvider = topLevel?.StorageProvider;
            if (storageProvider == null)
            {
                return;
            }

            var storageFile = await storageProvider.TryGetFileFromPathAsync(_config.FilePath);
            if (storageFile == null)
            {
                return;
            }

            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.CreateFile(storageFile));

            // Start drag operation
            if (_dragStartEventArgs != null)
            {
                await DragDrop.DoDragDropAsync(_dragStartEventArgs, dataTransfer, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _viewModel?.OnMouseEnter();

        // Show header overlay when there is a URL or a local file path fallback.
        if (_urlOverlay != null && _viewModel?.HasHeaderText == true)
        {
            _urlOverlay.Opacity = 1;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _viewModel?.OnMouseLeave();

        // Hide URL overlay
        if (_urlOverlay != null)
        {
            _urlOverlay.Opacity = 0;
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        if (sender is MenuFlyout)
        {
            _viewModel?.OnMenuOpened();
        }
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        if (sender is MenuFlyout)
        {
            _viewModel?.OnMenuClosed();
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnOpacityChanged(object? sender, double opacity)
    {
        Opacity = opacity;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_flyoutHost?.ContextFlyout is MenuFlyout menuFlyout)
        {
            menuFlyout.Opened -= OnFlyoutOpened;
            menuFlyout.Closed -= OnFlyoutClosed;
        }

        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.OpacityChanged -= OnOpacityChanged;
            _viewModel.Dispose();
        }

        base.OnClosed(e);
    }
}
