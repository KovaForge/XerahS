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

#endregion

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace XerahS.UI.Onboarding.Controls;

public partial class DirectoryPicker : UserControl
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<DirectoryPicker, string?>(nameof(Value));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<DirectoryPicker, string?>(
            nameof(ErrorMessage),
            defaultValue: null,
            coerce: (o, v) => v ?? string.Empty);

    public static readonly StyledProperty<bool> IsWritableProperty =
        AvaloniaProperty.Register<DirectoryPicker, bool>(nameof(IsWritable), inherits: true);

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public bool IsWritable
    {
        get => GetValue(IsWritableProperty);
        set => SetValue(IsWritableProperty, value);
    }

    public DirectoryPicker()
    {
        InitializeComponent();
        ValueProperty.Changed.AddClassHandler<DirectoryPicker>(OnValueChanged);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnValueChanged(DirectoryPicker picker, AvaloniaPropertyChangedEventArgs e)
    {
        ValidatePath();
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose Screenshots Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            Value = folder[0].Path.LocalPath;
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        Value = null;
    }

    private void ValidatePath()
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            IsWritable = false;
            ShowError(false);
            return;
        }

        try
        {
            if (!System.IO.Directory.Exists(Value))
            {
                var parent = System.IO.Path.GetDirectoryName(Value);
                if (string.IsNullOrEmpty(parent) || !System.IO.Directory.Exists(parent))
                {
                    IsWritable = false;
                    ShowError(true);
                    return;
                }
            }

            var testFile = System.IO.Path.Combine(Value, $".xerahs_write_test_{Guid.NewGuid():N}");
            using (System.IO.File.Create(testFile, 0, System.IO.FileOptions.DeleteOnClose)) { }
            IsWritable = true;
            ShowError(false);
        }
        catch
        {
            IsWritable = false;
            ShowError(true);
        }
    }

    private void ShowError(bool show)
    {
        var errorBlock = this.FindControl<TextBlock>("ErrorTextBlock");
        if (errorBlock != null)
            errorBlock.IsVisible = show;
    }
}
