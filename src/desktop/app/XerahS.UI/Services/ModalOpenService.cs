#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.Common;

namespace XerahS.UI.Services;

/// <summary>
/// Centralizes modal open scheduling to keep behavior consistent across platforms.
/// </summary>
public static class ModalOpenService
{
    /// <summary>
    /// Returns the view model that backs the main window's modal overlay.
    /// <see cref="MainViewModel.Current"/> is reassigned by every MainViewModel constructor,
    /// so it points at the last standalone editor window once one has been opened.
    /// </summary>
    public static MainViewModel? ResolveHostViewModel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainViewModel hostVm)
        {
            return hostVm;
        }

        return MainViewModel.Current;
    }

    public static void Open(MainViewModel mainViewModel, object modalContent, string debugSource)
    {
        Dispatcher.UIThread.Post(() =>
        {
            mainViewModel.ModalContent = modalContent;
            mainViewModel.IsModalOpen = true;
            DebugHelper.WriteLine($"[{debugSource}] Modal opened");
        }, DispatcherPriority.Background);
    }
}
