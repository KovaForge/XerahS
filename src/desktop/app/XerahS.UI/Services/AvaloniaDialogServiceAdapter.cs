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

using System.Threading.Tasks;
using XerahS.Services.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Services;

/// <summary>
/// Avalonia implementation of <see cref="IDialogService"/> using ModalContent overlay.
/// </summary>
public sealed class AvaloniaDialogServiceAdapter : IDialogService
{
    public Task ShowMessageAsync(string title, string message)
    {
        return ShowPromptAsync(title, message, showCancel: false, isError: false, isWarning: false);
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        return ShowPromptAsync(title, message, showCancel: true, isError: false, isWarning: false);
    }

    public Task ShowErrorAsync(string title, string error)
    {
        return ShowPromptAsync(title, error, showCancel: false, isError: true, isWarning: false);
    }

    public Task ShowWarningAsync(string title, string warning)
    {
        return ShowPromptAsync(title, warning, showCancel: false, isError: false, isWarning: true);
    }

    public async Task<string?> ShowInputAsync(string title, string label, string? defaultValue = null)
    {
        var viewModel = new SimplePromptViewModel
        {
            Title = title,
            Label = label,
            InputText = defaultValue ?? string.Empty,
            ShowCancel = true,
            ShowInput = true,
            PrimaryButtonText = "OK"
        };

        var ok = await ModalDialogHost.ShowAsync(
            viewModel,
            set => viewModel.CloseRequested = set,
            dismissResult: false,
            debugSource: "SimplePrompt.Input");

        return ok ? viewModel.AcceptedInput : null;
    }

    public Task<T?> ShowSelectionAsync<T>(string title, string label, System.Collections.Generic.IEnumerable<T> items) where T : class
    {
        return Task.FromResult<T?>(default);
    }

    private static async Task<bool> ShowPromptAsync(string title, string message, bool showCancel, bool isError, bool isWarning)
    {
        var viewModel = new SimplePromptViewModel
        {
            Title = title,
            Message = message,
            ShowCancel = showCancel,
            ShowInput = false,
            IsError = isError,
            IsWarning = isWarning,
            PrimaryButtonText = showCancel ? "OK" : "Close"
        };

        return await ModalDialogHost.ShowAsync(
            viewModel,
            set => viewModel.CloseRequested = set,
            dismissResult: false,
            debugSource: "SimplePrompt");
    }
}
