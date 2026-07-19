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
using XerahS.UI.Services.SettingsSearch;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Lightweight hub VM for Settings search. Debounces query changes and searches a
/// cached index off the UI thread so typing stays responsive.
/// </summary>
public partial class SettingsSearchViewModel : ViewModelBase
{
    public const int DebounceMilliseconds = 180;

    private readonly SettingsSearchService _searchService;
    private CancellationTokenSource? _debounceCts;
    private int _searchGeneration;

    public SettingsSearchViewModel()
        : this(SettingsSearchService.Instance)
    {
    }

    public SettingsSearchViewModel(SettingsSearchService searchService)
    {
        _searchService = searchService;
        _searchService.EnsureCatalogOnly();
        Results = new ObservableCollection<SettingsSearchEntry>();
        UpdateStatusText();
    }

    public ObservableCollection<SettingsSearchEntry> Results { get; }

    public Action<SettingsSearchEntry>? OpenResultHandler { get; set; }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private SettingsSearchEntry? _selectedResult;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _showEmptyState;

    [ObservableProperty]
    private bool _showIdleHint = true;

    partial void OnSearchQueryChanged(string value)
    {
        ScheduleSearch(value);
    }

    public void RefreshStatus()
    {
        UpdateStatusText();
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            ScheduleSearch(SearchQuery);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedResult))]
    private void OpenSelectedResult()
    {
        if (SelectedResult == null)
        {
            return;
        }

        OpenResultHandler?.Invoke(SelectedResult);
    }

    private bool CanOpenSelectedResult() => SelectedResult != null;

    partial void OnSelectedResultChanged(SettingsSearchEntry? value)
    {
        OpenSelectedResultCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        Results.Clear();
        HasResults = false;
        ShowEmptyState = false;
        ShowIdleHint = true;
        SelectedResult = null;
        UpdateStatusText();
    }

    private void ScheduleSearch(string query)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        CancellationToken token = _debounceCts.Token;
        int generation = Interlocked.Increment(ref _searchGeneration);

        if (string.IsNullOrWhiteSpace(query))
        {
            Results.Clear();
            HasResults = false;
            ShowEmptyState = false;
            ShowIdleHint = true;
            SelectedResult = null;
            UpdateStatusText();
            return;
        }

        ShowIdleHint = false;
        _ = DebouncedSearchAsync(query, generation, token);
    }

    private async Task DebouncedSearchAsync(string query, int generation, CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
            if (token.IsCancellationRequested || generation != Volatile.Read(ref _searchGeneration))
            {
                return;
            }

            IReadOnlyList<SettingsSearchEntry> hits = await Task.Run(
                () => _searchService.Search(query),
                token).ConfigureAwait(false);

            if (token.IsCancellationRequested || generation != Volatile.Read(ref _searchGeneration))
            {
                return;
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != Volatile.Read(ref _searchGeneration))
                {
                    return;
                }

                Results.Clear();
                foreach (SettingsSearchEntry hit in hits)
                {
                    Results.Add(hit);
                }

                HasResults = Results.Count > 0;
                ShowEmptyState = !HasResults;
                ShowIdleHint = false;
                SelectedResult = HasResults ? Results[0] : null;
                UpdateStatusText();
            });
        }
        catch (OperationCanceledException)
        {
            // Debounce superseded.
        }
    }

    private void UpdateStatusText()
    {
        if (_searchService.IsFullyIndexed)
        {
            StatusText = $"Searching {_searchService.EntryCount} settings entries";
            return;
        }

        if (_searchService.IsApplicationIndexed || _searchService.IsDestinationIndexed)
        {
            StatusText = "Indexing settings… catalog and partial UI labels available";
            return;
        }

        StatusText = "Searching settings guide (UI labels indexing in background)";
    }
}
