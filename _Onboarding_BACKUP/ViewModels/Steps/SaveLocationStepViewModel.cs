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

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Path preset for quick selection.
/// </summary>
public record PathPreset(string Name, string Path, string Icon);

/// <summary>
/// Step 2: Save Location Configuration
/// </summary>
public partial class SaveLocationStepViewModel : StepViewModelBase
{
    [ObservableProperty]
    private string _selectedPath = "";

    [ObservableProperty]
    private bool _createDateSubfolders = true;

    [ObservableProperty]
    private bool _isPathWritable = true;

    [ObservableProperty]
    private string? _pathError;

    public ObservableCollection<PathPreset> QuickSelectPaths { get; } = new();

    public string PathPreview => string.IsNullOrEmpty(SelectedPath)
        ? ""
        : System.IO.Path.Combine(SelectedPath, DateTime.Now.ToString("yyyy-MM-dd"));

    public SaveLocationStepViewModel()
    {
        StepTitle = "Save Location";
        StepSubtitle = "Where should screenshots be saved?";
        StepDescription = "Choose a folder where your screenshots will be stored.";
        CanSkip = true;

        InitializeQuickSelectPaths();
        SetDefaultPath();
    }

    private void InitializeQuickSelectPaths()
    {
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        QuickSelectPaths.Add(new PathPreset("Pictures / Screenshots", System.IO.Path.Combine(picturesPath, "Screenshots"), "📷"));
        QuickSelectPaths.Add(new PathPreset("Desktop", desktopPath, "🖥️"));
        QuickSelectPaths.Add(new PathPreset("Documents", documentsPath, "📄"));
    }

    private void SetDefaultPath()
    {
        var defaultPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Screenshots");
        SelectedPath = defaultPath;
        _ = TestPathAsync();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        // This will be implemented with Avalonia's StorageProvider
        // For now, we use a callback that the View will set
        if (BrowseFolderCallback != null)
        {
            var result = await BrowseFolderCallback();
            if (!string.IsNullOrEmpty(result))
            {
                SelectedPath = result;
                await TestPathAsync();
            }
        }
    }

    /// <summary>
    /// Callback for folder browsing. Set by the View.
    /// </summary>
    public Func<Task<string?>>? BrowseFolderCallback { get; set; }

    public async Task<bool> TestPathAsync()
    {
        if (string.IsNullOrEmpty(SelectedPath))
        {
            IsPathWritable = false;
            PathError = "Please select a folder path.";
            return false;
        }

        try
        {
            // Check if directory exists or can be created
            if (!Directory.Exists(SelectedPath))
            {
                try
                {
                    Directory.CreateDirectory(SelectedPath);
                }
                catch (Exception ex)
                {
                    IsPathWritable = false;
                    PathError = $"Cannot create directory: {ex.Message}";
                    return false;
                }
            }

            // Test write permissions by creating a temporary file
            var testFile = System.IO.Path.Combine(SelectedPath, $".xerahs_test_{Guid.NewGuid()}.tmp");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                IsPathWritable = true;
                PathError = null;
                return true;
            }
            catch (Exception ex)
            {
                IsPathWritable = false;
                PathError = $"Directory is not writable: {ex.Message}";
                return false;
            }
        }
        catch (Exception ex)
        {
            IsPathWritable = false;
            PathError = $"Error testing path: {ex.Message}";
            return false;
        }
    }

    [RelayCommand]
    private void SelectPreset(PathPreset preset)
    {
        SelectedPath = preset.Path;
        _ = TestPathAsync();
    }

    public override void LoadFromState(OnboardingState state)
    {
        SelectedPath = state.ScreenshotsFolder;
        CreateDateSubfolders = state.CreateDateSubfolders;
        _ = TestPathAsync();
    }

    public override void SaveToState(OnboardingState state)
    {
        state.ScreenshotsFolder = SelectedPath;
        state.CreateDateSubfolders = CreateDateSubfolders;
    }

    public override bool Validate()
    {
        return !string.IsNullOrEmpty(SelectedPath) && IsPathWritable;
    }

    partial void OnSelectedPathChanged(string value)
    {
        _ = TestPathAsync();
    }
}
