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
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NUnit.Framework;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.MediaBrowser;

[TestFixture]
public class MediaBrowserViewTests
{
    private static async Task<(Window Window, MediaBrowserViewModel Vm)> ShowAsync()
    {
        var store = new MediaBrowserViewModelTests.MemoryExplorer(MediaBrowserViewModelTests.Full);
        store.Folders.Add("screenshots");
        store.Folders.Add("recordings");
        store.Files["2026-09-26 17.20.44.png"] = new byte[245_000];
        store.Files["clip_trimmed.mp4"] = new byte[4_200_000];
        store.Files["notes.txt"] = new byte[1_024];
        store.Files["logs.zip"] = new byte[88_000];

        var vm = new MediaBrowserViewModel(
            [MediaBrowserViewModelTests.Source(store, "xerahs-media"), MediaBrowserViewModelTests.Source(store, "backup", "{\"b\":2}")],
            new MediaBrowserViewModelTests.ScriptedDialogs());
        var window = new Window { Width = 1180, Height = 760, Content = new MediaBrowserView { DataContext = vm } };
        foreach (string theme in new[] { "Typography", "ThemeResources" })
        {
            window.Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://XerahS.UI/"))
            {
                Source = new Uri($"avares://XerahS.UI/Themes/{theme}.axaml"),
            });
        }

        window.Show();

        for (int i = 0; i < 20 && (vm.IsBusy || vm.Items.Count == 0); i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static void Capture(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("XERAHS_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using FileStream file = File.Create(Path.Combine(directory, name + ".png"));
        window.CaptureRenderedFrame()?.Save(file, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaTest]
    public async Task RendersListWithFoldersFirstAndStatus()
    {
        (Window window, MediaBrowserViewModel vm) = await ShowAsync();
        ListBox rows = ((MediaBrowserView)window.Content!).FindControl<ListBox>("RowsList")!;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsGridView, Is.False, "sources without thumbnails open in list view");
            Assert.That(rows.ItemCount, Is.EqualTo(6));
            Assert.That(vm.Items[0].Name, Is.EqualTo("recordings"));
            Assert.That(vm.StatusText, Does.StartWith("4 files, 2 folders"));
        });

        vm.SelectedItem = vm.Items.First(i => i.Name.EndsWith(".png"));
        Dispatcher.UIThread.RunJobs();
        Capture(window, "media-browser-list");
        window.Close();
    }

    [AvaloniaTest]
    public async Task GridViewAndKeyboardNavigation()
    {
        (Window window, MediaBrowserViewModel vm) = await ShowAsync();
        vm.IsGridView = true;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "media-browser-grid");

        vm.SelectedItem = vm.Items.First(i => i.Name == "screenshots");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        for (int i = 0; i < 20 && vm.CurrentPath != "screenshots"; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.That(vm.CurrentPath, Is.EqualTo("screenshots"));
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        for (int i = 0; i < 20 && vm.CurrentPath != ""; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.That(vm.CurrentPath, Is.EqualTo(""));
        window.Close();
    }
}
