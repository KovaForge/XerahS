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

using ShareX.ImageEditor.Core.Annotations;
using System.ComponentModel;

namespace XerahS.RegionCapture.ViewModels;

public sealed class RegionCaptureAnnotationToolCoordinator
{
    private readonly List<RegionCaptureAnnotationViewModel> _viewModels = [];
    private EditorTool _activeTool = EditorTool.Select;
    private bool _isSynchronizing;

    public EditorTool ActiveTool => _activeTool;

    public void Register(RegionCaptureAnnotationViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (_viewModels.Contains(viewModel))
        {
            return;
        }

        _viewModels.Add(viewModel);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (viewModel.ActiveTool != _activeTool)
        {
            viewModel.ActiveTool = _activeTool;
        }
    }

    public void Unregister(RegionCaptureAnnotationViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (!_viewModels.Remove(viewModel))
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSynchronizing ||
            e.PropertyName != nameof(RegionCaptureAnnotationViewModel.ActiveTool) ||
            sender is not RegionCaptureAnnotationViewModel source)
        {
            return;
        }

        SynchronizeActiveTool(source);
    }

    private void SynchronizeActiveTool(RegionCaptureAnnotationViewModel source)
    {
        _activeTool = source.ActiveTool;
        _isSynchronizing = true;

        try
        {
            foreach (var viewModel in _viewModels)
            {
                if (ReferenceEquals(viewModel, source) ||
                    viewModel.ActiveTool == _activeTool)
                {
                    continue;
                }

                viewModel.ActiveTool = _activeTool;
            }
        }
        finally
        {
            _isSynchronizing = false;
        }
    }
}
