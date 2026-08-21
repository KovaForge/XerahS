#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using Avalonia.Threading;
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.Common;

namespace XerahS.UI.Services;

/// <summary>
/// Centralizes modal open scheduling to keep behavior consistent across platforms.
/// </summary>
public static class ModalOpenService
{
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
