using System.Collections.Generic;
using System.Threading.Tasks;

namespace XerahS.UI.Services
{
    public interface IViewDialogService
    {
        Task ShowDialogAsync<TWindow>(object dataContext) where TWindow : class, new();
        Task<TResult?> ShowDialogAsync<TWindow, TResult>(object dataContext) where TWindow : class, new();
        Task<string?> ShowFilePickerAsync(string title, IEnumerable<string>? filters = null);
        Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string defaultExtension, IEnumerable<string>? filters = null);
        Task<string?> ShowFolderPickerAsync(string title);
        object? GetMainWindow();
        IEnumerable<object> GetOpenWindows();
    }
}
