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
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Services;

/// <summary>Opens the Media Browser over every destination that supports browsing.</summary>
public static class MediaBrowserToolService
{
    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner) =>
        job == WorkflowType.MediaBrowser ? OpenAsync() : Task.CompletedTask;

    /// <summary>Opens the browser, starting on <paramref name="preferredInstance"/> when it is browsable.</summary>
    public static async Task OpenAsync(UploaderInstance? preferredInstance = null)
    {
        try
        {
            IUiViewModelFactory factory = UiViewModelFactoryAccessor.GetRequired();
            IReadOnlyList<MediaBrowserSource> sources = MediaBrowserSource.Discover(InstanceManager.Instance.GetInstances(), ProviderCatalog.GetProvider);
            if (sources.Count == 0)
            {
                await factory.CoreDialogService.ShowMessageAsync(
                    "Media Browser",
                    "None of your destinations can be browsed yet.\n\nAdd Amazon S3, FTP / FTPS / SFTP, Dropbox, Nextcloud, Immich or Imgur in Destinations, then open the Media Browser again.");
                return;
            }

            MediaBrowserSource? initial = preferredInstance == null
                ? null
                : sources.FirstOrDefault(source => source.Instance.InstanceId == preferredInstance.InstanceId)
                    ?? sources.FirstOrDefault(source => source.Instance.ProviderId == preferredInstance.ProviderId &&
                                                        source.Instance.SettingsJson == preferredInstance.SettingsJson);

            MediaBrowserViewModel viewModel = factory.CreateMediaBrowserViewModel(sources, initial);
            await factory.ViewDialogService.ShowMediaBrowserAsync(viewModel);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to open the Media Browser");
        }
    }
}
