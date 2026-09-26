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

using NUnit.Framework;
using XerahS.Services.Abstractions;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.MediaBrowser;

[TestFixture]
public class MediaBrowserViewModelTests
{
    /// <summary>An in-memory remote store: paths are "a/b" style, folders are implicit.</summary>
    internal sealed class MemoryExplorer(ExplorerCapabilities capabilities) : IUploaderExplorer
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Folders { get; } = new(StringComparer.Ordinal);
        public List<string> Calls { get; } = [];
        public int PageSize { get; set; } = int.MaxValue;

        public bool SupportsFolders => true;
        public ExplorerCapabilities BrowserCapabilities => capabilities;

        public Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
        {
            Calls.Add("list:" + query.FolderPath);
            string folder = (query.FolderPath ?? "").Trim('/');
            string prefix = folder.Length == 0 ? "" : folder + "/";
            var items = new List<MediaItem>();
            foreach (string f in Folders.Where(f => ParentOf(f) == folder).OrderBy(f => f))
                items.Add(new MediaItem { Name = f[prefix.Length..], Path = f, IsFolder = true });
            foreach ((string path, byte[] data) in Files.Where(kv => ParentOf(kv.Key) == folder).OrderBy(kv => kv.Key))
                items.Add(new MediaItem { Name = path[prefix.Length..], Path = path, SizeBytes = data.Length, Url = "https://cdn.example/" + path, ModifiedAt = new DateTime(2026, 1, data.Length % 28 + 1) });

            int skip = int.TryParse(query.ContinuationToken, out int s) ? s : 0;
            var page = items.Skip(skip).Take(PageSize).ToList();
            return Task.FromResult(new ExplorerPage { Items = page, ContinuationToken = skip + page.Count < items.Count ? (skip + page.Count).ToString() : null });
        }

        public Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default) => Task.FromResult<byte[]?>(null);
        public Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default) =>
            Task.FromResult<Stream?>(Files.TryGetValue(item.Path, out byte[]? data) ? new MemoryStream(data) : null);
        public Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default) => Task.FromResult(false);
        public Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default) => Task.FromResult(false);

        public Task<bool> CreateFolderAsync(ExplorerContext context, string parentPath, string folderName, CancellationToken cancellation = default)
        {
            Calls.Add($"mkdir:{parentPath}|{folderName}|{context.SettingsJson}");
            Folders.Add(Combine(parentPath, folderName));
            return Task.FromResult(true);
        }

        public async Task<bool> UploadAsync(ExplorerContext context, string folderPath, string fileName, Stream content, CancellationToken cancellation = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellation);
            Files[Combine(folderPath, fileName)] = buffer.ToArray();
            Calls.Add($"upload:{folderPath}|{fileName}");
            return true;
        }

        public Task<bool> RenameAsync(ExplorerContext context, MediaItem item, string newName, CancellationToken cancellation = default)
        {
            string target = Combine(ParentOf(item.Path), newName);
            Files[target] = Files[item.Path];
            Files.Remove(item.Path);
            Calls.Add($"rename:{item.Path}|{newName}");
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(ExplorerContext context, MediaItem item, CancellationToken cancellation = default)
        {
            Calls.Add("delete:" + item.Path);
            if (item.IsFolder)
            {
                Folders.Remove(item.Path);
                foreach (string key in Files.Keys.Where(k => k.StartsWith(item.Path + "/", StringComparison.Ordinal)).ToList()) Files.Remove(key);
            }
            else
            {
                Files.Remove(item.Path);
            }

            return Task.FromResult(true);
        }

        public Task<bool?> HasChildrenAsync(ExplorerContext context, MediaItem folder, CancellationToken cancellation = default) =>
            Task.FromResult<bool?>(Files.Keys.Any(k => k.StartsWith(folder.Path + "/", StringComparison.Ordinal)));

        private static string Combine(string folder, string name) => folder.Trim('/').Length == 0 ? name : folder.Trim('/') + "/" + name;
        private static string ParentOf(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    }

    internal sealed class ScriptedDialogs : IDialogService
    {
        public Queue<string?> Inputs { get; } = new();
        public Queue<bool> Confirmations { get; } = new();
        public List<string> Shown { get; } = [];

        public Task ShowMessageAsync(string title, string message) { Shown.Add("message:" + title); return Task.CompletedTask; }
        public Task<bool> ShowConfirmationAsync(string title, string message) { Shown.Add("confirm:" + title); return Task.FromResult(Confirmations.Count == 0 || Confirmations.Dequeue()); }
        public Task ShowErrorAsync(string title, string error) { Shown.Add("error:" + title); return Task.CompletedTask; }
        public Task ShowWarningAsync(string title, string warning) => Task.CompletedTask;
        public Task<string?> ShowInputAsync(string title, string label, string? defaultValue = null) => Task.FromResult(Inputs.Count > 0 ? Inputs.Dequeue() : defaultValue);
        public Task<T?> ShowSelectionAsync<T>(string title, string label, IEnumerable<T> items) where T : class => Task.FromResult(items.FirstOrDefault());
    }

    internal const ExplorerCapabilities Full =
        ExplorerCapabilities.Download | ExplorerCapabilities.Upload | ExplorerCapabilities.Rename |
        ExplorerCapabilities.Delete | ExplorerCapabilities.Url | ExplorerCapabilities.CreateFolder;

    internal static MediaBrowserSource Source(IUploaderExplorer explorer, string name = "Bucket", string settings = "{\"bucket\":\"b\"}") =>
        new(new UploaderInstance { InstanceId = Guid.NewGuid().ToString(), ProviderId = "s3", DisplayName = name, SettingsJson = settings }, explorer, "Amazon S3");

    private static async Task<(MediaBrowserViewModel Vm, MemoryExplorer Store, ScriptedDialogs Dialogs)> OpenAsync(ExplorerCapabilities capabilities = Full)
    {
        var store = new MemoryExplorer(capabilities);
        store.Folders.Add("photos");
        store.Folders.Add("photos/2026");
        store.Files["readme.txt"] = new byte[10];
        store.Files["b.png"] = new byte[300];
        store.Files["a.png"] = new byte[20];
        store.Files["photos/cat.jpg"] = new byte[5];
        var dialogs = new ScriptedDialogs();
        var vm = new MediaBrowserViewModel([Source(store)], dialogs);
        await vm.InitializeAsync();
        return (vm, store, dialogs);
    }

    private static string[] Names(MediaBrowserViewModel vm) => vm.Items.Select(i => i.Name).ToArray();

    [Test]
    public async Task ListsFoldersFirstThenFilesByName()
    {
        (MediaBrowserViewModel vm, _, _) = await OpenAsync();
        Assert.Multiple(() =>
        {
            Assert.That(Names(vm), Is.EqualTo(new[] { "photos", "a.png", "b.png", "readme.txt" }));
            Assert.That(vm.StatusText, Does.StartWith("3 files, 1 folder"));
            Assert.That(vm.CanGoUp, Is.False);
            Assert.That(vm.IsEmpty, Is.False);
        });
    }

    [Test]
    public async Task SortsByColumnAndTogglesDirectionKeepingFoldersFirst()
    {
        (MediaBrowserViewModel vm, _, _) = await OpenAsync();
        vm.SortByCommand.Execute(MediaBrowserSortColumn.Size);
        Assert.That(Names(vm), Is.EqualTo(new[] { "photos", "readme.txt", "a.png", "b.png" }));
        vm.SortByCommand.Execute(MediaBrowserSortColumn.Size);
        Assert.That(Names(vm), Is.EqualTo(new[] { "photos", "b.png", "a.png", "readme.txt" }));
        Assert.That(vm.IsSizeSorted, Is.True);
    }

    [Test]
    public async Task SearchAndTypeFilterApplyInstantly()
    {
        (MediaBrowserViewModel vm, _, _) = await OpenAsync();
        vm.SearchText = "PNG";
        Assert.That(Names(vm), Is.EqualTo(new[] { "a.png", "b.png" }));
        vm.SearchText = "";
        vm.TypeFilter = MediaBrowserTypeFilter.Documents;
        Assert.That(Names(vm), Is.EqualTo(new[] { "photos", "readme.txt" }), "folders stay visible for navigation");
        vm.SearchText = "zzz";
        Assert.That(vm.IsEmpty, Is.True);
        Assert.That(vm.EmptyMessage, Is.EqualTo("No items match the filter"));
    }

    [Test]
    public async Task NavigatesIntoFoldersWithHistoryAndBreadcrumbs()
    {
        (MediaBrowserViewModel vm, _, _) = await OpenAsync();
        await vm.OpenCommand.ExecuteAsync(vm.Items[0]);
        Assert.That(vm.CurrentPath, Is.EqualTo("photos"));
        Assert.That(Names(vm), Is.EqualTo(new[] { "2026", "cat.jpg" }));
        Assert.That(vm.Breadcrumbs.Select(c => c.Label), Is.EqualTo(new[] { "Bucket", "photos" }));

        await vm.GoUpCommand.ExecuteAsync(null);
        Assert.That(vm.CurrentPath, Is.EqualTo(""));
        await vm.GoBackCommand.ExecuteAsync(null);
        Assert.That(vm.CurrentPath, Is.EqualTo("photos"));
        await vm.GoForwardCommand.ExecuteAsync(null);
        Assert.That(vm.CurrentPath, Is.EqualTo(""));
    }

    [Test]
    public async Task UploadsIntoTheCurrentFolderAndRefreshes()
    {
        (MediaBrowserViewModel vm, MemoryExplorer store, _) = await OpenAsync();
        await vm.OpenCommand.ExecuteAsync(vm.Items[0]);
        await vm.UploadAsync([new MediaBrowserUploadSource("new.gif", () => Task.FromResult<Stream>(new MemoryStream(new byte[7])))]);
        Assert.That(store.Files.ContainsKey("photos/new.gif"), Is.True);
        Assert.That(Names(vm), Does.Contain("new.gif"));
        Assert.That(vm.StatusText, Is.EqualTo("Uploaded new.gif"));
    }

    [Test]
    public async Task CreateFolderPassesInstanceSettingsAndRejectsDuplicates()
    {
        (MediaBrowserViewModel vm, MemoryExplorer store, ScriptedDialogs dialogs) = await OpenAsync();
        dialogs.Inputs.Enqueue("photos");
        await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.That(dialogs.Shown, Does.Contain("error:Name already exists"));

        dialogs.Inputs.Enqueue(" clips ");
        await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.That(store.Calls, Does.Contain("mkdir:|clips|{\"bucket\":\"b\"}"));
        Assert.That(Names(vm)[..2], Is.EqualTo(new[] { "clips", "photos" }));
    }

    [Test]
    public async Task RenamesTheSelectedItem()
    {
        (MediaBrowserViewModel vm, MemoryExplorer store, ScriptedDialogs dialogs) = await OpenAsync();
        vm.SelectedItem = vm.Items.Single(i => i.Name == "readme.txt");
        dialogs.Inputs.Enqueue("notes.txt");
        await vm.RenameCommand.ExecuteAsync(null);
        Assert.That(store.Files.ContainsKey("notes.txt"), Is.True);
        Assert.That(Names(vm), Does.Contain("notes.txt").And.Not.Contain("readme.txt"));
    }

    [Test]
    public async Task DeletingANonEmptyFolderAsksTwice()
    {
        (MediaBrowserViewModel vm, MemoryExplorer store, ScriptedDialogs dialogs) = await OpenAsync();
        vm.SelectedItem = vm.Items.Single(i => i.Name == "photos");
        dialogs.Confirmations.Enqueue(true);
        dialogs.Confirmations.Enqueue(false); // decline the non-empty warning
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.That(store.Folders, Does.Contain("photos"));
        Assert.That(dialogs.Shown, Does.Contain("confirm:Delete non-empty folder"));

        dialogs.Confirmations.Enqueue(true);
        dialogs.Confirmations.Enqueue(true);
        await vm.DeleteCommand.ExecuteAsync(vm.Items.Single(i => i.Name == "photos"));
        Assert.That(store.Folders, Does.Not.Contain("photos"));
        Assert.That(store.Files.Keys, Has.None.StartsWith("photos/"));
    }

    [Test]
    public async Task CapabilitiesGateActions()
    {
        (MediaBrowserViewModel vm, MemoryExplorer store, ScriptedDialogs dialogs) = await OpenAsync(ExplorerCapabilities.Download | ExplorerCapabilities.Url);
        vm.SelectedItem = vm.Items.Single(i => i.Name == "a.png");
        Assert.Multiple(() =>
        {
            Assert.That(vm.CanUpload, Is.False);
            Assert.That(vm.CanCreateFolder, Is.False);
            Assert.That(vm.CanRename, Is.False);
            Assert.That(vm.CanDelete, Is.False);
            Assert.That(vm.CanDownload, Is.True);
            Assert.That(vm.CanUseUrl, Is.True);
            Assert.That(vm.CanPreview, Is.True);
        });

        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.That(store.Files.ContainsKey("a.png"), Is.True);
        Assert.That(dialogs.Shown, Is.Empty);
    }

    [Test]
    public async Task DownloadsIntoTheGivenStream()
    {
        (MediaBrowserViewModel vm, _, _) = await OpenAsync();
        var target = new MemoryStream();
        Assert.That(await vm.DownloadAsync(vm.Items.Single(i => i.Name == "b.png"), target), Is.True);
        Assert.That(target.Length, Is.EqualTo(300));
    }

    [Test]
    public async Task PagesWithLoadMore()
    {
        var store = new MemoryExplorer(Full) { PageSize = 2 };
        for (int i = 0; i < 5; i++) store.Files[$"f{i}.txt"] = new byte[1];
        var vm = new MediaBrowserViewModel([Source(store)], new ScriptedDialogs());
        await vm.InitializeAsync();
        Assert.That((vm.Items.Count, vm.HasMoreItems), Is.EqualTo((2, true)));
        await vm.LoadMoreCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.That((vm.Items.Count, vm.HasMoreItems), Is.EqualTo((5, false)));
    }

    [Test]
    public async Task ProviderFailuresSurfaceInTheErrorBanner()
    {
        var failing = new FailingExplorer();
        var vm = new MediaBrowserViewModel([Source(failing)], new ScriptedDialogs());
        await vm.InitializeAsync();
        Assert.That(vm.HasError, Is.True);
        Assert.That(vm.ErrorText, Does.Contain("AccessDenied"));
        Assert.That(vm.IsBusy, Is.False);
    }

    private sealed class FailingExplorer : IUploaderExplorer
    {
        public bool SupportsFolders => true;
        public Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default) => throw new InvalidOperationException("S3 request failed: AccessDenied");
        public Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default) => Task.FromResult<byte[]?>(null);
        public Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default) => Task.FromResult<Stream?>(null);
        public Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default) => Task.FromResult(false);
        public Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default) => Task.FromResult(false);
    }

    [Test]
    public void DiscoverySkipsNonBrowsableProvidersAndCollapsesDuplicateAccounts()
    {
        var store = new MemoryExplorer(Full);
        var plain = new NonExplorerProvider();
        var browsable = new ExplorerProvider(store);
        UploaderInstance[] instances =
        [
            new() { InstanceId = "1", ProviderId = "s3", DisplayName = "Images", SettingsJson = "{\"a\":1}" },
            new() { InstanceId = "2", ProviderId = "s3", DisplayName = "Files", SettingsJson = "{\"a\":1}" },
            new() { InstanceId = "3", ProviderId = "s3", DisplayName = "Other bucket", SettingsJson = "{\"a\":2}" },
            new() { InstanceId = "4", ProviderId = "imgbb", DisplayName = "ImgBB", SettingsJson = "{}" },
        ];

        IReadOnlyList<MediaBrowserSource> sources = MediaBrowserSource.Discover(instances, id => id == "s3" ? browsable : plain);
        Assert.That(sources.Select(s => s.Instance.InstanceId), Is.EqualTo(new[] { "1", "3" }));
        Assert.That(sources[1].DisplayName, Is.EqualTo("Browsable — Other bucket"));
    }

    [TestCase("a/b/c", "a/b")]
    [TestCase("a/b/c/", "a/b/")]
    [TestCase("a", "")]
    [TestCase("a/", "")]
    public void ParentKeepsTheProviderPathConvention(string path, string parent) =>
        Assert.That(MediaBrowserViewModel.ParentOf(path), Is.EqualTo(parent));

    private class NonExplorerProvider : UploaderProviderBase
    {
        public override string ProviderId => "imgbb";
        public override string Name => "ImgBB";
        public override string Description => "";
        public override Version Version => new(1, 0);
        public override UploaderCategory[] SupportedCategories => [UploaderCategory.Image];
        public override Type ConfigModelType => typeof(object);
        public override XerahS.Uploaders.Uploader CreateInstance(string settingsJson) => throw new NotSupportedException();
        public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() => new();
    }

    private sealed class ExplorerProvider(MemoryExplorer inner) : NonExplorerProvider, IUploaderExplorer
    {
        public override string ProviderId => "s3";
        public override string Name => "Browsable";
        public bool SupportsFolders => true;
        public Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default) => inner.ListAsync(query, cancellation);
        public Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default) => inner.GetThumbnailAsync(item, maxWidthPx, cancellation);
        public Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default) => inner.GetContentAsync(item, cancellation);
        public Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default) => inner.DeleteAsync(item, cancellation);
        public Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default) => inner.CreateFolderAsync(parentPath, folderName, cancellation);
    }
}
