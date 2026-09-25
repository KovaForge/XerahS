#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class UpdateMessageBox : UserControl
{
    public UpdateMessageBox()
    {
        InitializeComponent();
    }

    public static UpdateMessageBoxViewModel CreateViewModel(UpdateChecker updateChecker)
    {
        return new UpdateMessageBoxViewModel
        {
            CurrentVersion = FormatVersion(updateChecker.CurrentVersion),
            LatestVersion = FormatVersion(updateChecker.LatestVersion),
            IsPreRelease = (updateChecker as GitHubUpdateChecker)?.IsPreRelease ?? false,
            IsPortable = updateChecker.IsPortable,
            IsDev = updateChecker.IsDev
        };
    }

    private static string FormatVersion(Version? version)
    {
        return version?.ToString() ?? "Unknown";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
