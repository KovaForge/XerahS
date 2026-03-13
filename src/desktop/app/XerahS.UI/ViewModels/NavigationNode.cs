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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XerahS.UI.ViewModels;

public enum NavigationNodeKind
{
    Page,
    Action,
    Group
}

public partial class NavigationNode : ObservableObject
{
    public NavigationNode(string text, string? tag, string? glyph, NavigationNodeKind kind)
    {
        Text = text;
        Tag = tag;
        Glyph = glyph;
        Kind = kind;
    }

    public string Text { get; }

    public string? Tag { get; }

    public string? Glyph { get; }

    public NavigationNodeKind Kind { get; }

    public ObservableCollection<NavigationNode> Children { get; } = new();

    public NavigationNode? Parent { get; private set; }

    public bool HasGlyph => !string.IsNullOrWhiteSpace(Glyph);

    public void AddChild(NavigationNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void ReplaceChildren(IEnumerable<NavigationNode> children)
    {
        Children.Clear();

        foreach (NavigationNode child in children)
        {
            AddChild(child);
        }
    }

    public void ExpandPath()
    {
        for (NavigationNode? current = this; current != null; current = current.Parent)
        {
            if (current.Children.Count > 0)
            {
                current.IsExpanded = true;
            }
        }
    }

    [ObservableProperty]
    private bool _isExpanded;
}
