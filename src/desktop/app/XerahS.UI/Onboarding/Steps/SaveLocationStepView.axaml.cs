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

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.UI.Onboarding.Steps;

/// <summary>
/// Step 2: Save Location Configuration view.
/// </summary>
public partial class SaveLocationStepView : UserControl
{
    public SaveLocationStepView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SaveLocationStepViewModel vm)
        {
            if (vm.BrowseFolderCallback == null)
            {
                vm.BrowseFolderCallback = async () =>
                {
                    var topLevel = this.VisualRoot as TopLevel;
                    if (topLevel?.StorageProvider == null) return null;

                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                        new FolderPickerOpenOptions
                        {
                            Title = "Select Screenshots Folder",
                            AllowMultiple = false,
                        });

                    if (folders.Count == 0) return null;
                    try
                    {
                        return folders[0].TryGetLocalPath();
                    }
                    catch (Exception)
                    {
                        // Defensive: Avalonia's TryGetLocalPath can throw on some macOS paths
                        return null;
                    }
                };
            }
        }
    }

}
