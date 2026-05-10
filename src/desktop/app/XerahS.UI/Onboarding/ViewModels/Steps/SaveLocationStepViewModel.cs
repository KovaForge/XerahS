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
        ? string.Empty
        : CreateDateSubfolders
            ? Path.Combine(SelectedPath, DateTime.Now.ToString("yyyy-MM-dd"))
            : SelectedPath;

    public bool HasPathPreview => !string.IsNullOrEmpty(PathPreview);

    public PathPreset? PicturesPreset => QuickSelectPaths.ElementAtOrDefault(0);

    public PathPreset? DesktopPreset => QuickSelectPaths.ElementAtOrDefault(1);

    public PathPreset? DocumentsPreset => QuickSelectPaths.ElementAtOrDefault(2);

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
        string picturesPath = GetDefaultPicturesPath();
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        QuickSelectPaths.Add(new PathPreset("Pictures / Screenshots", Path.Combine(picturesPath, "Screenshots"), "folder"));
        QuickSelectPaths.Add(new PathPreset("Desktop", desktopPath, "desktop"));
        QuickSelectPaths.Add(new PathPreset("Documents", documentsPath, "document"));
    }

    private void SetDefaultPath()
    {
        SelectedPath = IsFlatpakSandbox()
            ? PathsManager.ScreenshotsFolder
            : Path.Combine(GetDefaultPicturesPath(), "Screenshots");
        _ = TestPathAsync();
    }

    private static string GetDefaultPicturesPath()
    {
        string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return string.IsNullOrWhiteSpace(picturesPath)
            ? LinuxXdgDirectories.Detect().PicturesDirectory
            : picturesPath;
    }

    private static bool IsFlatpakSandbox()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")) ||
            File.Exists("/.flatpak-info");
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (BrowseFolderCallback == null)
        {
            return;
        }

        string? result = await BrowseFolderCallback();
        if (!string.IsNullOrEmpty(result))
        {
            SelectedPath = result;
            await TestPathAsync();
        }
    }

    /// <summary>
    /// Callback for folder browsing. Set by the View.
    /// </summary>
    public Func<Task<string?>>? BrowseFolderCallback { get; set; }

    public async Task<bool> TestPathAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPath))
        {
            IsPathWritable = false;
            PathError = "Please select a folder path.";
            SetValidationState(false, PathError);
            return false;
        }

        try
        {
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
                    SetValidationState(false, PathError);
                    return false;
                }
            }

            string testFile = Path.Combine(SelectedPath, $".xerahs_test_{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                IsPathWritable = true;
                PathError = null;
                SetValidationState(true);
                return true;
            }
            catch (Exception ex)
            {
                IsPathWritable = false;
                PathError = $"Directory is not writable: {ex.Message}";
                SetValidationState(false, PathError);
                return false;
            }
        }
        catch (Exception ex)
        {
            IsPathWritable = false;
            PathError = $"Error testing path: {ex.Message}";
            SetValidationState(false, PathError);
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
        if (!string.IsNullOrWhiteSpace(state.ScreenshotsFolder))
        {
            SelectedPath = state.ScreenshotsFolder;
        }

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
        bool isValid = !string.IsNullOrWhiteSpace(SelectedPath) && IsPathWritable;
        SetValidationState(isValid, isValid ? null : PathError ?? "Select a writable folder.");
        return isValid;
    }

    partial void OnSelectedPathChanged(string value)
    {
        OnPropertyChanged(nameof(PathPreview));
        OnPropertyChanged(nameof(HasPathPreview));
        _ = TestPathAsync();
    }

    partial void OnCreateDateSubfoldersChanged(bool value)
    {
        OnPropertyChanged(nameof(PathPreview));
        OnPropertyChanged(nameof(HasPathPreview));
    }
}
