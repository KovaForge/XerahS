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
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Core.ImageEffects;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.ImageEditor.Core.ImageEffects.Filters;
using ShareX.ImageEditor.Core.ImageEffects.Helpers;
using ShareX.ImageEditor.Core.ImageEffects.Manipulations;
using ShareX.ImageEditor.Presentation.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using EditorImageHelpers = ShareX.ImageEditor.Core.ImageEffects.Helpers.ImageHelpers;
using XerahS.Common;
using XerahS.Common.Helpers;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.UI.Services;
using XerahS.UI.Views;

namespace XerahS.UI.ViewModels
{
    public partial class ImageEffectsViewModel : ViewModelBase
    {
        private TaskSettingsImage settings;
        private EditorCore editorCore;
        private SKBitmap? sourcePreviewBitmap;
        private const int PreviewSize = 256;
        private bool isSyncSuspended;
        private readonly IViewDialogService _dialogService;

        private bool canUndo;
        public bool CanUndo
        {
            get => canUndo;
            private set => SetProperty(ref canUndo, value);
        }

        private bool canRedo;
        public bool CanRedo
        {
            get => canRedo;
            private set => SetProperty(ref canRedo, value);
        }

        private string name = "New preset";
        public string Name
        {
            get => name;
            set
            {
                if (SetProperty(ref name, value))
                {
                    SyncToSettings();
                }
            }
        }

        public ObservableCollection<ImageEffect> Effects { get; private set; } = new ObservableCollection<ImageEffect>();

        private ImageEffect? selectedEffect;
        public ImageEffect? SelectedEffect
        {
            get => selectedEffect;
            set => SetProperty(ref selectedEffect, value);
        }

        public List<EffectCategory> AvailableEffects { get; private set; } = new();

        private Bitmap? previewBitmap;
        public Bitmap? PreviewBitmap
        {
            get => previewBitmap;
            private set => SetProperty(ref previewBitmap, value);
        }

        public ImageEffectsViewModel(TaskSettingsImage settings, EditorCore editorCore, IViewDialogService dialogService)
        {
            this.settings = settings;
            this.editorCore = editorCore;
            _dialogService = dialogService;

            InitializeAvailableEffects();
            GeneratePreviewImage();

            var preset = settings.ImageEffectsPreset ?? ImageEffectPreset.GetDefaultPreset();
            isSyncSuspended = true;
            try
            {
                Name = string.IsNullOrWhiteSpace(preset.Name) ? "Preset" : preset.Name;
                Effects.Clear();
                foreach (var effect in preset.Effects ?? new List<ImageEffect>())
                {
                    Effects.Add(effect);
                }
                SelectedEffect = Effects.FirstOrDefault();
            }
            finally
            {
                isSyncSuspended = false;
                SyncToSettings();
            }
            UpdatePreview();

            editorCore.HistoryChanged += OnHistoryChanged;
            Effects.CollectionChanged += (s, e) => SyncToSettings();
        }

        private void OnEffectsChanged()
        {
            isSyncSuspended = true;
            try
            {
                SyncFromCore();
            }
            finally
            {
                isSyncSuspended = false;
                SyncToSettings();
            }
            UpdatePreview();
        }

        private void OnHistoryChanged()
        {
            CanUndo = editorCore.CanUndo;
            CanRedo = editorCore.CanRedo;
        }

        private void SyncFromCore()
        {
            SelectedEffect = Effects.FirstOrDefault();
        }

        private void SyncToSettings()
        {
            if (isSyncSuspended)
                return;

            if (settings.ImageEffectsPreset == null)
            {
                settings.ImageEffectsPreset = new ImageEffectPreset();
            }

            var preset = settings.ImageEffectsPreset;
            preset.Name = Name;
            preset.Effects = Effects.ToList();
        }

        private void ApplyPreset(ImageEffectPreset preset, bool updatePreview)
        {
            var effects = preset.Effects ?? new List<ImageEffect>();
            isSyncSuspended = true;
            try
            {
                Name = string.IsNullOrWhiteSpace(preset.Name) ? "Preset" : preset.Name;
            }
            finally
            {
                isSyncSuspended = false;
            }
            Effects.Clear();
            foreach (var effect in effects)
            {
                Effects.Add(effect);
            }
            SelectedEffect = Effects.FirstOrDefault();
            SyncToSettings();
            if (updatePreview)
            {
                UpdatePreview();
            }
        }

        private void InitializeAvailableEffects()
        {
            var effectTypes = typeof(ImageEffect).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(ImageEffect).IsAssignableFrom(t))
                .Select(t =>
                {
                    var created = TryCreateEffectInstance(t, out var instance);
                    return new { Type = t, Instance = created ? instance : null };
                })
                .Where(x => x.Instance != null)
                .ToList();

            AvailableEffects = effectTypes
                .GroupBy(x => x.Instance!.Category)
                .OrderBy(x => x.Key)
                .Select(group => new EffectCategory(
                    group.Key.ToString(),
                    group.Select(x => new EffectType(x.Type, x.Instance!.Name))))
                .ToList();
        }

        private void GeneratePreviewImage()
        {
            sourcePreviewBitmap?.Dispose();
            sourcePreviewBitmap = null;

            try
            {
                sourcePreviewBitmap = new SKBitmap(PreviewSize, PreviewSize);
                using var canvas = new SKCanvas(sourcePreviewBitmap);

                using var bgPaint = new SKPaint { Color = SKColors.White };
                canvas.DrawRect(0, 0, PreviewSize, PreviewSize, bgPaint);

                using var paint = new SKPaint
                {
                    Color = new SKColor(70, 130, 180),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                // Draw an 'F' shape or similar asymmetric pattern
                float padding = PreviewSize * 0.2f;
                float width = PreviewSize * 0.6f;
                float height = PreviewSize * 0.6f;
                float thickness = width * 0.25f;

                // Vertical bar
                canvas.DrawRect(padding, padding, thickness, height, paint);
                // Top horizontal bar
                canvas.DrawRect(padding, padding, width, thickness, paint);
                // Middle horizontal bar
                canvas.DrawRect(padding, padding + height * 0.4f, width * 0.7f, thickness, paint);

                using var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 20,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left
                };
                canvas.DrawText("Preview", padding, PreviewSize - padding / 2, textPaint);
            }
            catch
            {
                // Ignore errors
            }
        }

        public void UpdatePreview()
        {
            if (sourcePreviewBitmap == null) return;

            SKBitmap result = sourcePreviewBitmap.Copy();

            try
            {
                foreach (var effect in Effects)
                {
                    var processed = effect.Apply(result);
                    if (processed != result)
                    {
                        result.Dispose();
                        result = processed;
                    }
                }

                // Convert SKBitmap to Avalonia Bitmap
                using var data = result.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = new MemoryStream();
                data.SaveTo(stream);
                stream.Position = 0;
                PreviewBitmap = new Bitmap(stream);
            }
            finally
            {
                result.Dispose();
            }
        }

        [RelayCommand]
        public void RefreshPreview()
        {
            UpdatePreview();
        }

        [RelayCommand]
        public void Undo()
        {
            editorCore.Undo();
        }

        [RelayCommand]
        public void Redo()
        {
            editorCore.Redo();
        }

        [RelayCommand]
        public void ToggleEffect(ImageEffect? effect)
        {
            if (effect == null) return;
            UpdatePreview();
            SyncToSettings();
        }

        [RelayCommand]
        public void AddEffect(Type effectType)
        {
            TryAddEffectType(effectType);
        }

        private bool TryAddEffectType(Type effectType)
        {
            if (TryCreateEffectInstance(effectType, out var effect) && effect != null)
            {
                Effects.Add(effect);
                SelectedEffect = Effects.LastOrDefault();
                UpdatePreview();
                SyncToSettings();
                return true;
            }

            return false;
        }

        private bool TryAddEffect(ImageEffect effect)
        {
            EnsurePreviewVisibleDefaults(effect);
            Effects.Add(effect);
            SelectedEffect = Effects.LastOrDefault();
            UpdatePreview();
            SyncToSettings();
            return true;
        }

        [RelayCommand]
        public async Task OpenEffectBrowserDialogAsync()
        {
            await _dialogService.ShowDialogAsync<ImageEffectsBrowserDialog>(this);
        }

        public bool TryAddEffectByBrowserId(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            if (BrowserEffectIdToTypeMap.TryGetValue(effectId, out var effectType))
            {
                return TryAddEffectType(effectType);
            }

            return false;
        }

        public bool TryAddRotate90ClockwiseEffect() => TryAddEffect(RotateImageEffect.Clockwise90);

        public bool TryAddRotate90CounterClockwiseEffect() => TryAddEffect(RotateImageEffect.CounterClockwise90);

        public bool TryAddRotate180Effect() => TryAddEffect(RotateImageEffect.Rotate180);

        public bool TryAddRotateCustomEffect() => TryAddEffect(RotateImageEffect.Custom(0f));

        public bool TryAddFlipHorizontalEffect() => TryAddEffect(FlipImageEffect.Horizontal);

        public bool TryAddFlipVerticalEffect() => TryAddEffect(FlipImageEffect.Vertical);

        private static readonly Dictionary<string, Type> BrowserEffectIdToTypeMap = CreateBrowserEffectIdToTypeMap();

        private static Dictionary<string, Type> CreateBrowserEffectIdToTypeMap()
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in typeof(ImageEffect).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(ImageEffect).IsAssignableFrom(t)))
            {
                AddBrowserEffectTypeAlias(map, NormalizeBrowserEffectId(type.Name), type);
                AddBrowserEffectTypeAlias(map, NormalizeBrowserEffectId(RemoveEffectTypeSuffix(type.Name)), type);

                if (TryCreateEffectInstance(type, out var effect) && effect != null)
                {
                    AddBrowserEffectTypeAlias(map, NormalizeBrowserEffectId(effect.Name), type);
                }
            }

            // Browser IDs that intentionally point to shared effect implementations.
            AddBrowserEffectTypeAlias(map, "rotate", typeof(RotateImageEffect));
            AddBrowserEffectTypeAlias(map, "auto_crop_image", typeof(AutoCropImageEffect));
            AddBrowserEffectTypeAlias(map, "resize_image", typeof(ResizeImageEffect));
            AddBrowserEffectTypeAlias(map, "flip", typeof(FlipImageEffect));
            AddBrowserEffectTypeAlias(map, "draw_background", typeof(DrawBackgroundEffect));
            AddBrowserEffectTypeAlias(map, "draw_background_image", typeof(DrawBackgroundImageEffect));
            AddBrowserEffectTypeAlias(map, "draw_checkerboard", typeof(DrawCheckerboardEffect));
            AddBrowserEffectTypeAlias(map, "draw_image", typeof(DrawImageEffect));
            AddBrowserEffectTypeAlias(map, "draw_particles", typeof(DrawParticlesEffect));
            AddBrowserEffectTypeAlias(map, "draw_text", typeof(DrawTextEffect));

            // Browser-only actions not represented by task preset effects.
            map.Remove("crop_image");
            map.Remove("resize_canvas");

            return map;
        }

        private static void AddBrowserEffectTypeAlias(Dictionary<string, Type> map, string id, Type type)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            map[id] = type;
        }

        private static string RemoveEffectTypeSuffix(string value)
        {
            if (value.EndsWith("ImageEffect", StringComparison.Ordinal))
            {
                return value[..^"ImageEffect".Length];
            }

            if (value.EndsWith("Effect", StringComparison.Ordinal))
            {
                return value[..^"Effect".Length];
            }

            return value;
        }

        private static string NormalizeBrowserEffectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return EffectItem.NormalizeEffectId(value);
        }

        private void EnsurePreviewVisibleDefaults(ImageEffect effect)
        {
            if (sourcePreviewBitmap == null)
            {
                return;
            }

            if (HasPreviewImpact(effect))
            {
                return;
            }

            ApplyVisibleDefaultsHeuristic(effect);
        }

        private bool HasPreviewImpact(ImageEffect effect)
        {
            if (sourcePreviewBitmap == null)
            {
                return true;
            }

            using var source = sourcePreviewBitmap.Copy();
            SKBitmap? processed = null;

            try
            {
                processed = effect.Apply(source);
                return !AreBitmapsEqual(source, processed);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (processed != null && !ReferenceEquals(processed, source))
                {
                    processed.Dispose();
                }
            }
        }

        private static bool AreBitmapsEqual(SKBitmap left, SKBitmap right)
        {
            if (left.Width != right.Width || left.Height != right.Height)
            {
                return false;
            }

            for (int y = 0; y < left.Height; y++)
            {
                for (int x = 0; x < left.Width; x++)
                {
                    if (left.GetPixel(x, y) != right.GetPixel(x, y))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ApplyVisibleDefaultsHeuristic(ImageEffect effect)
        {
            var properties = effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            foreach (var property in properties)
            {
                try
                {
                    object? value = property.GetValue(effect);
                    var propertyType = property.PropertyType;
                    string name = property.Name;

                    if (propertyType == typeof(bool) && value is bool boolValue && !boolValue)
                    {
                        if (IsToggleLikeProperty(name))
                        {
                            property.SetValue(effect, true);
                        }

                        continue;
                    }

                    if (propertyType.IsEnum)
                    {
                        var current = value != null ? Convert.ToInt64(value) : 0;
                        if (current == 0)
                        {
                            var enumValues = Enum.GetValues(propertyType);
                            foreach (var enumValue in enumValues)
                            {
                                if (Convert.ToInt64(enumValue) != 0)
                                {
                                    property.SetValue(effect, enumValue);
                                    break;
                                }
                            }
                        }

                        continue;
                    }

                    if (propertyType == typeof(SKColor) && value is SKColor color && color.Alpha == 0)
                    {
                        property.SetValue(effect, SKColors.Black);
                        continue;
                    }

                    if (IsNumericType(propertyType))
                    {
                        double numeric = value != null ? Convert.ToDouble(value) : 0d;
                        if (Math.Abs(numeric) > double.Epsilon)
                        {
                            continue;
                        }

                        double fallback = GetNumericFallbackForProperty(name);
                        object converted = Convert.ChangeType(fallback, propertyType);
                        property.SetValue(effect, converted);
                    }
                }
                catch
                {
                    // Ignore per-property conversion issues and keep best-effort defaults.
                }
            }
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        private static bool IsToggleLikeProperty(string propertyName)
        {
            return propertyName.Contains("Top", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Right", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Bottom", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Left", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Horizontal", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Vertical", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Curved", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Outline", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Enabled", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Contains("Enable", StringComparison.OrdinalIgnoreCase);
        }

        private static double GetNumericFallbackForProperty(string propertyName)
        {
            if (propertyName.Contains("Angle", StringComparison.OrdinalIgnoreCase))
                return 15;
            if (propertyName.Contains("Opacity", StringComparison.OrdinalIgnoreCase))
                return 60;
            if (propertyName.Contains("Strength", StringComparison.OrdinalIgnoreCase))
                return 60;
            if (propertyName.Contains("Intensity", StringComparison.OrdinalIgnoreCase))
                return 60;
            if (propertyName.Contains("Threshold", StringComparison.OrdinalIgnoreCase))
                return 50;
            if (propertyName.Contains("Radius", StringComparison.OrdinalIgnoreCase))
                return 8;
            if (propertyName.Contains("Size", StringComparison.OrdinalIgnoreCase))
                return 8;
            if (propertyName.Contains("Depth", StringComparison.OrdinalIgnoreCase))
                return 8;
            if (propertyName.Contains("Range", StringComparison.OrdinalIgnoreCase))
                return 24;
            if (propertyName.Contains("Offset", StringComparison.OrdinalIgnoreCase))
                return 8;
            if (propertyName.Contains("Width", StringComparison.OrdinalIgnoreCase))
                return 64;
            if (propertyName.Contains("Height", StringComparison.OrdinalIgnoreCase))
                return 64;
            if (propertyName.Contains("Percentage", StringComparison.OrdinalIgnoreCase))
                return 25;
            if (propertyName.Contains("Alpha", StringComparison.OrdinalIgnoreCase))
                return 80;
            if (propertyName.Contains("Scale", StringComparison.OrdinalIgnoreCase))
                return 4;

            return 1;
        }

        private static bool TryCreateEffectInstance(Type effectType, out ImageEffect? effect)
        {
            effect = null;

            try
            {
                if (Activator.CreateInstance(effectType) is ImageEffect defaultEffect)
                {
                    effect = defaultEffect;
                    return true;
                }
            }
            catch
            {
                // Fall back to known effect constructors without parameterless overloads.
            }

            if (effectType == typeof(RotateImageEffect))
            {
                effect = RotateImageEffect.Custom(0f);
                return true;
            }

            if (effectType == typeof(TornEdgeImageEffect))
            {
                effect = new TornEdgeImageEffect(8, 24, top: true, right: true, bottom: true, left: true, curved: true);
                return true;
            }

            if (effectType == typeof(ReflectionImageEffect))
            {
                effect = new ReflectionImageEffect(25, 80, 0, 0, skew: false, skewSize: 0);
                return true;
            }

            if (effectType == typeof(ShadowImageEffect))
            {
                effect = new ShadowImageEffect(50, 8, SKColors.Black, 0, 0, autoResize: true);
                return true;
            }

            if (effectType == typeof(SliceImageEffect))
            {
                effect = new SliceImageEffect(8, 24, 4, 16);
                return true;
            }

            if (effectType == typeof(OutlineImageEffect))
            {
                effect = new OutlineImageEffect(2, 0, outlineOnly: false, SKColors.Black);
                return true;
            }

            if (effectType == typeof(GlowImageEffect))
            {
                effect = new GlowImageEffect(6, 60, SKColors.White, 0, 0, autoResize: true);
                return true;
            }

            if (effectType == typeof(BorderImageEffect))
            {
                effect = new BorderImageEffect(EditorImageHelpers.BorderType.Outside, 2, EditorImageHelpers.DashStyle.Solid, SKColors.Black);
                return true;
            }

            return false;
        }

        [RelayCommand]
        public void RemoveEffect()
        {
            if (SelectedEffect != null)
            {
                Effects.Remove(SelectedEffect);
                SelectedEffect = Effects.FirstOrDefault();
                UpdatePreview();
                SyncToSettings();
            }
        }

        [RelayCommand]
        public async Task SavePresetAsync()
        {
            if (Effects == null)
                return;

            string suggestedFileName = string.IsNullOrWhiteSpace(Name) ? "Preset.xsie" : $"{Name}.xsie";
            var filters = new[] { "*.xsie", "*.sxie" };
            
            var filePath = await _dialogService.ShowSaveFilePickerAsync("Save Image Effects Preset", suggestedFileName, "xsie", filters);
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            var presetToSave = new ImageEffectPreset
            {
                Name = Name,
                Effects = Effects.ToList()
            };

            try
            {
                if (extension == ".sxie")
                {
                    var result = LegacyImageEffectExporter.ExportSxieFile(filePath, presetToSave.Name, presetToSave.Effects);
                    if (!result.Success)
                    {
                        DebugHelper.WriteLine($"[ImageEffects] Legacy export failed: {result.ErrorMessage}");
                    }
                }
                else
                {
                    if (extension != ".xsie")
                    {
                        filePath = $"{filePath}.xsie";
                    }

                    ImageEffectPresetSerializer.SaveXsieFile(filePath, presetToSave);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to save image effects preset.");
            }
        }

        [RelayCommand]
        public async Task ImportEffectsAsync()
        {
            var preset = await LoadPresetFromPickerAsync("Import Image Effects");
            if (preset == null)
                return;

            ApplyPreset(preset, updatePreview: true);
        }

        private ImageEffectPreset? LoadLegacyPreset(string filePath)
        {
            var importResult = LegacyImageEffectImporter.ImportSxieFile(filePath);
            if (importResult == null || !importResult.Success)
            {
                DebugHelper.WriteLine($"[ImageEffects] Legacy import failed: {importResult?.ErrorMessage}");
                return null;
            }

            var preset = new ImageEffectPreset
            {
                Name = importResult.PresetName ?? "Imported Preset",
                MappedEffects = importResult.MappedEffects.Select(mapped => new MappedEffectData
                {
                    TargetTypeName = mapped.TargetTypeName,
                    Properties = mapped.Properties
                }).ToList()
            };

            foreach (var mapped in importResult.MappedEffects)
            {
                var effect = CreateEffectFromMapped(mapped);
                if (effect != null)
                {
                    preset.Effects.Add(effect);
                }
            }

            return preset;
        }

        private async Task<ImageEffectPreset?> LoadPresetFromPickerAsync(string title)
        {
            var filters = new[] { "*.xsie", "*.sxie" };
            var filePath = await _dialogService.ShowFilePickerAsync(title, filters);
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                var preset = extension == ".sxie"
                    ? LoadSxiePreset(filePath)
                    : ImageEffectPresetSerializer.LoadXsieFile(filePath);

                if (preset != null)
                {
                    preset.Name = Path.GetFileNameWithoutExtension(filePath);
                }

                return preset;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load image effects preset.");
                return null;
            }
        }

        private ImageEffectPreset? LoadSxiePreset(string filePath)
        {
            try
            {
                var preset = ImageEffectPresetSerializer.LoadXsieFile(filePath);
                if (preset != null)
                {
                    return preset;
                }
            }
            catch
            {
                // Legacy .sxie files may use ShareX.ImageEffectsLib schema.
            }

            return LoadLegacyPreset(filePath);
        }



        private static ImageEffect? CreateEffectFromMapped(MappedEffect mapped)
        {
            if (string.IsNullOrWhiteSpace(mapped.TargetTypeName))
                return null;

            if (mapped.TargetTypeName == nameof(RotateImageEffect))
            {
                if (mapped.Properties.TryGetValue("Angle", out var angleValue))
                {
                    var angle = ReadSingle(angleValue, 0f);
                    return RotateImageEffect.Custom(angle);
                }
            }

            if (mapped.TargetTypeName == nameof(FlipImageEffect))
            {
                bool horizontal = mapped.Properties.TryGetValue("Horizontal", out var horizontalValue) && Convert.ToBoolean(horizontalValue);
                bool vertical = mapped.Properties.TryGetValue("Vertical", out var verticalValue) && Convert.ToBoolean(verticalValue);

                if (vertical && !horizontal)
                    return FlipImageEffect.Vertical;

                return FlipImageEffect.Horizontal;
            }

            if (mapped.TargetTypeName == nameof(ResizeImageEffect))
            {
                int width = mapped.Properties.TryGetValue("_width", out var widthValue) ? ReadInt(widthValue, 0) : 0;
                int height = mapped.Properties.TryGetValue("_height", out var heightValue) ? ReadInt(heightValue, 0) : 0;
                return new ResizeImageEffect(width, height);
            }

            var assembly = typeof(ImageEffect).Assembly;
            var type = assembly.GetTypes().FirstOrDefault(t => t.Name.Equals(mapped.TargetTypeName, StringComparison.Ordinal));
            if (type == null)
                return null;

            if (Activator.CreateInstance(type) is not ImageEffect effect)
                return null;

            ApplyMappedProperties(effect, mapped.Properties);
            return effect;
        }

        private static void ApplyMappedProperties(ImageEffect effect, Dictionary<string, object?> properties)
        {
            var type = effect.GetType();

            foreach (var pair in properties)
            {
                var property = type.GetProperty(pair.Key);
                if (property == null || !property.CanWrite)
                    continue;

                var converted = ConvertPropertyValue(pair.Value, property.PropertyType);
                property.SetValue(effect, converted);
            }
        }

        private static object? ConvertPropertyValue(object? value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsInstanceOfType(value))
                return value;

            if (value is Newtonsoft.Json.Linq.JToken token)
                return token.ToObject(targetType);

            if (targetType == typeof(SKColor))
            {
                if (value is SKColor color)
                    return color;
            }

            if (targetType.IsEnum)
            {
                if (value is string text)
                    return Enum.Parse(targetType, text, ignoreCase: true);

                return Enum.ToObject(targetType, value);
            }

            return Convert.ChangeType(value, targetType);
        }

        private static float ReadSingle(object? value, float fallback)
        {
            if (value == null)
                return fallback;

            if (value is Newtonsoft.Json.Linq.JToken token)
                return token.ToObject<float>();

            return Convert.ToSingle(value);
        }

        private static int ReadInt(object? value, int fallback)
        {
            if (value == null)
                return fallback;

            if (value is Newtonsoft.Json.Linq.JToken token)
                return token.ToObject<int>();

            return Convert.ToInt32(value);
        }
    }

    public class EffectCategory
    {
        public string Name { get; }
        public List<EffectType> Effects { get; }

        public EffectCategory(string name, params Type[] types)
        {
            Name = name;
            Effects = types.Select(t => new EffectType(t)).ToList();
        }

        public EffectCategory(string name, IEnumerable<EffectType> effects)
        {
            Name = name;
            Effects = effects.ToList();
        }
    }

    public class EffectType
    {
        public string Name { get; }
        public Type Type { get; }

        public EffectType(Type type, string? displayName = null)
        {
            Type = type;

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                Name = displayName;
                return;
            }

            string? name = null;
            try
            {
                if (Activator.CreateInstance(type) is ImageEffect effect)
                {
                    name = effect.Name;
                }
            }
            catch
            {
            }

            Name = name ?? ShareX.ImageEditor.Core.ImageEffects.Helpers.TypeExtensions.GetDescription(type) ?? type.Name;
        }
    }
}

