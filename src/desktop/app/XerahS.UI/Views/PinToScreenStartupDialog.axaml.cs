#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Views;

public class PinToScreenStartupResult
{
    public required SKBitmap Image { get; init; }
    public PixelPoint? Location { get; init; }
}

public partial class PinToScreenStartupDialog : UserControl
{
    public PinToScreenStartupResult? Result { get; private set; }

    /// <summary>Invoked when the dialog should close (OK or cancel).</summary>
    public Action? CloseRequested { get; set; }

    public Func<Task<(SKBitmap? Bitmap, PixelPoint? Location)>>? SelectRegionRequested { get; set; }
    public Func<Task<string?>>? BrowseFileRequested { get; set; }

    public PinToScreenStartupDialog()
    {
        InitializeComponent();

        FromScreenButton.Click += OnFromScreenClick;
        FromClipboardButton.Click += OnFromClipboardClick;
        FromFileButton.Click += OnFromFileClick;
        CancelButton.Click += OnCancelClick;
    }

    private async void OnFromScreenClick(object? sender, RoutedEventArgs e)
    {
        if (SelectRegionRequested == null) return;

        // Close overlay first so region capture is not covered.
        CloseRequested?.Invoke();

        try
        {
            var (bitmap, location) = await SelectRegionRequested();
            if (bitmap != null)
            {
                Result = new PinToScreenStartupResult { Image = bitmap, Location = location };
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen from screen");
        }
    }

    private void OnFromClipboardClick(object? sender, RoutedEventArgs e)
    {
        if (!PlatformServices.IsInitialized) return;

        var bitmap = PlatformServices.Clipboard.GetImage();
        if (bitmap == null)
        {
            ShowToast("Clipboard does not contain an image.");
            return;
        }

        Result = new PinToScreenStartupResult { Image = bitmap };
        CloseRequested?.Invoke();
    }

    private async void OnFromFileClick(object? sender, RoutedEventArgs e)
    {
        if (BrowseFileRequested == null) return;

        var path = await BrowseFileRequested();
        if (string.IsNullOrEmpty(path)) return;

        using var bitmap = SKBitmap.Decode(path);
        if (bitmap == null)
        {
            ShowToast("Failed to load image file.");
            return;
        }

        Result = new PinToScreenStartupResult { Image = bitmap.Copy() };
        CloseRequested?.Invoke();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        CloseRequested?.Invoke();
    }

    private static void ShowToast(string text)
    {
        try
        {
            if (PlatformServices.IsToastServiceInitialized)
            {
                PlatformServices.Toast.ShowToast(new ToastConfig
                {
                    Title = "Pin to Screen",
                    Text = text,
                    Duration = 4f,
                    Size = new SizeI(420, 120),
                    AutoHide = true,
                    LeftClickAction = ToastClickAction.CloseNotification
                });
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen startup toast");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
