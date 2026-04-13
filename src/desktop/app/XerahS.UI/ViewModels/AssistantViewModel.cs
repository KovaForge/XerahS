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
using XerahS.UI.Assistant;

namespace XerahS.UI.ViewModels;

public partial class AssistantViewModel : ViewModelBase
{
    private readonly IAssistantService _assistantService;
    private CancellationTokenSource? _requestCts;
    private DateTimeOffset _lastEscClearAt = DateTimeOffset.MinValue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ask for a XerahS action.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasPendingConfirmation;

    [ObservableProperty]
    private string _confirmationText = string.Empty;

    [ObservableProperty]
    private bool _hasResultPreview;

    [ObservableProperty]
    private string _resultPreviewText = string.Empty;

    private AssistantAction? _pendingAction;

    public ObservableCollection<AssistantResultItem> Items { get; } = new();
    public ObservableCollection<AssistantAction> Actions { get; } = new();
    public ObservableCollection<string> Suggestions { get; } = new(AssistantCommandRouter.GetSuggestions());

    public event Action? RequestClose;
    public event Action? RequestFocusPrompt;

    public AssistantViewModel()
        : this(new AssistantService())
    {
    }

    public AssistantViewModel(IAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    private bool CanSubmit() => !string.IsNullOrWhiteSpace(Prompt);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        CancelRequest();
        _requestCts = new CancellationTokenSource();
        await RunAsync(_assistantService.ProcessPromptAsync(Prompt, _requestCts.Token));
    }

    [RelayCommand]
    private async Task ExecuteActionAsync(AssistantAction? action)
    {
        if (action == null)
        {
            return;
        }

        CancelRequest();
        _requestCts = new CancellationTokenSource();
        await RunAsync(_assistantService.ExecuteActionAsync(action, confirmed: false, _requestCts.Token));
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (_pendingAction == null)
        {
            return;
        }

        var action = _pendingAction;
        HasPendingConfirmation = false;
        ConfirmationText = string.Empty;
        _pendingAction = null;

        CancelRequest();
        _requestCts = new CancellationTokenSource();
        await RunAsync(_assistantService.ExecuteActionAsync(action, confirmed: true, _requestCts.Token));
    }

    [RelayCommand]
    private void CancelConfirmation()
    {
        HasPendingConfirmation = false;
        ConfirmationText = string.Empty;
        _pendingAction = null;
        StatusText = "Action cancelled. You can retry if needed.";
    }

    [RelayCommand]
    private void UseSuggestion(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            return;
        }

        Prompt = suggestion;
        RequestFocusPrompt?.Invoke();
    }

    public void HandleEscape()
    {
        if (HasPendingConfirmation)
        {
            CancelConfirmation();
            return;
        }

        if (IsBusy)
        {
            CancelRequest();
            StatusText = "Action cancelled. You can retry if needed.";
            return;
        }

        if (!string.IsNullOrEmpty(Prompt))
        {
            var now = DateTimeOffset.Now;
            if (now - _lastEscClearAt <= TimeSpan.FromSeconds(2))
            {
                RequestClose?.Invoke();
                return;
            }

            Prompt = string.Empty;
            _lastEscClearAt = now;
            return;
        }

        RequestClose?.Invoke();
    }

    private async Task RunAsync(Task<AssistantResponse> task)
    {
        IsBusy = true;
        StatusText = "Processing...";

        try
        {
            ApplyResponse(await task);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Action cancelled. You can retry if needed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResponse(AssistantResponse response)
    {
        Items.Clear();
        Actions.Clear();
        HasResultPreview = false;
        ResultPreviewText = string.Empty;

        foreach (var item in response.Items)
        {
            Items.Add(item);
        }

        foreach (var action in response.Actions)
        {
            Actions.Add(action);
        }

        HasPendingConfirmation = response.PendingConfirmation != null;
        ConfirmationText = response.PendingConfirmation?.Copy ?? string.Empty;
        _pendingAction = response.PendingConfirmation?.Action;
        StatusText = response.Message;
        UpdateResultPreview(response);
    }

    private void UpdateResultPreview(AssistantResponse response)
    {
        string? preview = response.Actions
            .FirstOrDefault(action => action.Kind == AssistantActionKind.CopyText && !string.IsNullOrWhiteSpace(action.Text))
            ?.Text;

        if (string.IsNullOrWhiteSpace(preview) && response.Items.Count > 0)
        {
            preview = string.Join(
                Environment.NewLine,
                response.Items.Select(item => !string.IsNullOrWhiteSpace(item.Subtitle) ? item.Subtitle : item.Title)
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (string.IsNullOrWhiteSpace(preview))
        {
            return;
        }

        ResultPreviewText = preview;
        HasResultPreview = true;
    }

    private void CancelRequest()
    {
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = null;
    }
}
