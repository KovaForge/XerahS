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
using ShareX.ImageEditor.Presentation.Theming;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.UI.Helpers;

namespace XerahS.UI.Views
{
    public partial class EditorWindow : SurfaceWindow
    {
        private MainViewModel? _viewModel;
        private bool _showTaskModeButtons;

        public bool ShowTaskModeButtons
        {
            get => _showTaskModeButtons;
            set
            {
                _showTaskModeButtons = value;
                UpdateTaskModeButtonVisibility();
            }
        }

        public EditorWindow()
        {
            InitializeComponent();

            RequestedThemeVariant = ThemeManager.GetCurrentTheme();
            ThemeManager.ThemeChanged += (s, theme) => RequestedThemeVariant = theme;
            Opened += (_, _) => UpdateTaskModeButtonVisibility();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnViewModelCloseRequested;
            }

            base.OnDataContextChanged(e);

            _viewModel = DataContext as MainViewModel;
            if (_viewModel != null)
            {
                _viewModel.CloseRequested += OnViewModelCloseRequested;
            }

            UpdateTaskModeButtonVisibility();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnViewModelCloseRequested;
                _viewModel = null;
            }

            base.OnClosed(e);
        }

        private void OnViewModelCloseRequested(object? sender, EventArgs e)
        {
            Close();
        }

        private void UpdateTaskModeButtonVisibility()
        {
            EditorView? editorView = this.FindControl<EditorView>("EditorViewControl");
            if (editorView != null)
            {
                EditorTaskButtons.SetVisible(editorView, ShowTaskModeButtons);
            }
        }
    }
}
