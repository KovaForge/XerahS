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
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Presentation.Rendering;
using ShareX.ImageEditor.Presentation.Theming;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using XerahS.RegionCapture.ViewModels;
using AvPixelRect = Avalonia.PixelRect;
using AvPixelPoint = Avalonia.PixelPoint;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.RegionCapture.UI;

/// <summary>
/// A transparent overlay window for a single monitor.
/// Each monitor gets its own overlay to avoid mixed-DPI scaling issues.
/// XIP-0023: Includes AnnotationToolbar for annotating during capture.
/// </summary>
public partial class OverlayWindow : Window
{
    private static readonly long SelectionDragRebuildIntervalTicks = Math.Max(1, Stopwatch.Frequency / 60);

    private readonly Models.MonitorInfo _monitor;
    private readonly TaskCompletionSource<RegionSelectionResult?> _completionSource;
    private readonly RegionCaptureControl _captureControl;
    private readonly RegionCaptureAnnotationViewModel _viewModel;
    private readonly SKBitmap? _backgroundBitmap;
    private Canvas? _annotationCanvas;

    // Annotation drawing state - delegates to EditorCore for lifecycle
    private Control? _currentShape;
    private bool _isDrawing;
    private Annotation? _currentAnnotation;
    private bool _rebuildScheduled;
    private bool _rebuildPending;
    private long _lastRebuildTicks;
    private bool _selectionInteractionActive;
    private bool _suppressInvalidateRequested;
    private readonly List<Control> _persistedAnnotationVisuals = new();

    // CTRL modifier state for toggling between drawing and region selection
    private bool _ctrlPressed;

    // Delayed focus retries to work around Linux/Wayland compositor not granting focus immediately (reduces "first pointer moved" delay)
    private static readonly int[] FocusRetryDelayMs = [50, 200, 500];
    private bool _windowClosed;

    public OverlayWindow()
    {
        // Design-time constructor
        _monitor = new Models.MonitorInfo("Design", new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1040), 1.0, true);
        _completionSource = new TaskCompletionSource<RegionSelectionResult?>();
        _captureControl = new RegionCaptureControl(_monitor);
        _viewModel = new RegionCaptureAnnotationViewModel();
        InitializeComponent();
        InitializeThemeScope();
        DataContext = _viewModel;
    }

    public OverlayWindow(
        Models.MonitorInfo monitor,
        TaskCompletionSource<RegionSelectionResult?> completionSource,
        Action<PixelRect>? selectionChanged = null,
        XerahS.Platform.Abstractions.CursorInfo? initialCursor = null,
        RegionCaptureOptions? options = null)
    {
        _monitor = monitor;
        _completionSource = completionSource;
        _backgroundBitmap = options?.BackgroundImage;

        // XIP-0023: Create ViewModel for annotation toolbar
        _viewModel = new RegionCaptureAnnotationViewModel();
        _viewModel.InvalidateRequested += OnInvalidateRequested;
        _viewModel.AnnotationsRestored += OnAnnotationsRestored;

        // Load saved editor options if available
        if (options?.EditorOptions != null)
        {
            _viewModel.LoadOptions(options.EditorOptions);
        }

        // Load a monitor-scoped background image into EditorCore at logical resolution
        // so annotation coordinates (from Avalonia pointer events) match image coordinates.
        if (_backgroundBitmap != null)
        {
            var editorBitmap = CreateMonitorLogicalBackgroundBitmap(_backgroundBitmap, monitor);
            if (editorBitmap != null)
            {
                _viewModel.LoadBackgroundImage(editorBitmap);
            }
        }

        // Wire up EditorCore events
        _viewModel.EditorCore.EditAnnotationRequested += OnEditAnnotationRequested;

        InitializeComponent();
        InitializeThemeScope();
        DataContext = _viewModel;

        bool isWindows = OperatingSystem.IsWindows();
        bool isLinux = OperatingSystem.IsLinux();
        bool isAvaloniaWayland = MonitorEnumerationService.IsAvaloniaWaylandBackend();
        var windowLayout = OverlayWindowLayoutCalculator.Calculate(monitor, isWindows, isLinux, isAvaloniaWayland);

        // Window origin and size do not share one coordinate space on every backend:
        // Windows and X11 use physical origin with logical size, while native Wayland/macOS use logical coordinates throughout.
        Position = new AvPixelPoint((int)windowLayout.Position.X, (int)windowLayout.Position.Y);
        Width = windowLayout.Width;
        Height = windowLayout.Height;

        DebugHelper.WriteLine($"[OverlayWindow] {monitor.DeviceName}: isWindows={isWindows} isAvaloniaWayland={isAvaloniaWayland} Position=({(int)windowLayout.Position.X},{(int)windowLayout.Position.Y}) Width={windowLayout.Width:F1} Height={windowLayout.Height:F1} PhysicalBounds=({monitor.PhysicalBounds.X:F1},{monitor.PhysicalBounds.Y:F1},{monitor.PhysicalBounds.Width:F1},{monitor.PhysicalBounds.Height:F1}) OverlayBounds=({monitor.OverlayBounds.X:F1},{monitor.OverlayBounds.Y:F1},{monitor.OverlayBounds.Width:F1},{monitor.OverlayBounds.Height:F1})");

        // Create and add the capture control
        _captureControl = new RegionCaptureControl(_monitor, options, initialCursor);
        if (selectionChanged is not null)
            _captureControl.SelectionChanged += selectionChanged;
        _captureControl.RegionSelected += OnRegionSelected;
        _captureControl.Cancelled += OnCancelled;

        var panel = this.FindControl<Panel>("RootPanel")!;
        panel.Children.Add(_captureControl);

        // XIP-0023: Wire up annotation canvas events
        _annotationCanvas = this.FindControl<Canvas>("AnnotationCanvas");
        if (_annotationCanvas != null)
        {
            _annotationCanvas.PointerPressed += OnAnnotationCanvasPointerPressed;
            _annotationCanvas.PointerMoved += OnAnnotationCanvasPointerMoved;
            _annotationCanvas.PointerReleased += OnAnnotationCanvasPointerReleased;
        }

        // Subscribe to ActiveTool changes to toggle canvas hit testing
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Ensure window can receive keyboard input
        Focusable = true;

        WireUpToolbarEvents();
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _windowClosed = true;
        base.OnClosed(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Focus the capture control so it receives keyboard and pointer events
        this.Focus();
        _captureControl.Focus();
        // On Linux/Wayland the compositor often grants focus with delay; retry focus a few times so pointer events (crosshair) start sooner
        ScheduleDelayedFocusRetries();

        // Diagnostic: log actual window geometry after opening to verify physical pixel sizing
        try
        {
            var topLeftPhysical = this.PointToScreen(new Avalonia.Point(0, 0));
            var bottomRightPhysical = this.PointToScreen(new Avalonia.Point(Width, Height));
            int physicalWindowW = bottomRightPhysical.X - topLeftPhysical.X;
            int physicalWindowH = bottomRightPhysical.Y - topLeftPhysical.Y;

            // Screen info at window position
            var screenAtWindow = Screens?.ScreenFromPoint(Position);
            string screenInfo = screenAtWindow != null
                ? $"ScreenAt={screenAtWindow.Bounds.Width}x{screenAtWindow.Bounds.Height} Scale={screenAtWindow.Scaling:F4} IsPrimary={screenAtWindow.IsPrimary}"
                : "ScreenAt=null";

            DebugHelper.WriteLine($"[OverlayWindow.OnOpened] {_monitor.DeviceName}: Logical=({Width:F1}x{Height:F1}) Position={Position} PhysicalTopLeft=({topLeftPhysical.X},{topLeftPhysical.Y}) PhysicalSize=({physicalWindowW}x{physicalWindowH}) MonitorPhysical=({_monitor.PhysicalBounds.Width:F0}x{_monitor.PhysicalBounds.Height:F0}) {screenInfo}");
            DebugHelper.WriteLine($"[OverlayWindow.OnOpened] {_monitor.DeviceName}: FillsMonitor={physicalWindowW >= (int)_monitor.PhysicalBounds.Width && physicalWindowH >= (int)_monitor.PhysicalBounds.Height} (physW={physicalWindowW} >= monW={(int)_monitor.PhysicalBounds.Width}, physH={physicalWindowH} >= monH={(int)_monitor.PhysicalBounds.Height})");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[OverlayWindow.OnOpened] {_monitor.DeviceName}: Diagnostic failed: {ex.Message}");
        }
    }

    private async void ScheduleDelayedFocusRetries()
    {
        foreach (int delayMs in FocusRetryDelayMs)
        {
            await Task.Delay(delayMs);
            if (_windowClosed)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                if (_windowClosed)
                    return;
                try
                {
                    this.Focus();
                    _captureControl.Focus();
                }
                catch
                {
                    // Window may be closing
                }
            }, DispatcherPriority.Input);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RegionCaptureAnnotationViewModel.ActiveTool))
        {
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Updates the AnnotationCanvas hit testing based on active tool and CTRL modifier.
    /// Select tool: hit testing OFF (allow RegionCaptureControl to handle mouse)
    /// Drawing tools + CTRL: hit testing OFF (CTRL allows region selection)
    /// Drawing tools (no CTRL): hit testing ON (canvas handles drawing)
    /// </summary>
    private bool UpdateAnnotationCanvasHitTesting()
    {
        if (_annotationCanvas == null) return false;

        // Annotation mode is active when:
        // 1. CTRL is NOT pressed (CTRL always allows region selection)
        // 2. Either a drawing tool is active, or Select is active with existing annotations
        //    so users can select/move/resize previously drawn annotations.
        bool hasAnnotations = _viewModel.EditorCore.Annotations.Count > 0;
        bool isAnnotationMode = !_ctrlPressed &&
                                (_viewModel.ActiveTool != EditorTool.Select || hasAnnotations);

        if (_annotationCanvas.IsHitTestVisible != isAnnotationMode)
        {
            _annotationCanvas.IsHitTestVisible = isAnnotationMode;
        }

        // Update the capture control's mode indicator
        if (_captureControl.IsAnnotationMode != isAnnotationMode)
        {
            _captureControl.IsAnnotationMode = isAnnotationMode;
            return true;
        }

        return false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeThemeScope()
    {
        RequestedThemeVariant = ThemeManager.GetCurrentTheme();
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, Avalonia.Styling.ThemeVariant theme)
    {
        Dispatcher.UIThread.Post(() => RequestedThemeVariant = theme);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // If inline text editing is active, let the TextBox handle keys
        if (_inlineTextBox != null)
        {
            if (e.Key == Key.Escape)
            {
                CancelInlineText();
                e.Handled = true;
            }
            return;
        }

        // Track CTRL key for toggling between drawing and region selection
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = true;
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }

        if (e.Key == Key.Escape)
        {
            OnCancelled();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // XIP-0023: Toggle annotation toolbar visibility
            ToggleAnnotationToolbar();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // XIP-0023: ENTER confirms capture with annotations
            ConfirmCaptureWithAnnotations();
            e.Handled = true;
        }
        // Tool shortcuts (only when no modifiers)
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.V: _viewModel.SelectToolCommand.Execute(EditorTool.Select); e.Handled = true; break;
                case Key.R: _viewModel.SelectToolCommand.Execute(EditorTool.Rectangle); e.Handled = true; break;
                case Key.E: _viewModel.SelectToolCommand.Execute(EditorTool.Ellipse); e.Handled = true; break;
                case Key.L: _viewModel.SelectToolCommand.Execute(EditorTool.Line); e.Handled = true; break;
                case Key.A: _viewModel.SelectToolCommand.Execute(EditorTool.Arrow); e.Handled = true; break;
                case Key.F: _viewModel.SelectToolCommand.Execute(EditorTool.Freehand); e.Handled = true; break;
                case Key.T: _viewModel.SelectToolCommand.Execute(EditorTool.Text); e.Handled = true; break;
                case Key.N: _viewModel.SelectToolCommand.Execute(EditorTool.Step); e.Handled = true; break;
                case Key.H: _viewModel.SelectToolCommand.Execute(EditorTool.Highlight); e.Handled = true; break;
                case Key.W: _viewModel.SelectToolCommand.Execute(EditorTool.SmartEraser); e.Handled = true; break;
                case Key.B: _viewModel.SelectToolCommand.Execute(EditorTool.Blur); e.Handled = true; break;
                case Key.P: _viewModel.SelectToolCommand.Execute(EditorTool.Pixelate); e.Handled = true; break;
                case Key.M: _viewModel.SelectToolCommand.Execute(EditorTool.Magnify); e.Handled = true; break;
                case Key.S: _viewModel.SelectToolCommand.Execute(EditorTool.Spotlight); e.Handled = true; break;
            }
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z)
            {
                _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Y)
            {
                _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // Track CTRL key release
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _ctrlPressed = false;
            if (UpdateAnnotationCanvasHitTesting())
            {
                _captureControl.InvalidateVisual();
            }
        }
    }

    #region Annotation Canvas Events

    private void OnAnnotationCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_annotationCanvas == null) return;

        // Commit any pending inline text edit. The click that commits text should not also
        // start a new annotation.
        if (_inlineTextBox != null)
        {
            CommitInlineText();
            e.Handled = true;
            return;
        }

        var point = e.GetPosition(_annotationCanvas);
        var props = e.GetCurrentPoint(_annotationCanvas).Properties;
        var skPoint = new SKPoint((float)point.X, (float)point.Y);

        // Right-click: delete annotation under cursor
        if (props.IsRightButtonPressed)
        {
            int annotationCountBeforeDelete = _viewModel.EditorCore.Annotations.Count;
            _viewModel.EditorCore.OnPointerPressed(skPoint, isRightButton: true);
            _selectionInteractionActive = false;
            SyncAnnotationState();
            if (_viewModel.EditorCore.Annotations.Count != annotationCountBeforeDelete)
            {
                RebuildAnnotationCanvas();
            }
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // Select tool still routes to EditorCore so existing annotations can be selected/moved/resized.
        if (_viewModel.ActiveTool == EditorTool.Select)
        {
            var selectedBefore = _viewModel.EditorCore.SelectedAnnotation;
            _viewModel.EditorCore.OnPointerPressed(skPoint);
            _selectionInteractionActive = true;
            SyncAnnotationState();
            if (!ReferenceEquals(selectedBefore, _viewModel.EditorCore.SelectedAnnotation))
            {
                RebuildAnnotationCanvas();
            }
            e.Pointer.Capture(_annotationCanvas);
            return;
        }

        if (_viewModel.ActiveTool == EditorTool.Spotlight &&
            TryBeginSpotlightSelectionInteraction(skPoint))
        {
            e.Pointer.Capture(_annotationCanvas);
            return;
        }

        // Clear any previous preview state before forwarding the new press to EditorCore.
        if (_currentShape != null)
        {
            _annotationCanvas.Children.Remove(_currentShape);
            _currentShape = null;
        }
        _currentAnnotation = null;
        _isDrawing = false;
        _selectionInteractionActive = false;

        // Delegate to EditorCore for annotation creation and initialization.
        int countBefore = _viewModel.EditorCore.Annotations.Count;
        _suppressInvalidateRequested = true;
        try
        {
            _viewModel.EditorCore.OnPointerPressed(skPoint);
        }
        finally
        {
            _suppressInvalidateRequested = false;
        }

        // Check if EditorCore created a new annotation
        if (_viewModel.EditorCore.Annotations.Count > countBefore)
        {
            // Discard any stale pending rebuild that could render a degenerate start-point artifact.
            _rebuildPending = false;

            _currentAnnotation = _viewModel.EditorCore.Annotations[_viewModel.EditorCore.Annotations.Count - 1];
            _isDrawing = true;

            ApplyToolbarDefaultsToAnnotation(_currentAnnotation);

            // Reuse the shared editor sampling path first, then fall back to monitor-specific sources.
            if (_currentAnnotation is SmartEraserAnnotation smartEraserAnn)
            {
                var sampledColor = ResolveSmartEraserColor(skPoint);
                if (!string.IsNullOrWhiteSpace(sampledColor))
                {
                    smartEraserAnn.StrokeColor = sampledColor;
                    smartEraserAnn.FillColor = sampledColor;
                }
            }

            // Create Avalonia preview shape for visual feedback during drawing
            _currentShape = CreatePreviewForAnnotation(_currentAnnotation);
            if (_currentShape != null)
            {
                _annotationCanvas.Children.Add(_currentShape);
            }
        }

        e.Pointer.Capture(_annotationCanvas);
    }

    private void OnAnnotationCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_annotationCanvas == null) return;

        // Match EditorCanvas behavior: forward move events while a button is pressed or while captured.
        var props = e.GetCurrentPoint(_annotationCanvas).Properties;
        if (e.Pointer.Captured != _annotationCanvas &&
            !props.IsLeftButtonPressed &&
            !props.IsRightButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(_annotationCanvas);
        var skPoint = new SKPoint((float)point.X, (float)point.Y);

        if (_isDrawing && _currentAnnotation != null)
        {
            // Keep draw-path updates lightweight and local to the active annotation preview.
            // This avoids expensive full-core invalidation work on every pointer move.
            UpdateCurrentDrawingAnnotation(skPoint);

            if (_currentShape != null)
            {
                UpdatePreviewFromAnnotation(_currentShape, _currentAnnotation);
            }
            return;
        }

        // Delegate to EditorCore for selection drag/resize interactions.
        _viewModel.EditorCore.OnPointerMoved(skPoint);
    }

    private void OnAnnotationCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_annotationCanvas == null) return;

        var endPoint = e.GetPosition(_annotationCanvas);
        var skPoint = new SKPoint((float)endPoint.X, (float)endPoint.Y);
        _selectionInteractionActive = false;

        if (e.Pointer.Captured == _annotationCanvas)
        {
            e.Pointer.Capture(null);
        }

        // Always forward release, even when not drawing, so EditorCore can end drag/resize state.
        _viewModel.EditorCore.OnPointerReleased(skPoint);

        // Remove preview shape if one was created for this draw operation.
        if (_isDrawing && _currentShape != null)
        {
            _annotationCanvas.Children.Remove(_currentShape);
            _currentShape = null;
        }
        _isDrawing = false;
        _currentAnnotation = null;

        // Rebuild canvas with finalized annotations (effects rendered, etc.)
        // Skip rebuild if inline text editing is about to start (EditAnnotationRequested handler will rebuild)
        if (_editingAnnotation == null)
        {
            RebuildAnnotationCanvas();
        }

        SyncAnnotationState();
    }

    private void UpdateCurrentDrawingAnnotation(SKPoint point)
    {
        if (_currentAnnotation == null)
        {
            return;
        }

        if (_currentAnnotation is FreehandAnnotation freehand)
        {
            freehand.Points.Add(point);
        }
        else if (_currentAnnotation is CutOutAnnotation cutOut)
        {
            float deltaX = Math.Abs(point.X - _currentAnnotation.StartPoint.X);
            float deltaY = Math.Abs(point.Y - _currentAnnotation.StartPoint.Y);
            cutOut.IsVertical = deltaX > deltaY;
            _currentAnnotation.EndPoint = point;
        }
        else
        {
            _currentAnnotation.EndPoint = point;
        }

        if (_currentAnnotation is SpotlightAnnotation spotlight)
        {
            spotlight.CanvasSize = new SKSize((float)Math.Max(1, Width), (float)Math.Max(1, Height));
        }
    }

    private bool TryBeginSpotlightSelectionInteraction(SKPoint point)
    {
        var editorCore = _viewModel.EditorCore;
        var selectedBefore = editorCore.SelectedAnnotation;
        SpotlightAnnotation? hitSpotlight = HitTestTopMostSpotlight(point);
        if (hitSpotlight == null)
        {
            if (selectedBefore is not null and not SpotlightAnnotation)
            {
                _suppressInvalidateRequested = true;
                try
                {
                    editorCore.Deselect();
                }
                finally
                {
                    _suppressInvalidateRequested = false;
                }

                SyncAnnotationState();
                RebuildAnnotationCanvas();
            }

            return false;
        }

        _selectionInteractionActive = true;
        _suppressInvalidateRequested = true;
        try
        {
            editorCore.Select(hitSpotlight);
            editorCore.OnPointerPressed(point);
        }
        finally
        {
            _suppressInvalidateRequested = false;
        }

        SyncAnnotationState();
        if (!ReferenceEquals(selectedBefore, editorCore.SelectedAnnotation))
        {
            RebuildAnnotationCanvas();
        }

        return true;
    }

    private SpotlightAnnotation? HitTestTopMostSpotlight(SKPoint point)
    {
        var annotations = _viewModel.EditorCore.Annotations;
        for (int i = annotations.Count - 1; i >= 0; i--)
        {
            if (annotations[i] is SpotlightAnnotation spotlight &&
                spotlight.HitTest(point))
            {
                return spotlight;
            }
        }

        return null;
    }

    /// <summary>
     /// Attempts to resolve SmartEraser color using a robust fallback chain:
    /// 1) Shared EditorCore snapshot sampling,
    /// 2) Full virtual-screen background bitmap with monitor mapping,
    /// 3) Editor source image,
    /// 4) Windows live-screen sampling (last resort).
    /// </summary>
    private string? ResolveSmartEraserColor(SKPoint logicalPoint)
    {
        string? sharedSample = _viewModel.EditorCore.SampleCanvasColor(logicalPoint);
        if (!string.IsNullOrWhiteSpace(sharedSample))
        {
            return sharedSample;
        }

        if (TrySampleVirtualBackgroundColor(logicalPoint, out string? virtualColor))
        {
            return virtualColor;
        }

        if (TrySampleBitmapColor(_viewModel.EditorCore.SourceImage, logicalPoint, out string? sourceColor))
        {
            return sourceColor;
        }

#if WINDOWS
        if (TrySampleLiveScreenColor(logicalPoint, out string? liveScreenColor))
        {
            return liveScreenColor;
        }
#endif

        return null;
    }

    private bool TrySampleVirtualBackgroundColor(SKPoint logicalPoint, out string? color)
    {
        color = null;
        if (_backgroundBitmap == null || _backgroundBitmap.Width <= 0 || _backgroundBitmap.Height <= 0)
        {
            return false;
        }

        int physX = (int)Math.Round(logicalPoint.X * _monitor.ScaleFactor);
        int physY = (int)Math.Round(logicalPoint.Y * _monitor.ScaleFactor);

        var coordService = new Services.CoordinateTranslationService();
        var virtualBounds = coordService.GetVirtualScreenBounds();
        int bmpX = (int)Math.Round(_monitor.PhysicalBounds.X - virtualBounds.X) + physX;
        int bmpY = (int)Math.Round(_monitor.PhysicalBounds.Y - virtualBounds.Y) + physY;
        bmpX = Math.Clamp(bmpX, 0, _backgroundBitmap.Width - 1);
        bmpY = Math.Clamp(bmpY, 0, _backgroundBitmap.Height - 1);

        var pixel = _backgroundBitmap.GetPixel(bmpX, bmpY);
        color = ToRgbHex(pixel);
        return true;
    }

#if WINDOWS
    private bool TrySampleLiveScreenColor(SKPoint logicalPoint, out string? color)
    {
        color = null;

        int physicalScreenX = (int)Math.Round(_monitor.PhysicalBounds.X + logicalPoint.X * _monitor.ScaleFactor);
        int physicalScreenY = (int)Math.Round(_monitor.PhysicalBounds.Y + logicalPoint.Y * _monitor.ScaleFactor);

        IntPtr hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            uint pixel = GetPixel(hdc, physicalScreenX, physicalScreenY);
            if (pixel == 0xFFFFFFFF)
            {
                return false;
            }

            byte r = (byte)(pixel & 0x000000FF);
            byte g = (byte)((pixel & 0x0000FF00) >> 8);
            byte b = (byte)((pixel & 0x00FF0000) >> 16);
            color = $"#{r:X2}{g:X2}{b:X2}";
            return true;
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, hdc);
        }
    }
#endif

    private static bool TrySampleBitmapColor(SKBitmap? bitmap, SKPoint logicalPoint, out string? color)
    {
        color = null;

        if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return false;
        }

        int x = Math.Clamp((int)Math.Round(logicalPoint.X), 0, bitmap.Width - 1);
        int y = Math.Clamp((int)Math.Round(logicalPoint.Y), 0, bitmap.Height - 1);
        var pixel = bitmap.GetPixel(x, y);
        color = ToRgbHex(pixel);
        return true;
    }

    private static string ToRgbHex(SKColor color)
    {
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

    private void ApplyToolbarDefaultsToAnnotation(Annotation annotation)
    {
        annotation.FillColor = _viewModel.FillColor;
        annotation.ShadowEnabled = _viewModel.ShadowEnabled;

        switch (annotation)
        {
            case TextAnnotation textAnnotation:
                textAnnotation.FontSize = _viewModel.FontSize;
                textAnnotation.TextColor = _viewModel.GetResolvedTextColor();
                textAnnotation.IsBold = _viewModel.TextBold;
                textAnnotation.IsItalic = _viewModel.TextItalic;
                textAnnotation.IsUnderline = _viewModel.TextUnderline;
                break;
            case NumberAnnotation numberAnnotation:
                numberAnnotation.FontSize = _viewModel.FontSize;
                numberAnnotation.FillColor = _viewModel.FillColor;
                numberAnnotation.TextColor = _viewModel.TextColor;
                break;
            case SpeechBalloonAnnotation speechBalloonAnnotation:
                speechBalloonAnnotation.FontSize = _viewModel.FontSize;
                speechBalloonAnnotation.FillColor = _viewModel.FillColor;
                speechBalloonAnnotation.TextColor = _viewModel.TextColor;
                speechBalloonAnnotation.CornerRadius = _viewModel.CornerRadius;
                break;
            case SmartEraserAnnotation smartEraserAnnotation:
                smartEraserAnnotation.StrokeWidth = 0;
                smartEraserAnnotation.ShadowEnabled = false;
                if (!string.IsNullOrWhiteSpace(smartEraserAnnotation.StrokeColor))
                {
                    smartEraserAnnotation.FillColor = smartEraserAnnotation.StrokeColor;
                }
                break;
            case RectangleAnnotation rectangleAnnotation when rectangleAnnotation is not SmartEraserAnnotation:
                rectangleAnnotation.CornerRadius = _viewModel.CornerRadius;
                break;
            case HighlightAnnotation highlightAnnotation:
                highlightAnnotation.FillColor = _viewModel.FillColor;
                break;
            case SpotlightAnnotation spotlightAnnotation:
                spotlightAnnotation.CanvasSize = new SKSize((float)Math.Max(1, Width), (float)Math.Max(1, Height));
                spotlightAnnotation.DarkenOpacity = _viewModel.GetSpotlightDarkenOpacity();
                break;
            case BaseEffectAnnotation effectAnnotation:
                effectAnnotation.Amount = _viewModel.EffectStrength;
                break;
        }
    }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
#endif

    /// <summary>
    /// Creates a lightweight Avalonia preview shape for visual feedback while drawing.
    /// </summary>
    private Control? CreatePreviewForAnnotation(Annotation annotation)
    {
        var shape = AnnotationVisualFactory.CreateVisualControl(annotation, AnnotationVisualMode.Preview);
        if (shape != null)
        {
            AnnotationVisualFactory.UpdateVisualControl(
                shape,
                annotation,
                AnnotationVisualMode.Preview,
                Width,
                Height);

            if (annotation is BaseEffectAnnotation)
            {
                AnnotationEffectVisualUpdater.UpdateEffectVisual(shape, _viewModel.EditorCore.SourceImage);
            }
        }
        return shape;
    }

    /// <summary>
    /// Updates the preview shape's position and geometry from the annotation's current state.
    /// </summary>
    private void UpdatePreviewFromAnnotation(Control shape, Annotation annotation)
    {
        AnnotationVisualFactory.UpdateVisualControl(
            shape,
            annotation,
            AnnotationVisualMode.Preview,
            Width,
            Height);

        if (annotation is BaseEffectAnnotation)
        {
            AnnotationEffectVisualUpdater.UpdateEffectVisual(shape, _viewModel.EditorCore.SourceImage);
        }
    }

    #endregion

    #region Event Handlers

    private void OnInvalidateRequested()
    {
        if (_suppressInvalidateRequested)
        {
            return;
        }

        // During active drawing we already update a lightweight preview shape in pointer handlers.
        // Rebuilding every annotation control on each move causes visible lag.
        if (_isDrawing && _currentShape != null)
        {
            return;
        }

        if (_selectionInteractionActive && ShouldThrottleSelectionRebuild())
        {
            return;
        }

        _rebuildPending = true;
        if (_rebuildScheduled)
        {
            return;
        }

        _rebuildScheduled = true;
        Dispatcher.UIThread.Post(ProcessPendingRebuild, DispatcherPriority.Render);
    }

    private void OnAnnotationsRestored()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RebuildAnnotationCanvas();
            SyncAnnotationState();
        });
    }

    private void ProcessPendingRebuild()
    {
        if (_rebuildPending)
        {
            _rebuildPending = false;
            RebuildAnnotationCanvas();
            SyncAnnotationState();
        }

        _rebuildScheduled = false;
        if (_rebuildPending)
        {
            _rebuildScheduled = true;
            Dispatcher.UIThread.Post(ProcessPendingRebuild, DispatcherPriority.Render);
        }
    }

    private void RebuildAnnotationCanvas()
    {
        if (_annotationCanvas == null) return;

        // Remove previous persisted visuals
        foreach (var visual in _persistedAnnotationVisuals)
        {
            _annotationCanvas.Children.Remove(visual);
        }
        _persistedAnnotationVisuals.Clear();

        var annotations = _viewModel.EditorCore.Annotations;
        if (annotations.Count > 0)
        {
            double canvasWidth = Width;
            double canvasHeight = Height;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            foreach (var annotation in annotations)
            {
                var visual = AnnotationVisualFactory.CreateVisualControl(
                    annotation, AnnotationVisualMode.Persisted);

                if (visual != null)
                {
                    visual.IsHitTestVisible = false;
                    AnnotationVisualFactory.UpdateVisualControl(
                        visual, annotation, AnnotationVisualMode.Persisted,
                        canvasWidth, canvasHeight);

                    if (annotation is BaseEffectAnnotation)
                    {
                        AnnotationEffectVisualUpdater.UpdateEffectVisual(visual, _viewModel.EditorCore.SourceImage);
                    }

                    _annotationCanvas.Children.Insert(0, visual);
                    _persistedAnnotationVisuals.Add(visual);
                }
            }
        }

        if (_inlineTextBox != null)
        {
            if (_annotationCanvas.Children.Contains(_inlineTextBox))
            {
                _annotationCanvas.Children.Remove(_inlineTextBox);
            }

            _annotationCanvas.Children.Add(_inlineTextBox);
        }

        _lastRebuildTicks = Stopwatch.GetTimestamp();
    }

    private bool ShouldThrottleSelectionRebuild()
    {
        if (_lastRebuildTicks == 0)
        {
            return false;
        }

        long elapsedTicks = Stopwatch.GetTimestamp() - _lastRebuildTicks;
        return elapsedTicks < SelectionDragRebuildIntervalTicks;
    }

    private void SyncAnnotationState()
    {
        bool hasAnnotations = _viewModel.EditorCore.Annotations.Count > 0;
        bool hasSelectedAnnotation = _viewModel.EditorCore.SelectedAnnotation != null;

        _viewModel.HasAnnotations = hasAnnotations;
        _viewModel.HasSelectedAnnotation = hasSelectedAnnotation;
        _viewModel.SelectedAnnotation = _viewModel.EditorCore.SelectedAnnotation;

        bool shouldInvalidateCapture = false;
        if (_captureControl.HasAnnotations != hasAnnotations)
        {
            _captureControl.HasAnnotations = hasAnnotations;
            shouldInvalidateCapture = true;
        }

        if (UpdateAnnotationCanvasHitTesting())
        {
            shouldInvalidateCapture = true;
        }

        if (shouldInvalidateCapture)
        {
            _captureControl.InvalidateVisual();
        }
    }

    /// <summary>
    /// Crops the full virtual-screen capture to this monitor and scales it to the monitor's logical size.
    /// This keeps effect tool sampling aligned with pointer coordinates on per-monitor overlays.
    /// </summary>
    private static SKBitmap? CreateMonitorLogicalBackgroundBitmap(SKBitmap fullBackground, Models.MonitorInfo monitor)
    {
        if (fullBackground.Width <= 0 || fullBackground.Height <= 0)
        {
            return null;
        }

        var coordinateService = new CoordinateTranslationService();
        var virtualBounds = coordinateService.GetVirtualScreenBounds();

        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: fullBitmap={fullBackground.Width}x{fullBackground.Height} virtualBounds=({virtualBounds.X:F0},{virtualBounds.Y:F0},{virtualBounds.Width:F0},{virtualBounds.Height:F0}) PhysicalBounds=({monitor.PhysicalBounds.X:F0},{monitor.PhysicalBounds.Y:F0},{monitor.PhysicalBounds.Width:F0},{monitor.PhysicalBounds.Height:F0}) Scale={monitor.ScaleFactor:F4}");

        int sourceX = (int)Math.Round(monitor.PhysicalBounds.X - virtualBounds.X);
        int sourceY = (int)Math.Round(monitor.PhysicalBounds.Y - virtualBounds.Y);
        int sourceWidth = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Width));
        int sourceHeight = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Height));

        var sourceRect = new SKRectI(sourceX, sourceY, sourceX + sourceWidth, sourceY + sourceHeight);
        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: physicalSourceRect=({sourceRect.Left},{sourceRect.Top},{sourceRect.Width}x{sourceRect.Height}) before clamp");
        sourceRect.Intersect(new SKRectI(0, 0, fullBackground.Width, fullBackground.Height));
        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: clampedSourceRect=({sourceRect.Left},{sourceRect.Top},{sourceRect.Width}x{sourceRect.Height}) valid={sourceRect.Width > 0 && sourceRect.Height > 0}");
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: sourceRect empty after clamp — returning null");
            return null;
        }

        var monitorBitmap = new SKBitmap(sourceRect.Width, sourceRect.Height, fullBackground.ColorType, fullBackground.AlphaType);
        if (!fullBackground.ExtractSubset(monitorBitmap, sourceRect))
        {
            using var subsetCanvas = new SKCanvas(monitorBitmap);
            subsetCanvas.DrawBitmap(
                fullBackground,
                sourceRect,
                new SKRect(0, 0, monitorBitmap.Width, monitorBitmap.Height));
        }

        int logicalWidth = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Width / monitor.ScaleFactor));
        int logicalHeight = Math.Max(1, (int)Math.Round(monitor.PhysicalBounds.Height / monitor.ScaleFactor));
        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: extracted={monitorBitmap.Width}x{monitorBitmap.Height} targetLogical={logicalWidth}x{logicalHeight}");
        if (monitorBitmap.Width == logicalWidth && monitorBitmap.Height == logicalHeight)
        {
            DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: no resize needed → {monitorBitmap.Width}x{monitorBitmap.Height}");
            return monitorBitmap;
        }

        var logicalBitmap = monitorBitmap.Resize(new SKImageInfo(logicalWidth, logicalHeight), SKFilterQuality.High);
        if (logicalBitmap != null)
        {
            DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: resized {monitorBitmap.Width}x{monitorBitmap.Height} → {logicalBitmap.Width}x{logicalBitmap.Height}");
            monitorBitmap.Dispose();
            return logicalBitmap;
        }

        DebugHelper.WriteLine($"[BackgroundBitmap] {monitor.DeviceName}: resize failed, returning extracted {monitorBitmap.Width}x{monitorBitmap.Height}");
        return monitorBitmap;
    }

    #endregion

    #region Capture Completion

    /// <summary>
    /// XIP-0023: Confirms capture with annotations using ENTER key.
    /// Uses the pending selection result if available, otherwise captures full monitor.
    /// </summary>
    private void ConfirmCaptureWithAnnotations()
    {
        // Save annotation options before completing
        _viewModel.SaveOptions();

        // Use the pending selection if user has made a region selection
        if (_pendingSelectionResult.HasValue)
        {
            var result = CreateResultWithAnnotations(_pendingSelectionResult.Value);
            _completionSource.TrySetResult(result);
            return;
        }

        // Fallback: Get the full monitor bounds if no selection was made
        var bounds = new PixelRect(0, 0, (int)_monitor.PhysicalBounds.Width, (int)_monitor.PhysicalBounds.Height);
        var cursorPos = new PixelPoint(bounds.Width / 2, bounds.Height / 2);
        var result2 = CreateResultWithAnnotations(new RegionSelectionResult(bounds, cursorPos));
        _completionSource.TrySetResult(result2);
    }

    private void OnRegionSelected(RegionSelectionResult result)
    {
        // If annotations have been drawn, don't auto-complete on region selection
        // User must press ENTER to confirm capture with annotations
        if (_viewModel.HasAnnotations || (_annotationCanvas?.Children.Count ?? 0) > 0)
        {
            // Store the selection result for later use when ENTER is pressed
            _pendingSelectionResult = result;

            // Update capture control to show the reminder
            _captureControl.HasPendingSelection = true;
            _captureControl.HasAnnotations = true;
            _captureControl.InvalidateVisual();
            return;
        }

        // Save annotation options before completing
        _viewModel.SaveOptions();

        _completionSource.TrySetResult(result);
    }

    /// <summary>
    /// Creates a RegionSelectionResult with the annotation layer rendered.
    /// </summary>
    private RegionSelectionResult CreateResultWithAnnotations(RegionSelectionResult baseResult)
    {
        // If no annotations, return the base result
        if (!_viewModel.HasAnnotations && (_annotationCanvas?.Children.Count ?? 0) == 0)
        {
            return baseResult;
        }

        // Render annotations to a transparent bitmap
        var annotationLayer = RenderAnnotationLayer();

        // Pass the monitor origin so the compositing code can adjust coordinates
        // (selection is in absolute screen coords, but annotation layer is monitor-relative)
        var monitorOrigin = new PixelPoint(
            (int)_monitor.PhysicalBounds.X,
            (int)_monitor.PhysicalBounds.Y);

        return new RegionSelectionResult(baseResult.Region, baseResult.CursorPosition, annotationLayer, monitorOrigin);
    }

    /// <summary>
    /// Renders all annotations to a transparent SKBitmap sized to the full monitor.
    /// The annotation layer can then be composited onto the captured image.
    /// </summary>
    private SKBitmap? RenderAnnotationLayer()
    {
        if (_annotationCanvas == null || _annotationCanvas.Children.Count == 0)
        {
            return null;
        }

        // Hide inline TextBox during capture so it doesn't render as a raw control
        bool textBoxWasVisible = _inlineTextBox?.IsVisible ?? false;
        if (_inlineTextBox != null) _inlineTextBox.IsVisible = false;

        try
        {
            // Physical pixel dimensions of the full monitor
            int width = (int)_monitor.PhysicalBounds.Width;
            int height = (int)_monitor.PhysicalBounds.Height;

            // Logical dimensions for layout (annotations are in logical coordinates)
            double logicalWidth = _monitor.PhysicalBounds.Width / _monitor.ScaleFactor;
            double logicalHeight = _monitor.PhysicalBounds.Height / _monitor.ScaleFactor;

            // Only force layout if the canvas isn't already at the expected size
            if (Math.Abs(_annotationCanvas.Bounds.Width - logicalWidth) > 1 ||
                Math.Abs(_annotationCanvas.Bounds.Height - logicalHeight) > 1)
            {
                _annotationCanvas.Measure(new Size(logicalWidth, logicalHeight));
                _annotationCanvas.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
            }

            // Render the Avalonia visual tree to a bitmap at physical resolution
            var dpi = 96.0 * _monitor.ScaleFactor;
            using var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(dpi, dpi));
            rtb.Render(_annotationCanvas);

            // Direct pixel copy from Avalonia RenderTargetBitmap to SKBitmap (avoids PNG encode/decode)
            var skBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var pixmap = skBitmap.PeekPixels();
            int rowBytes = skBitmap.Info.RowBytes;
            rtb.CopyPixels(new AvPixelRect(0, 0, width, height), pixmap.GetPixels(), rowBytes * height, rowBytes);

            return skBitmap;
        }
        finally
        {
            if (_inlineTextBox != null) _inlineTextBox.IsVisible = textBoxWasVisible;
        }
    }

    // Stores the selection result when annotations exist, for use with ENTER key
    private RegionSelectionResult? _pendingSelectionResult;

    private void OnCancelled()
    {
        // Save annotation options even when cancelled (user may have changed settings)
        _viewModel.SaveOptions();

        _completionSource.TrySetResult(null);
    }

    #endregion
}

/// <summary>
/// Extension method to convert SKColor to Avalonia Color.
/// </summary>
internal static class SKColorExtensions
{
    public static Color ToAvalonia(this SKColor color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }
}
