#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using CommunityToolkit.Mvvm.Input;
using XerahS.UI.Views.Dialogs;

namespace XerahS.UI.ViewModels;

public partial class OpenImageChoiceViewModel : ViewModelBase
{
    public Action<OpenImageChoice>? CloseRequested { get; set; }

    public OpenImageChoice Choice { get; private set; } = OpenImageChoice.Cancel;

    [RelayCommand]
    private void Replace()
    {
        Choice = OpenImageChoice.ReplaceImage;
        CloseRequested?.Invoke(Choice);
    }

    [RelayCommand]
    private void AddAsShape()
    {
        Choice = OpenImageChoice.AddAsShape;
        CloseRequested?.Invoke(Choice);
    }

    [RelayCommand]
    private void Cancel()
    {
        Choice = OpenImageChoice.Cancel;
        CloseRequested?.Invoke(Choice);
    }
}
