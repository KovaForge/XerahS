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

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Core.Abstractions;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Theming;
using SkiaSharp;

namespace XerahS.RegionCapture.ViewModels;

public partial class RegionCaptureAnnotationViewModel : ObservableObject, IAnnotationToolbarAdapter
{
    private const float MinEffectStrength = 1;
    private const float MaxBlurStrength = 200;
    private const float MaxPixelateStrength = 200;
    private const float MaxMagnifyStrength = 10;
    private const float MaxSpotlightStrength = 100;

    private readonly EditorCore _editorCore;
    private ImageEditorOptions _options = new();
    private bool _isLoadingToolOptions;
    private Annotation? _selectedAnnotation;
    private bool _canUndo;
    private bool _canRedo;
    private bool _hasSelectedAnnotation;
    private bool _hasAnnotations;

    public RegionCaptureAnnotationViewModel()
    {
        _editorCore = new EditorCore();
        _editorCore.HistoryChanged += OnHistoryChanged;
        _editorCore.AnnotationsRestored += OnAnnotationsRestored;
        _editorCore.InvalidateRequested += OnInvalidateRequested;
    }

    public EditorCore EditorCore => _editorCore;

    public bool ImageEditorMode => false;

    public event Action? InvalidateRequested;

    public event Action? AnnotationsRestored;

    public void LoadOptions(ImageEditorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _isLoadingToolOptions = true;
        try
        {
            if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
            {
                LoadSelectedAnnotationOptions(SelectedAnnotation);
            }
            else
            {
                LoadOptionsForTool(ActiveTool);
            }

            UpdateToolOptionsVisibility();
        }
        finally
        {
            _isLoadingToolOptions = false;
        }
    }

    public void SaveOptions()
    {
    }

    [ObservableProperty]
    private EditorTool _activeTool = EditorTool.Select;

    partial void OnActiveToolChanged(EditorTool value)
    {
        _editorCore.ActiveTool = value;

        _isLoadingToolOptions = true;
        try
        {
            OnPropertyChanged(nameof(EffectStrengthMaximum));

            if (value == EditorTool.Select && SelectedAnnotation != null)
            {
                LoadSelectedAnnotationOptions(SelectedAnnotation);
            }
            else
            {
                LoadOptionsForTool(value);
            }

            UpdateToolOptionsVisibility();
        }
        finally
        {
            _isLoadingToolOptions = false;
        }
    }

    [RelayCommand]
    private void SelectTool(EditorTool tool)
    {
        ActiveTool = tool;
    }

    public Annotation? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            if (!SetProperty(ref _selectedAnnotation, value))
            {
                return;
            }

            _isLoadingToolOptions = true;
            try
            {
                OnPropertyChanged(nameof(EffectStrengthMaximum));
                if (ActiveTool == EditorTool.Select && value != null)
                {
                    LoadSelectedAnnotationOptions(value);
                }

                UpdateToolOptionsVisibility();
            }
            finally
            {
                _isLoadingToolOptions = false;
            }
        }
    }

    [ObservableProperty]
    private string _selectedColor = "#FFEF4444";

    public string StrokeColor
    {
        get => SelectedColor;
        set => SelectedColor = value;
    }

    public IBrush SelectedColorBrush
    {
        get => new SolidColorBrush(HexToColor(SelectedColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                SelectedColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnSelectedColorChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedColorBrush));
        ApplyStrokeColor(value);
    }

    [ObservableProperty]
    private string _fillColor = "#00000000";

    public IBrush FillColorBrush
    {
        get => new SolidColorBrush(HexToColor(FillColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                FillColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnFillColorChanged(string value)
    {
        OnPropertyChanged(nameof(FillColorBrush));
        ApplyFillColor(value);
    }

    [ObservableProperty]
    private string _textColor = "#FFFAFAFA";

    public IBrush TextColorBrush
    {
        get => new SolidColorBrush(HexToColor(TextColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                TextColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnTextColorChanged(string value)
    {
        OnPropertyChanged(nameof(TextColorBrush));
        ApplyTextColor(value);
    }

    [ObservableProperty]
    private int _strokeWidth = 4;

    partial void OnStrokeWidthChanged(int value)
    {
        ApplyStrokeWidth(value);
    }

    [ObservableProperty]
    private int _cornerRadius = 4;

    partial void OnCornerRadiusChanged(int value)
    {
        int clamped = Math.Max(0, value);
        if (clamped != value)
        {
            CornerRadius = clamped;
            return;
        }

        ApplyCornerRadius(clamped);
    }

    [ObservableProperty]
    private float _fontSize = 48;

    partial void OnFontSizeChanged(float value)
    {
        ApplyFontSize(value);
    }

    [ObservableProperty]
    private float _effectStrength = 15;

    partial void OnEffectStrengthChanged(float value)
    {
        float clamped = Math.Clamp(value, MinEffectStrength, EffectStrengthMaximum);
        if (Math.Abs(clamped - value) > float.Epsilon)
        {
            EffectStrength = clamped;
            return;
        }

        ApplyEffectStrength(clamped);
    }

    [ObservableProperty]
    private bool _shadowEnabled;

    partial void OnShadowEnabledChanged(bool value)
    {
        ApplyShadowEnabled(value);
    }

    [ObservableProperty]
    private bool _textBold = true;

    partial void OnTextBoldChanged(bool value)
    {
        ApplyTextStyle(value, TextStyle.Bold);
    }

    [ObservableProperty]
    private bool _textItalic;

    partial void OnTextItalicChanged(bool value)
    {
        ApplyTextStyle(value, TextStyle.Italic);
    }

    [ObservableProperty]
    private bool _textUnderline;

    partial void OnTextUnderlineChanged(bool value)
    {
        ApplyTextStyle(value, TextStyle.Underline);
    }

    [ObservableProperty]
    private StepTailStyle _tailStyle = StepTailStyle.Triangle;

    [RelayCommand]
    private void ToggleShadow()
    {
        ShadowEnabled = !ShadowEnabled;
    }

    [RelayCommand]
    private void ToggleTextBold()
    {
        TextBold = !TextBold;
    }

    [RelayCommand]
    private void ToggleTextItalic()
    {
        TextItalic = !TextItalic;
    }

    [RelayCommand]
    private void ToggleTextUnderline()
    {
        TextUnderline = !TextUnderline;
    }

    public float EffectStrengthMaximum => GetMaxEffectStrength(GetEffectiveToolForOptions());

    public bool ShowBorderColor => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
            or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Text or EditorTool.Step => true,
        _ => false
    };

    public bool ShowFillColor => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.Highlight => true,
        _ => false
    };

    public bool ShowTextColor => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowThickness => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
            or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.Text => true,
        _ => false
    };

    public bool ShowFontSize => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.Step or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowCornerRadius => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowStrength => GetToolOptionsContext() switch
    {
        EditorTool.Blur or EditorTool.Pixelate or EditorTool.Magnify or EditorTool.Spotlight => true,
        _ => false
    };

    public bool ShowShadow => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
            or EditorTool.Freehand or EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowTextStyle => GetToolOptionsContext() switch
    {
        EditorTool.Text => true,
        _ => false
    };

    public bool ShowTailStyle => GetToolOptionsContext() switch
    {
        EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowToolOptionsSeparator =>
        ShowBorderColor ||
        ShowFillColor ||
        ShowTextColor ||
        ShowThickness ||
        ShowFontSize ||
        ShowCornerRadius ||
        ShowStrength ||
        ShowTextStyle ||
        ShowShadow ||
        ShowTailStyle;

    public bool ShowToolOptions => ShowToolOptionsSeparator;

    public string ActiveToolIcon => EditorIcons.ForTool(GetEffectiveDisplayTool());

    public string ActiveToolName => GetEffectiveDisplayTool() switch
    {
        EditorTool.Select => "Select",
        EditorTool.Rectangle => "Rectangle",
        EditorTool.Ellipse => "Ellipse",
        EditorTool.Line => "Line",
        EditorTool.Arrow => "Arrow",
        EditorTool.Freehand => "Freehand",
        EditorTool.Text => "Text",
        EditorTool.SpeechBalloon => "Speech Balloon",
        EditorTool.Step => "Step",
        EditorTool.Blur => "Blur",
        EditorTool.Pixelate => "Pixelate",
        EditorTool.Magnify => "Magnify",
        EditorTool.Spotlight => "Spotlight",
        EditorTool.SmartEraser => "Smart Eraser",
        EditorTool.Highlight => "Highlight",
        EditorTool.Crop => "Crop",
        EditorTool.CutOut => "Cut Out",
        _ => "Select"
    };

    public bool CanUndo
    {
        get => _canUndo;
        private set => SetProperty(ref _canUndo, value);
    }

    public bool CanRedo
    {
        get => _canRedo;
        private set => SetProperty(ref _canRedo, value);
    }

    public bool HasSelection => HasSelectedAnnotation;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_editorCore.CanUndo)
        {
            _editorCore.Undo();
            SelectedAnnotation = _editorCore.SelectedAnnotation;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_editorCore.CanRedo)
        {
            _editorCore.Redo();
            SelectedAnnotation = _editorCore.SelectedAnnotation;
        }
    }

    public bool HasSelectedAnnotation
    {
        get => _hasSelectedAnnotation;
        set
        {
            if (SetProperty(ref _hasSelectedAnnotation, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                DeleteSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasAnnotations
    {
        get => _hasAnnotations;
        set
        {
            if (SetProperty(ref _hasAnnotations, value))
            {
                ClearAnnotationsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
    private void DeleteSelected()
    {
        _editorCore.DeleteSelected();
        SelectedAnnotation = _editorCore.SelectedAnnotation;
        HasSelectedAnnotation = SelectedAnnotation != null;
        HasAnnotations = _editorCore.Annotations.Count > 0;
        RequestCanvasRefresh();
    }

    [RelayCommand(CanExecute = nameof(HasAnnotations))]
    private void ClearAnnotations()
    {
        _editorCore.ClearAll();
        SelectedAnnotation = null;
        HasAnnotations = false;
        HasSelectedAnnotation = false;
        RequestCanvasRefresh();
    }

    private void OnHistoryChanged()
    {
        CanUndo = _editorCore.CanUndo;
        CanRedo = _editorCore.CanRedo;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnAnnotationsRestored()
    {
        SelectedAnnotation = _editorCore.SelectedAnnotation;
        HasAnnotations = _editorCore.Annotations.Count > 0;
        HasSelectedAnnotation = SelectedAnnotation != null;
        AnnotationsRestored?.Invoke();
    }

    private void OnInvalidateRequested()
    {
        InvalidateRequested?.Invoke();
    }

    public void LoadBackgroundImage(SKBitmap bitmap)
    {
        _editorCore.LoadImage(bitmap);
    }

    public string GetResolvedTextColor()
    {
        if (IsTransparent(TextColor))
        {
            TextColor = ColorToHex(_options.TextTextColor);
        }

        return TextColor;
    }

    public byte GetSpotlightDarkenOpacity()
    {
        return ConvertSpotlightStrengthToOpacity(EffectStrength);
    }

    private void ApplyStrokeColor(string colorHex)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is not BaseEffectAnnotation &&
                SelectedAnnotation is not SmartEraserAnnotation &&
                SelectedAnnotation is not ImageAnnotation)
            {
                SelectedAnnotation.StrokeColor = colorHex;
                RequestCanvasRefresh();
            }

            return;
        }

        Color color = HexToColor(colorHex);
        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepBorderColor = color;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonBorderColor = color;
                break;
            case EditorTool.Text:
                _options.TextBorderColor = color;
                break;
            default:
                _options.BorderColor = color;
                break;
        }

        _editorCore.StrokeColor = colorHex;
    }

    private void ApplyFillColor(string colorHex)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case NumberAnnotation number:
                    number.FillColor = colorHex;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.FillColor = colorHex;
                    break;
                case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                    rectangle.FillColor = colorHex;
                    break;
                case EllipseAnnotation ellipse:
                    ellipse.FillColor = colorHex;
                    break;
                case HighlightAnnotation highlight:
                    highlight.FillColor = colorHex;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        Color color = HexToColor(colorHex);
        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepFillColor = color;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonFillColor = color;
                break;
            case EditorTool.Highlight:
                _options.HighlightFillColor = color;
                break;
            default:
                _options.FillColor = color;
                break;
        }
    }

    private void ApplyTextColor(string colorHex)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case NumberAnnotation number:
                    number.TextColor = colorHex;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.TextColor = colorHex;
                    break;
                case TextAnnotation text:
                    text.TextColor = colorHex;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        Color color = HexToColor(colorHex);
        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepTextColor = color;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonTextColor = color;
                break;
            case EditorTool.Text:
                _options.TextTextColor = color;
                break;
        }
    }

    private void ApplyStrokeWidth(int value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is SmartEraserAnnotation ||
                SelectedAnnotation is BaseEffectAnnotation ||
                SelectedAnnotation is SpotlightAnnotation)
            {
                return;
            }

            SelectedAnnotation.StrokeWidth = value;
            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepThickness = value;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonThickness = value;
                break;
            case EditorTool.Text:
                _options.TextThickness = value;
                break;
            default:
                _options.Thickness = value;
                break;
        }

        _editorCore.StrokeWidth = value;
    }

    private void ApplyCornerRadius(int value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                    rectangle.CornerRadius = value;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.CornerRadius = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool is EditorTool.Rectangle or EditorTool.SpeechBalloon)
        {
            _options.CornerRadius = value;
        }
    }

    private void ApplyFontSize(float value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case NumberAnnotation number:
                    number.FontSize = value;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.FontSize = value;
                    break;
                case TextAnnotation text:
                    text.FontSize = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepFontSize = value;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonFontSize = value;
                break;
            case EditorTool.Text:
                _options.TextFontSize = value;
                break;
        }
    }

    private void ApplyEffectStrength(float value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case SpotlightAnnotation spotlight:
                    spotlight.DarkenOpacity = ConvertSpotlightStrengthToOpacity(value);
                    RequestCanvasRefresh();
                    return;
                case BaseEffectAnnotation effect:
                    effect.Amount = value;
                    if (_editorCore.SourceImage != null)
                    {
                        effect.UpdateEffect(_editorCore.SourceImage);
                    }

                    RequestCanvasRefresh();
                    return;
                default:
                    return;
            }
        }

        switch (ActiveTool)
        {
            case EditorTool.Blur:
                _options.BlurStrength = value;
                break;
            case EditorTool.Pixelate:
                _options.PixelateStrength = value;
                break;
            case EditorTool.Magnify:
                _options.MagnifierStrength = value;
                break;
            case EditorTool.Spotlight:
                _options.SpotlightStrength = value;
                break;
        }
    }

    private void ApplyShadowEnabled(bool value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is not BaseEffectAnnotation &&
                SelectedAnnotation is not SmartEraserAnnotation &&
                SelectedAnnotation is not SpotlightAnnotation)
            {
                SelectedAnnotation.ShadowEnabled = value;
                RequestCanvasRefresh();
            }

            return;
        }

        _options.Shadow = value;
    }

    private void ApplyTextStyle(bool value, TextStyle style)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation is TextAnnotation selectedText)
        {
            switch (style)
            {
                case TextStyle.Bold:
                    selectedText.IsBold = value;
                    break;
                case TextStyle.Italic:
                    selectedText.IsItalic = value;
                    break;
                case TextStyle.Underline:
                    selectedText.IsUnderline = value;
                    break;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (style)
        {
            case TextStyle.Bold:
                _options.TextBold = value;
                break;
            case TextStyle.Italic:
                _options.TextItalic = value;
                break;
            case TextStyle.Underline:
                _options.TextUnderline = value;
                break;
        }
    }

    private void LoadOptionsForTool(EditorTool tool)
    {
        switch (tool)
        {
            case EditorTool.Rectangle:
            case EditorTool.Ellipse:
            case EditorTool.Line:
            case EditorTool.Arrow:
            case EditorTool.Freehand:
                SelectedColor = ColorToHex(_options.BorderColor);
                FillColor = ColorToHex(_options.FillColor);
                StrokeWidth = _options.Thickness;
                CornerRadius = _options.CornerRadius;
                ShadowEnabled = _options.Shadow;
                break;
            case EditorTool.Text:
                SelectedColor = ColorToHex(_options.TextBorderColor);
                TextColor = ColorToHex(_options.TextTextColor);
                StrokeWidth = _options.TextThickness;
                ShadowEnabled = _options.Shadow;
                FontSize = _options.TextFontSize;
                TextBold = _options.TextBold;
                TextItalic = _options.TextItalic;
                TextUnderline = _options.TextUnderline;
                break;
            case EditorTool.SpeechBalloon:
                SelectedColor = ColorToHex(_options.SpeechBalloonBorderColor);
                FillColor = ColorToHex(_options.SpeechBalloonFillColor);
                TextColor = ColorToHex(_options.SpeechBalloonTextColor);
                StrokeWidth = _options.SpeechBalloonThickness;
                CornerRadius = _options.CornerRadius;
                ShadowEnabled = _options.Shadow;
                FontSize = _options.SpeechBalloonFontSize;
                TextBold = _options.TextBold;
                TextItalic = _options.TextItalic;
                TextUnderline = _options.TextUnderline;
                break;
            case EditorTool.Step:
                SelectedColor = ColorToHex(_options.StepBorderColor);
                FillColor = ColorToHex(_options.StepFillColor);
                TextColor = ColorToHex(_options.StepTextColor);
                StrokeWidth = _options.StepThickness;
                ShadowEnabled = _options.Shadow;
                FontSize = _options.StepFontSize;
                TextBold = _options.TextBold;
                TextItalic = _options.TextItalic;
                TextUnderline = _options.TextUnderline;
                break;
            case EditorTool.Highlight:
                FillColor = ColorToHex(_options.HighlightFillColor);
                break;
            case EditorTool.Blur:
                EffectStrength = _options.BlurStrength;
                break;
            case EditorTool.Pixelate:
                EffectStrength = _options.PixelateStrength;
                break;
            case EditorTool.Magnify:
                EffectStrength = _options.MagnifierStrength;
                break;
            case EditorTool.Spotlight:
                EffectStrength = _options.SpotlightStrength;
                break;
        }
    }

    private void LoadSelectedAnnotationOptions(Annotation annotation)
    {
        if (annotation is not ImageAnnotation &&
            annotation is not BaseEffectAnnotation &&
            annotation is not SmartEraserAnnotation)
        {
            SelectedColor = annotation.StrokeColor;
            StrokeWidth = (int)annotation.StrokeWidth;
            ShadowEnabled = annotation.ShadowEnabled;
        }

        switch (annotation)
        {
            case NumberAnnotation number:
                FontSize = number.FontSize;
                FillColor = number.FillColor;
                if (!string.IsNullOrWhiteSpace(number.TextColor))
                {
                    TextColor = number.TextColor;
                }
                break;
            case TextAnnotation text:
                FontSize = text.FontSize;
                TextBold = text.IsBold;
                TextItalic = text.IsItalic;
                TextUnderline = text.IsUnderline;
                if (!string.IsNullOrWhiteSpace(text.TextColor))
                {
                    TextColor = text.TextColor;
                }
                break;
            case SpeechBalloonAnnotation balloon:
                FontSize = balloon.FontSize;
                FillColor = balloon.FillColor;
                CornerRadius = balloon.CornerRadius;
                if (!string.IsNullOrWhiteSpace(balloon.TextColor))
                {
                    TextColor = balloon.TextColor;
                }
                break;
            case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                FillColor = rectangle.FillColor;
                CornerRadius = rectangle.CornerRadius;
                break;
            case EllipseAnnotation ellipse:
                FillColor = ellipse.FillColor;
                break;
            case SpotlightAnnotation spotlight:
                EffectStrength = ConvertSpotlightOpacityToStrength(spotlight.DarkenOpacity);
                break;
            case BaseEffectAnnotation effect:
                EffectStrength = effect.Amount;
                if (effect is HighlightAnnotation highlight)
                {
                    FillColor = highlight.FillColor;
                }
                break;
        }
    }

    private void UpdateToolOptionsVisibility()
    {
        OnPropertyChanged(nameof(ShowBorderColor));
        OnPropertyChanged(nameof(ShowFillColor));
        OnPropertyChanged(nameof(ShowTextColor));
        OnPropertyChanged(nameof(ShowThickness));
        OnPropertyChanged(nameof(ShowFontSize));
        OnPropertyChanged(nameof(ShowCornerRadius));
        OnPropertyChanged(nameof(ShowStrength));
        OnPropertyChanged(nameof(ShowShadow));
        OnPropertyChanged(nameof(ShowTextStyle));
        OnPropertyChanged(nameof(ShowTailStyle));
        OnPropertyChanged(nameof(ShowToolOptionsSeparator));
        OnPropertyChanged(nameof(ActiveToolIcon));
        OnPropertyChanged(nameof(ActiveToolName));
        OnPropertyChanged(nameof(EffectStrengthMaximum));
    }

    private EditorTool? GetToolOptionsContext()
    {
        return ActiveTool == EditorTool.Select ? SelectedAnnotation?.ToolType : ActiveTool;
    }

    private EditorTool GetEffectiveToolForOptions()
    {
        return GetToolOptionsContext() ?? ActiveTool;
    }

    private EditorTool GetEffectiveDisplayTool()
    {
        return GetToolOptionsContext() ?? EditorTool.Select;
    }

    private void RequestCanvasRefresh()
    {
        InvalidateRequested?.Invoke();
    }

    private static float GetMaxEffectStrength(EditorTool tool) => tool switch
    {
        EditorTool.Blur => MaxBlurStrength,
        EditorTool.Pixelate => MaxPixelateStrength,
        EditorTool.Magnify => MaxMagnifyStrength,
        EditorTool.Spotlight => MaxSpotlightStrength,
        _ => 30
    };

    private static byte ConvertSpotlightStrengthToOpacity(float strength)
    {
        return (byte)Math.Clamp(strength / MaxSpotlightStrength * 255, 0, 255);
    }

    private static float ConvertSpotlightOpacityToStrength(byte opacity)
    {
        return opacity / 255f * MaxSpotlightStrength;
    }

    private static bool IsTransparent(string colorHex)
    {
        return HexToColor(colorHex).A == 0;
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color HexToColor(string hex)
    {
        return Color.TryParse(hex, out Color parsedColor) ? parsedColor : Colors.Transparent;
    }

    private enum TextStyle
    {
        Bold,
        Italic,
        Underline
    }

    void IAnnotationToolbarAdapter.SelectTool(EditorTool tool) => SelectToolCommand.Execute(tool);

    void IAnnotationToolbarAdapter.Undo() => UndoCommand.Execute(null);

    void IAnnotationToolbarAdapter.Redo() => RedoCommand.Execute(null);

    void IAnnotationToolbarAdapter.DeleteSelection() => DeleteSelectedCommand.Execute(null);

    void IAnnotationToolbarAdapter.ClearSelection() => ClearAnnotationsCommand.Execute(null);
}
