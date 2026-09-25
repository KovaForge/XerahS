#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion License Information (GPL v3)

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XerahS.UI.Views;

public partial class SimplePromptView : UserControl
{
    public SimplePromptView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
