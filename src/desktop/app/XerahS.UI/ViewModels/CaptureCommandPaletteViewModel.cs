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
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core.CaptureCommandPalette;

namespace XerahS.UI.ViewModels;

public partial class CaptureCommandPaletteViewModel : ViewModelBase
{
    private readonly Func<IReadOnlyList<CaptureCommandPaletteItem>> _loadItems;
    private readonly Func<CaptureCommandPaletteItem, Task> _executeItemAsync;
    private IReadOnlyList<CaptureCommandPaletteItem> _allItems = Array.Empty<CaptureCommandPaletteItem>();

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedCommand))]
    private CaptureCommandPaletteItem? _selectedItem;

    [ObservableProperty]
    private string _statusText = "Type to filter capture workflows.";

    public ObservableCollection<CaptureCommandPaletteItem> Items { get; } = new();

    public event Action? RequestClose;
    public event Action? RequestFocusSearch;

    public CaptureCommandPaletteViewModel(
        Func<IReadOnlyList<CaptureCommandPaletteItem>> loadItems,
        Func<CaptureCommandPaletteItem, Task> executeItemAsync)
    {
        _loadItems = loadItems;
        _executeItemAsync = executeItemAsync;
        ReloadItems();
    }

    partial void OnQueryChanged(string value)
    {
        RefreshItems();
    }

    public void ReloadItems()
    {
        try
        {
            _allItems = _loadItems();
            RefreshItems();
        }
        catch (Exception ex)
        {
            _allItems = Array.Empty<CaptureCommandPaletteItem>();
            Items.Clear();
            SelectedItem = null;
            StatusText = "Capture workflows are unavailable.";
            DebugHelper.WriteException(ex, "Capture command palette failed to load workflows.");
        }
    }

    public void MoveSelection(int delta)
    {
        if (Items.Count == 0)
        {
            SelectedItem = null;
            return;
        }

        int currentIndex = SelectedItem == null ? -1 : Items.IndexOf(SelectedItem);
        int nextIndex = currentIndex < 0
            ? (delta < 0 ? Items.Count - 1 : 0)
            : (currentIndex + delta + Items.Count) % Items.Count;
        SelectedItem = Items[nextIndex];
    }

    public void HandleEscape()
    {
        if (!string.IsNullOrWhiteSpace(Query))
        {
            Query = string.Empty;
            RequestFocusSearch?.Invoke();
            return;
        }

        RequestClose?.Invoke();
    }

    private void RefreshItems()
    {
        CaptureCommandPaletteItem? previousSelection = SelectedItem;
        IReadOnlyList<CaptureCommandPaletteItem> filtered = CaptureCommandPaletteProvider.FilterAndRank(_allItems, Query);

        Items.Clear();
        foreach (CaptureCommandPaletteItem item in filtered)
        {
            Items.Add(item);
        }

        SelectedItem = previousSelection != null && Items.Contains(previousSelection)
            ? previousSelection
            : Items.FirstOrDefault();

        StatusText = Items.Count switch
        {
            0 when _allItems.Count == 0 => "No capture workflows are configured.",
            0 => "No capture workflows match your search.",
            1 => "1 capture workflow",
            _ => $"{Items.Count} capture workflows"
        };
    }

    private bool CanExecuteSelected() => SelectedItem != null;

    [RelayCommand(CanExecute = nameof(CanExecuteSelected))]
    private async Task ExecuteSelectedAsync()
    {
        if (SelectedItem == null)
        {
            return;
        }

        CaptureCommandPaletteItem item = SelectedItem;
        RequestClose?.Invoke();

        try
        {
            await _executeItemAsync(item);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Capture command palette failed to execute '{item.Label}'.");
        }
    }
}
