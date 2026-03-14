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
using System;

namespace XerahS.UI.Views;

/// <summary>
/// Shared top-level surface for standard windows and dialogs. Owns the first
/// painted client-area surface so transparent layout roots do not fall through
/// to the stock Fluent window background.
/// </summary>
public class SurfaceWindow : Window
{
    private Border? _surfaceHost;
    private bool _isWrappingContent;

    public SurfaceWindow()
    {
        PropertyChanged += OnSurfaceWindowPropertyChanged;
    }

    protected override void OnOpened(EventArgs e)
    {
        EnsureSurfaceHost();
        base.OnOpened(e);
    }

    private void OnSurfaceWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentProperty)
        {
            EnsureSurfaceHost();
            return;
        }

        if (e.Property == BackgroundProperty && _surfaceHost != null)
        {
            _surfaceHost.Background = Background;
        }
    }

    private void EnsureSurfaceHost()
    {
        if (_isWrappingContent || Content is not Control content)
        {
            return;
        }

        if (ReferenceEquals(content, _surfaceHost))
        {
            return;
        }

        if (_surfaceHost != null && ReferenceEquals(_surfaceHost.Child, content))
        {
            _surfaceHost.Background = Background;
            return;
        }

        var host = new Border
        {
            Background = Background,
            Child = content
        };

        _surfaceHost = host;
        _isWrappingContent = true;

        try
        {
            Content = host;
        }
        finally
        {
            _isWrappingContent = false;
        }
    }
}
