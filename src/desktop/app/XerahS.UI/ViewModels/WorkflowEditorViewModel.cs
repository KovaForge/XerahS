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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using System.Diagnostics;

namespace XerahS.UI.ViewModels;

public partial class WorkflowEditorViewModel : ViewModelBase
{
    // Prevents writing selection back while we are initializing/reloading the list
    private bool _isLoadingSelection;
    private readonly WorkflowSettings _sourceModel;
    private readonly bool _loadUploaderCategories;
    private bool _descriptionAutoSyncEnabled;
    private static readonly JsonSerializerSettings CloneJsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Converters = new List<JsonConverter>
        {
            new StringEnumConverter(),
            new XerahS.Common.Converters.SkColorJsonConverter()
        }
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkflowId))]
    private WorkflowSettings _model;

    public string WorkflowId => Model.Id;


    [ObservableProperty]
    private Key _selectedKey;

    [ObservableProperty]
    private KeyModifiers _selectedModifiers;

    [ObservableProperty]
    private WorkflowType _selectedJob;



    // Destinations
    public ObservableCollection<UploaderInstanceViewModel> AvailableDestinations { get; } = new();

    [ObservableProperty]
    private UploaderInstanceViewModel? _selectedDestination;

    private CategoryViewModel _imageCategory = null!;
    private CategoryViewModel _textCategory = null!;
    private CategoryViewModel _fileCategory = null!;
    private CategoryViewModel _urlCategory = null!;

    public string SelectedJobDescription => EnumExtensions.GetDescription(SelectedJob);

    public string WindowTitle
    {
        get
        {
            var baseTitle = Model.HotkeyInfo.Id == 0 ? "Add Workflow" : "Edit Workflow";
            var desc = Description;
            if (string.IsNullOrEmpty(desc))
            {
                desc = EnumExtensions.GetDescription(Model.Job);
            }
            return $"{baseTitle} - {desc}";
        }
    }

    public string Description
    {
        get => Model.TaskSettings.Description;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (Model.TaskSettings.Description != normalizedValue)
            {
                Model.TaskSettings.Description = normalizedValue;
                _descriptionAutoSyncEnabled = ShouldAutoSyncDescription(normalizedValue, SelectedJob);
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    // Categories
    public ObservableCollection<JobCategoryViewModel> JobCategories { get; } = new();

    [ObservableProperty]
    private JobCategoryViewModel? _selectedJobCategory;

    [ObservableProperty]
    private HotkeyItemViewModel? _selectedJobItem;

    partial void OnSelectedJobItemChanged(HotkeyItemViewModel? value)
    {
        if (value != null)
        {
            SelectedJob = value.Model.Job;
        }
    }

    // Sub-ViewModels
    public TaskSettingsViewModel TaskSettings { get; private set; }
    public IndexFolderViewModel IndexFolderConfig { get; }

    public WorkflowEditorViewModel(WorkflowSettings model, bool loadUploaderCategories = true)
    {
        var sw = Stopwatch.StartNew();
        DebugHelper.WriteLine($"[WorkflowEditorVM] ctor start. Job={model.Job}, Id={model.Id}");

        _sourceModel = model ?? throw new ArgumentNullException(nameof(model));
        _loadUploaderCategories = loadUploaderCategories;
        _model = CloneWorkflow(model);
        _model.TaskSettings ??= new TaskSettings();
        _model.EnsureId();
        _descriptionAutoSyncEnabled = ShouldAutoSyncDescription(_model.TaskSettings.Description, _model.Job);
        _selectedKey = _model.HotkeyInfo.Key;
        _selectedModifiers = _model.HotkeyInfo.Modifiers;
        _selectedJob = _model.Job;
        LogStep(sw, "basic fields set");

        // Initialize TaskSettings VM
        TaskSettings = new TaskSettingsViewModel(_model.TaskSettings);
        IndexFolderConfig = new IndexFolderViewModel(_model.TaskSettings, true);
        LogStep(sw, "task settings viewmodels created");

        LoadJobCategories();
        LogStep(sw, $"job categories loaded: {JobCategories.Count}");

        // Select the current job from the category tree
        SelectJobInCategories(_model.Job);
        LogStep(sw, $"job selected: {SelectedJob}");

        _isLoadingSelection = true;
        if (_loadUploaderCategories)
        {
            InitializeCategories();
            LogStep(sw, "uploader categories initialized");
            UpdateDestinations();
            LogStep(sw, $"destinations updated: {AvailableDestinations.Count}");
            LoadSelectedDestination();
            LogStep(sw, $"selected destination loaded: {SelectedDestination?.DisplayName ?? "none"}");
        }
        _isLoadingSelection = false;

        LogStep(sw, "ctor end");
    }

    private void InitializeCategories()
    {
        DebugHelper.WriteLine("[WorkflowEditorVM] InitializeCategories start");
        _imageCategory = new CategoryViewModel("Image Uploaders", UploaderCategory.Image);
        _imageCategory.LoadInstances();

        _textCategory = new CategoryViewModel("Text Uploaders", UploaderCategory.Text);
        _textCategory.LoadInstances();

        _fileCategory = new CategoryViewModel("File Uploaders", UploaderCategory.File);
        _fileCategory.LoadInstances();

        _urlCategory = new CategoryViewModel("URL Shorteners", UploaderCategory.UrlShortener);
        _urlCategory.LoadInstances();
        DebugHelper.WriteLine($"[WorkflowEditorVM] InitializeCategories end: image={_imageCategory.Instances.Count}, text={_textCategory.Instances.Count}, file={_fileCategory.Instances.Count}, url={_urlCategory.Instances.Count}");
    }

    partial void OnSelectedJobChanged(WorkflowType value)
    {
        _isLoadingSelection = true;
        TaskSettings.Job = value;
        if (_loadUploaderCategories)
        {
            UpdateDestinations();
            LoadSelectedDestination();
        }
        _isLoadingSelection = false;

        if (_descriptionAutoSyncEnabled)
        {
            ApplyDefaultDescriptionForSelectedJob();
        }

        OnPropertyChanged(nameof(SelectedJobDescription));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void UpdateDestinations()
    {
        if (!_loadUploaderCategories)
        {
            return;
        }

        DebugHelper.WriteLine($"[WorkflowEditorVM] UpdateDestinations start. Job={SelectedJob}");
        if (_imageCategory == null || _textCategory == null || _fileCategory == null || _urlCategory == null)
        {
            InitializeCategories();
        }

        AvailableDestinations.Clear();

        string category = SelectedJob.GetHotkeyCategory();

        // Determine which destination types to show based on category
        bool showImageUploaders = false;
        bool showTextUploaders = false;
        bool showFileUploaders = false;

        switch (category)
        {
            case EnumExtensions.WorkflowType_Category_ScreenCapture:
            case EnumExtensions.WorkflowType_Category_ScreenRecord:
                showImageUploaders = true;
                showFileUploaders = true;
                break;

            case EnumExtensions.WorkflowType_Category_Upload:
                if (SelectedJob == WorkflowType.ClipboardUpload ||
                    SelectedJob == WorkflowType.ClipboardUploadWithContentViewer)
                {
                    showImageUploaders = true;
                    showTextUploaders = true;
                    showFileUploaders = true;
                }
                else if (SelectedJob == WorkflowType.FileUpload)
                {
                    showFileUploaders = true;
                }
                else
                {
                    showImageUploaders = true;
                    showFileUploaders = true;
                }
                break;

            case EnumExtensions.WorkflowType_Category_Tools:
                showImageUploaders = true;
                showFileUploaders = true;
                break;
        }

        if (showImageUploaders && _imageCategory != null)
        {
            foreach (var instance in _imageCategory.Instances)
                AvailableDestinations.Add(instance);
        }

        if (showTextUploaders && _textCategory != null)
        {
            foreach (var instance in _textCategory.Instances)
                AvailableDestinations.Add(instance);
        }

        if (showFileUploaders && _fileCategory != null)
        {
            foreach (var instance in _fileCategory.Instances)
                AvailableDestinations.Add(instance);
        }

        if (SelectedDestination == null)
        {
            SelectedDestination = AvailableDestinations.FirstOrDefault();
        }

        DebugHelper.WriteLine($"[WorkflowEditorVM] UpdateDestinations end. Count={AvailableDestinations.Count}, Selected={SelectedDestination?.DisplayName ?? "none"}");
    }



    private void LoadSelectedDestination()
    {
        if (!_loadUploaderCategories)
        {
            return;
        }

        UploaderInstanceViewModel? matched = null;
        var settings = Model;

        DebugHelper.WriteLine($"[WorkflowEditorVM] LoadSelectedDestination start. Job={SelectedJob}, Available={AvailableDestinations.Count}");

        if (settings.TaskSettings.OverrideCustomUploader)
        {
            var customList = SettingsManager.UploadersConfig.CustomUploadersList;
            if (settings.TaskSettings.CustomUploaderIndex >= 0 && settings.TaskSettings.CustomUploaderIndex < customList.Count)
            {
                var custom = customList[settings.TaskSettings.CustomUploaderIndex];
                matched = AvailableDestinations.FirstOrDefault(d => d.DisplayName == custom.Name);
                DebugHelper.WriteLine($"[WorkflowEditorVM] Matched Custom Uploader: {matched?.DisplayName}");
            }
        }
        else if (settings.TaskSettings.OverrideFTP)
        {
            var ftpList = SettingsManager.UploadersConfig.FTPAccountList;
            if (settings.TaskSettings.FTPIndex >= 0 && settings.TaskSettings.FTPIndex < ftpList.Count)
            {
                var ftp = ftpList[settings.TaskSettings.FTPIndex];
                matched = AvailableDestinations.FirstOrDefault(d => d.DisplayName == $"FTP: {ftp.Name}");
                DebugHelper.WriteLine($"[WorkflowEditorVM] Matched FTP: {matched?.DisplayName}");
            }
        }
        else
        {
            // Use the centralized instance ID stored in TaskSettings
            string? targetInstanceId = settings.TaskSettings.GetDestinationInstanceId(SelectedJob);

            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                matched = AvailableDestinations.FirstOrDefault(d =>
                    string.Equals(d.Instance.InstanceId, targetInstanceId, StringComparison.OrdinalIgnoreCase));
            }

            DebugHelper.WriteLine($"[WorkflowEditorVM] TaskSettings target instance: {targetInstanceId}. Matched: {matched?.DisplayName}");
        }

        if (matched != null)
        {
            SelectedDestination = matched;
        }
        else
        {
            DebugHelper.WriteLine("[WorkflowEditorVM] No matching destination found, keeping default.");
        }

        DebugHelper.WriteLine($"[WorkflowEditorVM] LoadSelectedDestination end. Selected={SelectedDestination?.DisplayName ?? "none"}");
    }

    public void Save()
    {
        Model.HotkeyInfo.Key = SelectedKey;
        Model.HotkeyInfo.Modifiers = SelectedModifiers;
        Model.Job = SelectedJob;

        // Ensure TaskSettings knows its job too
        if (Model.TaskSettings != null)
        {
            Model.TaskSettings.Job = SelectedJob;

            // Save Destination if selected
            if (SelectedDestination != null)
            {
                // Reset overrides first to ensure clean state
                Model.TaskSettings.OverrideCustomUploader = false;
                Model.TaskSettings.OverrideFTP = false;

                // 1. Check if it's a Custom Uploader
                var customList = SettingsManager.UploadersConfig.CustomUploadersList;
                var customIndex = customList.FindIndex(c => c.Name == SelectedDestination.DisplayName);
                
                // 2. Check if it's an FTP account (DisplayName format is "FTP: Name")
                var isFtp = SelectedDestination.DisplayName.StartsWith("FTP: ");
                
                if (customIndex >= 0)
                {
                    Model.TaskSettings.OverrideCustomUploader = true;
                    Model.TaskSettings.CustomUploaderIndex = customIndex;
                    DebugHelper.WriteLine($"Workflow saved with Custom Uploader: {SelectedDestination.DisplayName}");
                }
                else if (isFtp)
                {
                    var ftpName = SelectedDestination.DisplayName.Substring(5);
                    var ftpList = SettingsManager.UploadersConfig.FTPAccountList;
                    var ftpIndex = ftpList.FindIndex(f => f.Name == ftpName);
                    
                    if (ftpIndex >= 0)
                    {
                        Model.TaskSettings.OverrideFTP = true;
                        Model.TaskSettings.FTPIndex = ftpIndex;
                        DebugHelper.WriteLine($"Workflow saved with FTP: {ftpName}");
                    }
                }
                else if (SelectedDestination.Instance != null && !string.IsNullOrEmpty(SelectedDestination.Instance.InstanceId))
                {
                    // 3. Save the selected uploader instance ID
                    bool saved = Model.TaskSettings.SetDestinationInstanceId(SelectedJob, SelectedDestination.Instance.InstanceId);

                    if (saved)
                    {
                        DebugHelper.WriteLine($"Workflow saved destination instance: {SelectedDestination.Instance.InstanceId} for job {SelectedJob}");
                    }
                    else
                    {
                        DebugHelper.WriteLine($"Warning: Could not save destination instance {SelectedDestination.Instance.InstanceId} for job {SelectedJob}");
                    }
                }
            }
        }

        ApplyChangesToSourceModel();
    }

    partial void OnSelectedDestinationChanged(UploaderInstanceViewModel? value)
    {
        if (_isLoadingSelection)
        {
            return;
        }

        if (value?.Instance != null && Model.TaskSettings != null)
        {
            // Persist the selection immediately so closing the dialog without OK does not lose context
            Model.TaskSettings.SetDestinationInstanceId(SelectedJob, value.Instance.InstanceId);
            DebugHelper.WriteLine($"[WorkflowEditorVM] Selected destination changed to instance {value.Instance.InstanceId} for job {SelectedJob}");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedKey = Key.None;
        SelectedModifiers = KeyModifiers.None;
    }

    public string KeyText
    {
        get
        {
            if (SelectedKey == Key.None && SelectedModifiers == KeyModifiers.None)
                return "None";

            var info = new HotkeyInfo { Key = SelectedKey, Modifiers = SelectedModifiers };
            return info.ToString();
        }
    }

    partial void OnSelectedKeyChanged(Key value) => OnPropertyChanged(nameof(KeyText));
    partial void OnSelectedModifiersChanged(KeyModifiers value) => OnPropertyChanged(nameof(KeyText));

    private void LoadJobCategories()
    {
        // Group WorkflowTypes by their Category attribute
        var allTypes = Enum.GetValues(typeof(WorkflowType)).Cast<WorkflowType>()
            .Where(t => t != WorkflowType.None);

        var grouped = allTypes.GroupBy(t => t.GetHotkeyCategory())
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderBy(g => GetCategoryOrder(g.Key));

        foreach (var group in grouped)
        {
            var category = new JobCategoryViewModel(GetCategoryDisplayName(group.Key), group);
            JobCategories.Add(category);
        }
    }

    private void SelectJobInCategories(WorkflowType job)
    {
        foreach (var category in JobCategories)
        {
            var item = category.Jobs.FirstOrDefault(j => j.Model.Job == job);
            if (item != null)
            {
                SelectedJobCategory = category;
                SelectedJobItem = item;
                break;
            }
        }

        // If not found (e.g. None), maybe select first generic
        if (SelectedJobItem == null && JobCategories.Count > 0)
        {
            SelectedJobCategory = JobCategories[0];
            SelectedJobItem = SelectedJobCategory.Jobs.FirstOrDefault();
        }
    }



    private string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            EnumExtensions.WorkflowType_Category_Upload => "Upload",
            EnumExtensions.WorkflowType_Category_ScreenCapture => "Screen Capture",
            EnumExtensions.WorkflowType_Category_ScreenRecord => "Screen Record",
            EnumExtensions.WorkflowType_Category_Tools => "Tools",
            EnumExtensions.WorkflowType_Category_Other => "Other",
            _ => category
        };
    }

    private int GetCategoryOrder(string category)
    {
        return category switch
        {
            EnumExtensions.WorkflowType_Category_ScreenCapture => 0,
            EnumExtensions.WorkflowType_Category_ScreenRecord => 1,
            EnumExtensions.WorkflowType_Category_Upload => 2,
            EnumExtensions.WorkflowType_Category_Tools => 3,
            EnumExtensions.WorkflowType_Category_Other => 4,
            _ => 99
        };
    }

    private static void LogStep(Stopwatch sw, string message)
    {
        DebugHelper.WriteLine($"[WorkflowEditorVM] {message} (+{sw.ElapsedMilliseconds}ms)");
    }

    private static WorkflowSettings CloneWorkflow(WorkflowSettings source)
    {
        string json = JsonConvert.SerializeObject(source, CloneJsonSettings);
        return JsonConvert.DeserializeObject<WorkflowSettings>(json, CloneJsonSettings) ?? new WorkflowSettings();
    }

    private void ApplyChangesToSourceModel()
    {
        var snapshot = CloneWorkflow(Model);
        snapshot.TaskSettings ??= new TaskSettings();
        snapshot.EnsureId();

        _sourceModel.Id = snapshot.Id;
        _sourceModel.Enabled = snapshot.Enabled;
        _sourceModel.PinnedToTray = snapshot.PinnedToTray;
        _sourceModel.HotkeyInfo = snapshot.HotkeyInfo ?? new HotkeyInfo();
        _sourceModel.TaskSettings = snapshot.TaskSettings;
        _sourceModel.TaskSettings.Job = snapshot.Job;
        _sourceModel.EnsureId();
    }

    private void ApplyDefaultDescriptionForSelectedJob()
    {
        string defaultDescription = GetDefaultDescriptionForJob(SelectedJob);
        if (string.Equals(Model.TaskSettings.Description, defaultDescription, StringComparison.Ordinal))
        {
            return;
        }

        Model.TaskSettings.Description = defaultDescription;
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private static bool ShouldAutoSyncDescription(string? description, WorkflowType job)
    {
        return string.IsNullOrWhiteSpace(description) ||
               string.Equals(description, GetDefaultDescriptionForJob(job), StringComparison.Ordinal) ||
               string.Equals(description, EnumExtensions.GetDescription(job), StringComparison.Ordinal);
    }

    private static string GetDefaultDescriptionForJob(WorkflowType job)
    {
        string? preferredDescription = WorkflowsConfig
            .GetDefaultWorkflowList()
            .FirstOrDefault(workflow => workflow.Job == job)?
            .TaskSettings.Description;

        return string.IsNullOrWhiteSpace(preferredDescription)
            ? EnumExtensions.GetDescription(job)
            : preferredDescription;
    }
}
