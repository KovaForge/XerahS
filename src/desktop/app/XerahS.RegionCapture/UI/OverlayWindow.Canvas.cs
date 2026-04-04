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

using Avalonia.Controls;
using Avalonia.Input;
using ShareX.ImageEditor.Core.Annotations;
using SkiaSharp;
using System;
using System.Collections.Generic;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.ViewModels;

namespace XerahS.RegionCapture.UI;

public partial class OverlayWindow
{
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

    #endregion

    #region Canvas Hit Testing

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

    #endregion
}
