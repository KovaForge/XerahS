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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using XerahS.Core;
using XerahS.UI.Services;
using System.Linq;

namespace XerahS.UI.Views
{
    public partial class DestinationSettingsView : PageView
    {
        public DestinationSettingsView()
        {
            InitializeComponent();
            DataContext = UiViewModelFactoryAccessor.GetRequired().CreateDestinationSettingsViewModel();

            // Call async Initialize when the view is loaded
            Loaded += async (s, e) =>
            {
                if (DataContext is ViewModels.DestinationSettingsViewModel vm)
                {
                    try
                    {
                        await vm.Initialize();
                    }
                    catch (Exception ex)
                    {
                        XerahS.Common.DebugHelper.WriteException(ex, "Destination Settings failed to initialize");
                    }
                }
            };

            // Save uploaders config when navigating away from this view
            Unloaded += (s, e) =>
            {
                _ = SettingsManager.SaveUploadersConfigAsync();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public bool ApplySearchTarget(string? categoryName, string? instanceName)
        {
            if (DataContext is not ViewModels.DestinationSettingsViewModel vm)
            {
                return false;
            }

            bool changed = false;

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                ViewModels.CategoryViewModel? category = vm.Categories.FirstOrDefault(item =>
                    string.Equals(item.Name, categoryName, StringComparison.OrdinalIgnoreCase));
                if (category != null && !ReferenceEquals(vm.SelectedCategory, category))
                {
                    vm.SelectedCategory = category;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(instanceName) && vm.SelectedCategory != null)
            {
                ViewModels.UploaderInstanceViewModel? instance = vm.SelectedCategory.Instances.FirstOrDefault(item =>
                    string.Equals(item.DisplayName, instanceName, StringComparison.OrdinalIgnoreCase));
                if (instance != null)
                {
                    vm.SelectedCategory.SelectedInstance = instance;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
