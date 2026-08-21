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
using Avalonia.Interactivity;
using Ava.ViewModels;

namespace Ava.Views;

public partial class MobileAboutView : UserControl
{
    public MobileAboutView()
    {
        InitializeComponent();
        DataContext = new MobileAboutViewModel();
    }

    private async void OpenLink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.Button { Tag: string url } || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher != null)
            await launcher.LaunchUriAsync(uri);
    }
}
