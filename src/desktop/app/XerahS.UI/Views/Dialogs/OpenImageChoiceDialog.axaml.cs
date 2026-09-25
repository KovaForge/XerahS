#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XerahS.UI.Views.Dialogs;

public enum OpenImageChoice
{
    Cancel = 0,
    ReplaceImage = 1,
    AddAsShape = 2
}

public partial class OpenImageChoiceDialog : UserControl
{
    public OpenImageChoiceDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
