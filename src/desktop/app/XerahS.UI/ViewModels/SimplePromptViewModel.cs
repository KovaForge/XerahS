#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XerahS.UI.ViewModels;

/// <summary>Shared modal prompt for message / confirm / text / secret input.</summary>
public partial class SimplePromptViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _showCancel;
    [ObservableProperty] private bool _showInput;
    [ObservableProperty] private bool _isPassword;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _isWarning;
    [ObservableProperty] private string _primaryButtonText = "OK";
    [ObservableProperty] private string _cancelButtonText = "Cancel";

    public Action<bool>? CloseRequested { get; set; }

    /// <summary>When input mode, set on OK; otherwise unused.</summary>
    public string? AcceptedInput { get; private set; }

    public bool Confirmed { get; private set; }

    [RelayCommand]
    private void Primary()
    {
        Confirmed = true;
        if (ShowInput)
            AcceptedInput = InputText;
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        AcceptedInput = null;
        CloseRequested?.Invoke(false);
    }
}
