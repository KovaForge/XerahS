using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Core.Hotkeys;
using XerahS.Core.Services;
using XerahS.Core.Tasks;
using XerahS.History;
using XerahS.McpServer.Transport;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.McpServer.Runtime;

public sealed class XerahSMcpRuntime : IXerahSMcpRuntime
{
    internal const long MaxInlineHistoryBlobBytes = 5 * 1024 * 1024;

    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private IServiceProvider? _services;
    private IDesktopTaskManager? _taskManager;

    public string ServerVersion => Server.Capabilities.ServerVersion;

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return SettingsManager.Settings.McpApiKey;
    }

    public async Task<JsonObject> CaptureRegionAsync(string? workflowId, int? monitor, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = CreateCaptureTaskSettings(workflowId, WorkflowType.RectangleRegion);
        var selection = await PlatformServices.ScreenCapture.SelectRegionAsync(CreateCaptureOptions(settings));

        if (selection.IsEmpty)
        {
            throw new McpUserCancelledException("Capture cancelled by user.");
        }

        if (monitor != null)
        {
            var screens = PlatformServices.Screen.GetAllScreens();
            if (monitor < 0 || monitor >= screens.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(monitor), "The requested monitor index is out of range.");
            }

            var bounds = screens[monitor.Value].Bounds;
            var intersected = new SkiaSharp.SKRectI(
                Math.Max(selection.Left, bounds.Left),
                Math.Max(selection.Top, bounds.Top),
                Math.Min(selection.Right, bounds.Right),
                Math.Min(selection.Bottom, bounds.Bottom));

            if (intersected.Width <= 0 || intersected.Height <= 0)
            {
                throw new InvalidOperationException("The selected region does not intersect the requested monitor.");
            }

            selection = intersected;
        }

        using var image = await PlatformServices.ScreenCapture.CaptureRectAsync(selection, CreateCaptureOptions(settings))
            ?? throw new InvalidOperationException("XerahS failed to capture the selected region.");
        var savedPath = SaveImageToFile(image, settings);
        AppendHistoryItem(savedPath, "Image");

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["monitor"] = monitor,
            ["workflow_id"] = workflowId
        };
    }

    public async Task<JsonObject> CaptureWindowAsync(string? windowTitle, bool includeDecoration, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = CreateCaptureTaskSettings(null, WorkflowType.ActiveWindow);
        settings.CaptureSettings.CaptureClientArea = !includeDecoration;

        var handle = string.IsNullOrWhiteSpace(windowTitle)
            ? PlatformServices.Window.GetForegroundWindow()
            : PlatformServices.Window.SearchWindow(windowTitle);

        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("No matching window was found.");
        }

        using var image = await PlatformServices.ScreenCapture.CaptureWindowAsync(handle, PlatformServices.Window, CreateCaptureOptions(settings))
            ?? throw new InvalidOperationException("XerahS failed to capture the requested window.");
        var savedPath = SaveImageToFile(image, settings);
        AppendHistoryItem(savedPath, "Image", null, PlatformServices.Window.GetWindowText(handle), ResolveProcessName(handle));

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["window_title"] = PlatformServices.Window.GetWindowText(handle),
            ["include_decoration"] = includeDecoration
        };
    }

    public async Task<JsonObject> CaptureFullScreenAsync(int? monitor, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = CreateCaptureTaskSettings(null, WorkflowType.PrintScreen);
        using var image = await CaptureFullScreenBitmapAsync(monitor, settings);
        var savedPath = SaveImageToFile(image, settings);
        AppendHistoryItem(savedPath, "Image");

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["monitor"] = monitor
        };
    }

    public async Task<JsonObject> CaptureScrollingAsync(string scrollDirection, int maxFrames, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        scrollDirection = string.IsNullOrWhiteSpace(scrollDirection) ? "down" : scrollDirection.Trim().ToLowerInvariant();
        if (!string.Equals(scrollDirection, "down", StringComparison.Ordinal))
        {
            throw new NotSupportedException("capture_scrolling currently supports only scroll_direction='down'.");
        }

        maxFrames = Math.Clamp(maxFrames, 1, 1000);

        if (PlatformServices.ScrollingCapture == null || !PlatformServices.ScrollingCapture.IsSupported)
        {
            throw new InvalidOperationException("Scrolling capture is not supported on this platform.");
        }

        var handle = PlatformServices.Window.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("No active window is available for scrolling capture.");
        }

        var bounds = PlatformServices.Window.GetWindowClientBounds(handle);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("The active window does not expose a capturable client area.");
        }

        var settings = CreateCaptureTaskSettings(null, WorkflowType.ScrollingCapture);
        var options = settings.CaptureSettings.ScrollingCaptureOptions ?? new ScrollingCaptureOptions();
        var manager = new ScrollingCaptureManager(
            PlatformServices.ScrollingCapture,
            PlatformServices.ScreenCapture,
            PlatformServices.Window);

        var result = await manager.CaptureAsync(
            handle,
            new SkiaSharp.SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom),
            options.ScrollMethod,
            scrollAmount: options.ScrollAmount,
            startDelayMs: options.StartDelay,
            scrollDelayMs: options.ScrollDelay,
            maxFrames: maxFrames,
            autoScrollTop: true,
            autoIgnoreBottomEdge: options.AutoIgnoreBottomEdge,
            cancellationToken: cancellationToken);

        using var image = result.Image ?? throw new InvalidOperationException("Scrolling capture did not produce an image.");
        var savedPath = SaveImageToFile(image, settings);
        AppendHistoryItem(savedPath, "Image", null, PlatformServices.Window.GetWindowText(handle), ResolveProcessName(handle));

        return new JsonObject
        {
            ["file_path"] = savedPath,
            ["url"] = null,
            ["frames_captured"] = result.FramesCaptured,
            ["status"] = result.Status.ToString(),
            ["scroll_direction"] = scrollDirection,
            ["max_frames"] = maxFrames
        };
    }

    public async Task<JsonObject> AnnotateImageAsync(string? imagePath, JsonArray? annotations, bool autoSave, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("image_path is required.", nameof(imagePath));
        }

        var absolutePath = Path.GetFullPath(imagePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The specified image_path does not exist.", absolutePath);
        }

        using var bitmap = SkiaSharp.SKBitmap.Decode(absolutePath)
            ?? throw new InvalidOperationException("The specified image could not be decoded.");
        var applied = annotations != null ? SkiaAnnotationRenderer.ApplyAnnotations(bitmap, annotations) : Array.Empty<string>();
        var outputPath = GetAnnotatedOutputPath(absolutePath);
        SaveBitmap(bitmap, outputPath);

        return new JsonObject
        {
            ["input_path"] = absolutePath,
            ["output_path"] = outputPath,
            ["auto_save"] = autoSave,
            ["annotations_applied"] = ToJsonArray(applied),
            ["interactive_editor_available"] = false
        };
    }

    public async Task<JsonObject> UploadFileAsync(string? filePath, string? destination, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("file_path is required.", nameof(filePath));
        }

        var absolutePath = Path.GetFullPath(filePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The specified file_path does not exist.", absolutePath);
        }

        var settings = CreateUploadTaskSettings(absolutePath, destination);
        var task = await RunTaskAsync(
            () => _taskManager!.StartFileTask(settings, absolutePath),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        var url = task.Info.Metadata?.UploadURL;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(task.Error?.Message ?? "Upload failed.");
        }

        var fileInfo = new FileInfo(absolutePath);
        return new JsonObject
        {
            ["url"] = url,
            ["filename"] = fileInfo.Name,
            ["size_bytes"] = fileInfo.Length,
            ["destination"] = ResolveDestinationSummary(settings.DestinationInstanceId)
        };
    }

    public async Task<JsonObject> UploadClipboardAsync(string? destination, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (PlatformServices.Clipboard.ContainsImage())
        {
            using var image = PlatformServices.Clipboard.GetImage();
            if (image == null)
            {
                throw new InvalidOperationException("Clipboard reported image data but no image could be read.");
            }

            var settings = CreateUploadTaskSettings("clipboard.png", destination, UploaderCategory.Image);
            var task = await RunTaskAsync(
                () => _taskManager!.StartImageUploadTask(settings, image.Copy()),
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return CreateUploadTaskResult(task, "clipboard.png");
        }

        if (PlatformServices.Clipboard.ContainsText())
        {
            var text = await PlatformServices.Clipboard.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Clipboard text was empty.");
            }

            var settings = CreateUploadTaskSettings("clipboard.txt", destination, UploaderCategory.Text);
            var task = await RunTaskAsync(
                () => _taskManager!.StartTextTask(settings, text),
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return CreateUploadTaskResult(task, "clipboard.txt");
        }

        if (PlatformServices.Clipboard.ContainsFileDropList())
        {
            var files = await PlatformServices.Clipboard.GetFileDropListAsync();
            var firstFile = files?.FirstOrDefault(path => File.Exists(path));
            if (!string.IsNullOrWhiteSpace(firstFile))
            {
                return await UploadFileAsync(firstFile, destination, cancellationToken);
            }
        }

        throw new InvalidOperationException("Clipboard does not currently contain supported image, text, or file data.");
    }

    public async Task<JsonObject> QueryHistoryAsync(string? query, string? fromDate, string? toDate, string fileType, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        List<HistoryItem> historyItems = LoadHistoryItems();
        Dictionary<long, string> indexedTexts = new HistoryOcrIndexStore(SettingsManager.GetHistoryFilePath())
            .GetTexts(historyItems.Select(item => item.Id));

        var filtered = historyItems
            .Where(item => MatchesDate(item, fromDate, toDate))
            .Where(item => MatchesFileType(item, fileType))
            .Where(item => MatchesQuery(item, query, indexedTexts.TryGetValue(item.Id, out string? ocrText) ? ocrText : null))
            .OrderByDescending(item => item.DateTime)
            .ToList();

        var boundedLimit = Math.Clamp(limit, 1, 100);
        var page = filtered
            .Take(boundedLimit)
            .Select(item => CreateHistorySummary(item, indexedTexts.TryGetValue(item.Id, out string? ocrText) ? ocrText : null))
            .Cast<JsonNode>()
            .ToArray();

        return new JsonObject
        {
            ["items"] = new JsonArray(page),
            ["total_count"] = filtered.Count,
            ["has_more"] = filtered.Count > boundedLimit
        };
    }

    public async Task<JsonObject> GetHistoryItemAsync(string? id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await CreateHistoryDetailsAsync(FindHistoryItem(id), cancellationToken);
    }

    public async Task<JsonObject> ListWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var workflows = SettingsManager.WorkflowsConfig?.Hotkeys ?? [];
        return new JsonObject
        {
            ["workflows"] = new JsonArray(workflows.Select(workflow => new JsonObject
            {
                ["id"] = workflow.Id,
                ["name"] = string.IsNullOrWhiteSpace(workflow.Name) ? workflow.ToString() : workflow.Name,
                ["job"] = workflow.Job.ToString(),
                ["capture_mode"] = InferCaptureMode(workflow.Job),
                ["after_capture"] = FlagsToNames(workflow.TaskSettings.AfterCaptureJob),
                ["after_upload"] = FlagsToNames(workflow.TaskSettings.AfterUploadJob),
                ["enabled"] = workflow.Enabled,
                ["pinned_to_tray"] = workflow.PinnedToTray
            }).Cast<JsonNode>().ToArray()),
            ["count"] = workflows.Count
        };
    }

    public async Task<JsonObject> GetSettingsAsync(string? category, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var normalized = category?.Trim().ToLowerInvariant();
        JsonNode settings = normalized switch
        {
            "capture" => CreateCaptureSettings(),
            "upload" => CreateUploadSettings(),
            "history" => CreateHistorySettings(),
            "general" => CreateGeneralSettings(),
            "integration" => CreateIntegrationSettings(),
            null or "" => new JsonObject
            {
                ["capture"] = CreateCaptureSettings(),
                ["upload"] = CreateUploadSettings(),
                ["history"] = CreateHistorySettings(),
                ["general"] = CreateGeneralSettings(),
                ["integration"] = CreateIntegrationSettings()
            },
            _ => throw new ArgumentException($"Unknown settings category: {category}")
        };

        return new JsonObject
        {
            ["category"] = string.IsNullOrWhiteSpace(normalized) ? "all" : normalized,
            ["settings"] = settings
        };
    }

    public async Task<JsonObject> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (uri.StartsWith("xerahs://history/thumb/", StringComparison.OrdinalIgnoreCase))
        {
            var item = FindHistoryItem(uri["xerahs://history/thumb/".Length..]);
            string blobPath;
            try
            {
                blobPath = ResolveHistoryBlobPath(item);
            }
            catch (FileNotFoundException)
            {
                return CreateHistoryBlobMissingResponse(uri, item);
            }

            var blobInfo = new FileInfo(blobPath);
            if (blobInfo.Length > MaxInlineHistoryBlobBytes)
            {
                return CreateHistoryBlobTooLargeResponse(uri, blobPath, blobInfo.Length);
            }

            return new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["uri"] = uri,
                        ["mimeType"] = GuessMimeType(blobPath),
                        ["blob"] = Convert.ToBase64String(await File.ReadAllBytesAsync(blobPath, cancellationToken))
                    })
            };
        }

        if (IsHistorySearchResourceUri(uri))
        {
            var queryStart = uri.IndexOf('?');
            if (queryStart < 0)
            {
                return CreateJsonResource(uri, await QueryHistoryAsync(null, null, null, "all", 20, cancellationToken));
            }

            var queryString = uri[(queryStart + 1)..];
            string? query = null;
            string? fromDate = null;
            string? toDate = null;
            var limit = 20;

            var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var eqIndex = pair.IndexOf('=');
                if (eqIndex < 0)
                {
                    continue;
                }

                var key = DecodeResourceQueryComponent(pair[..eqIndex]);
                var value = DecodeResourceQueryComponent(pair[(eqIndex + 1)..]);
                if (key == null || value == null)
                {
                    continue;
                }

                if (string.Equals(key, "q", StringComparison.OrdinalIgnoreCase))
                {
                    query = string.IsNullOrWhiteSpace(value) ? null : value;
                }
                else if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
                {
                    fromDate = value;
                }
                else if (string.Equals(key, "to", StringComparison.OrdinalIgnoreCase))
                {
                    toDate = value;
                }
                else if (string.Equals(key, "limit", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var parsedLimit))
                {
                    limit = parsedLimit;
                }
            }

            return CreateJsonResource(uri, await QueryHistoryAsync(query, fromDate, toDate, "all", limit, cancellationToken));
        }

        if (uri.StartsWith("xerahs://history/", StringComparison.OrdinalIgnoreCase))
        {
            return CreateJsonResource(uri, await GetHistoryItemAsync(uri["xerahs://history/".Length..], cancellationToken));
        }

        if (uri.Equals("xerahs://capture/latest", StringComparison.OrdinalIgnoreCase))
        {
            var latest = LoadHistoryItems().OrderByDescending(item => item.DateTime).FirstOrDefault()
                ?? throw new InvalidOperationException("No capture history is available.");
            return CreateJsonResource(uri, await CreateHistoryDetailsAsync(latest, cancellationToken));
        }

        if (uri.Equals("xerahs://workflows", StringComparison.OrdinalIgnoreCase))
        {
            return CreateJsonResource(uri, await ListWorkflowsAsync(cancellationToken));
        }

        if (uri.StartsWith("xerahs://workflows/", StringComparison.OrdinalIgnoreCase))
        {
            var workflowId = uri["xerahs://workflows/".Length..];
            var workflow = SettingsManager.GetWorkflowById(workflowId)
                ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");

            return CreateJsonResource(uri, new JsonObject
            {
                ["workflow"] = new JsonObject
                {
                    ["id"] = workflow.Id,
                    ["name"] = string.IsNullOrWhiteSpace(workflow.Name) ? workflow.ToString() : workflow.Name,
                    ["job"] = workflow.Job.ToString(),
                    ["capture_mode"] = InferCaptureMode(workflow.Job),
                    ["after_capture"] = FlagsToNames(workflow.TaskSettings.AfterCaptureJob),
                    ["after_upload"] = FlagsToNames(workflow.TaskSettings.AfterUploadJob),
                    ["enabled"] = workflow.Enabled,
                    ["pinned_to_tray"] = workflow.PinnedToTray
                }
            });
        }

        if (uri.StartsWith("xerahs://settings/", StringComparison.OrdinalIgnoreCase))
        {
            return CreateJsonResource(uri, await GetSettingsAsync(uri["xerahs://settings/".Length..], cancellationToken));
        }

        if (uri.Equals("xerahs://monitors", StringComparison.OrdinalIgnoreCase))
        {
            var screens = PlatformServices.Screen.GetAllScreens();
            return CreateJsonResource(uri, new JsonObject
            {
                ["monitors"] = new JsonArray(screens.Select((screen, index) => new JsonObject
                {
                    ["index"] = index,
                    ["device_name"] = screen.DeviceName,
                    ["is_primary"] = screen.IsPrimary,
                    ["bounds"] = SerializeRectangle(screen.Bounds),
                    ["working_area"] = SerializeRectangle(screen.WorkingArea),
                    ["scale_factor"] = screen.ScaleFactor
                }).Cast<JsonNode>().ToArray())
            });
        }

        if (uri.Equals("xerahs://destinations", StringComparison.OrdinalIgnoreCase))
        {
            return CreateJsonResource(uri, CreateDestinationsResource());
        }

        throw new ArgumentException($"Unknown resource URI: {uri}");
    }

    internal static bool IsHistorySearchResourceUri(string uri)
    {
        return uri.Equals("xerahs://history/search", StringComparison.OrdinalIgnoreCase) ||
               uri.StartsWith("xerahs://history/search?", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? DecodeResourceQueryComponent(string value)
    {
        if (!HasValidPercentEncoding(value))
        {
            return null;
        }

        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private static bool HasValidPercentEncoding(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%')
            {
                continue;
            }

            if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2]))
            {
                return false;
            }

            i += 2;
        }

        return true;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_services != null && _taskManager != null && PlatformServices.IsInitialized)
        {
            return;
        }

        await _initializeGate.WaitAsync(cancellationToken);
        try
        {
            if (_services != null && _taskManager != null && PlatformServices.IsInitialized)
            {
                return;
            }

            var result = await ShareXBootstrap.InitializeAsync(new BootstrapOptions
            {
                EnableLogging = true,
                InitializeRecording = true,
                UIService = new HeadlessMcpUIService(),
                ToastService = new HeadlessMcpToastService()
            });

            _services = result.ServiceProvider ?? throw new InvalidOperationException("XerahS bootstrap did not provide a service provider.");
            _taskManager = _services.GetRequiredService<IDesktopTaskManager>();
            EnsureApiKey();
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    private static void EnsureApiKey()
    {
        if (!string.IsNullOrWhiteSpace(SettingsManager.Settings.McpApiKey))
        {
            return;
        }

        SettingsManager.Settings.McpApiKey = HttpServer.GenerateApiKey();
        SettingsManager.SaveApplicationConfig();
    }

    private static TaskSettings CreateCaptureTaskSettings(string? workflowId, WorkflowType fallbackJob)
    {
        var settings = CloneTaskSettings(ResolveWorkflowTaskSettings(workflowId));
        settings.Job = fallbackJob;
        settings.AfterCaptureJob = AfterCaptureTasks.None;
        settings.AfterUploadJob = AfterUploadTasks.None;
        settings.WorkflowId = workflowId ?? settings.WorkflowId;
        return settings;
    }

    private static TaskSettings CreateUploadTaskSettings(string filePathOrName, string? destination, UploaderCategory? forcedCategory = null)
    {
        var settings = CloneTaskSettings(SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload).TaskSettings);
        settings.Job = WorkflowType.FileUpload;
        settings.AfterCaptureJob = AfterCaptureTasks.None;
        settings.AfterUploadJob = AfterUploadTasks.None;
        settings.DestinationInstanceId = ResolveDestinationInstanceId(destination, forcedCategory ?? GuessUploaderCategory(filePathOrName));
        return settings;
    }

    private async Task<WorkerTask> RunTaskAsync(Func<Task> start, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_taskManager);

        var tcs = new TaskCompletionSource<WorkerTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        WorkerTask? expectedTask = null;
        EventHandler<WorkerTask>? startedHandler = null;
        startedHandler = (_, task) =>
        {
            _taskManager.TaskStarted -= startedHandler;
            expectedTask = task;
        };

        _taskManager.TaskStarted += startedHandler;

        EventHandler<WorkerTask>? handler = null;
        handler = (_, task) =>
        {
            if (expectedTask == null || task != expectedTask)
            {
                return;
            }

            _taskManager.TaskCompleted -= handler;
            tcs.TrySetResult(task);
        };

        _taskManager.TaskCompleted += handler;

        try
        {
            await start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await using var _ = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));
            return await tcs.Task;
        }
        catch
        {
            _taskManager.TaskStarted -= startedHandler;
            _taskManager.TaskCompleted -= handler;
            throw;
        }
    }

    private static async Task<SkiaSharp.SKBitmap> CaptureFullScreenBitmapAsync(int? monitor, TaskSettings settings)
    {
        var bitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(CreateCaptureOptions(settings))
            ?? throw new InvalidOperationException("XerahS failed to capture the screen.");

        if (monitor == null)
        {
            return bitmap;
        }

        var screens = PlatformServices.Screen.GetAllScreens();
        if (monitor < 0 || monitor >= screens.Length)
        {
            bitmap.Dispose();
            throw new ArgumentOutOfRangeException(nameof(monitor), "The requested monitor index is out of range.");
        }

        var virtualBounds = PlatformServices.Screen.GetVirtualScreenBounds();
        var monitorBounds = screens[monitor.Value].Bounds;
        var crop = new SkiaSharp.SKRectI(
            monitorBounds.Left - virtualBounds.Left,
            monitorBounds.Top - virtualBounds.Top,
            monitorBounds.Right - virtualBounds.Left,
            monitorBounds.Bottom - virtualBounds.Top);

        var cropped = new SkiaSharp.SKBitmap(crop.Width, crop.Height);
        using var canvas = new SkiaSharp.SKCanvas(cropped);
        canvas.DrawBitmap(bitmap, crop, new SkiaSharp.SKRect(0, 0, crop.Width, crop.Height));
        bitmap.Dispose();
        return cropped;
    }

    private static CaptureOptions CreateCaptureOptions(TaskSettings settings)
    {
        return new CaptureOptions
        {
            UseModernCapture = settings.CaptureSettings.UseModernCapture,
            LinuxRegionSelectorPreference = settings.CaptureSettings.LinuxRegionSelectorPreference,
            MacOSRegionSelectorPreference = settings.CaptureSettings.MacOSRegionSelectorPreference,
            MacOSPlayCaptureSound = settings.CaptureSettings.MacOSPlayCaptureSound,
            ShowCursor = settings.CaptureSettings.ShowCursor,
            CaptureTransparent = settings.CaptureSettings.CaptureTransparent,
            CaptureShadow = settings.CaptureSettings.CaptureShadow,
            CaptureClientArea = settings.CaptureSettings.CaptureClientArea,
            WorkflowId = settings.WorkflowId,
            WorkflowCategory = settings.Job.GetHotkeyCategory(),
            CaptureStartDelaySeconds = (double)settings.CaptureSettings.ScreenshotDelay
        };
    }

    private static TaskSettings ResolveWorkflowTaskSettings(string? workflowId)
    {
        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            var workflow = SettingsManager.GetWorkflowById(workflowId)
                ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");
            return workflow.TaskSettings;
        }

        return SettingsManager.DefaultTaskSettings ?? new TaskSettings();
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        var json = JsonConvert.SerializeObject(source, jsonSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
    }

    private static string SaveImageToFile(SkiaSharp.SKBitmap bitmap, TaskSettings settings)
    {
        var path = XerahS.Core.TaskHelpers.SaveImageAsFile(bitmap, settings);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("XerahS did not produce a saved file path.");
        }

        return path;
    }

    private static void SaveBitmap(SkiaSharp.SKBitmap bitmap, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(GetEncodedFormat(outputPath), 100);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static SkiaSharp.SKEncodedImageFormat GetEncodedFormat(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg,
            ".bmp" => SkiaSharp.SKEncodedImageFormat.Bmp,
            ".gif" => SkiaSharp.SKEncodedImageFormat.Gif,
            ".webp" => SkiaSharp.SKEncodedImageFormat.Webp,
            _ => SkiaSharp.SKEncodedImageFormat.Png
        };
    }

    private static string GetAnnotatedOutputPath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        var candidate = Path.Combine(directory, $"{name}_annotated{extension}");
        var suffix = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name}_annotated_{suffix}{extension}");
            suffix++;
        }

        return candidate;
    }

    private static void AppendHistoryItem(string filePath, string type, string? url = null, string? windowTitle = null, string? processName = null)
    {
        var historyPath = SettingsManager.GetHistoryFilePath();
        using var historyManager = new HistoryManagerSQLite(historyPath);
        var item = new HistoryItem
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            DateTime = DateTime.Now,
            Type = type,
            URL = url ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            item.Tags["WindowTitle"] = windowTitle;
        }

        if (!string.IsNullOrWhiteSpace(processName))
        {
            item.Tags["ProcessName"] = processName;
        }

        historyManager.AppendHistoryItem(item);
    }

    private static string? ResolveProcessName(IntPtr handle)
    {
        try
        {
            var processId = PlatformServices.Window.GetWindowProcessId(handle);
            if (processId == 0)
            {
                return null;
            }

            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveDestinationInstanceId(string? destination, UploaderCategory category)
    {
        var instanceManager = InstanceManager.Instance;
        if (string.IsNullOrWhiteSpace(destination))
        {
            return instanceManager.GetDefaultInstance(category)?.InstanceId;
        }

        var normalized = destination.Trim();
        var allInstances = instanceManager.GetInstances();
        var matched = allInstances.FirstOrDefault(instance =>
                string.Equals(instance.InstanceId, normalized, StringComparison.OrdinalIgnoreCase))
            ?? allInstances.FirstOrDefault(instance =>
                instance.Category == category &&
                (string.Equals(instance.ProviderId, normalized, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(instance.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)));

        if (matched == null)
        {
            throw new InvalidOperationException($"Upload destination '{destination}' was not found.");
        }

        return matched.InstanceId;
    }

    private static string? ResolveDestinationSummary(string? destinationInstanceId)
    {
        if (string.IsNullOrWhiteSpace(destinationInstanceId))
        {
            return null;
        }

        var instance = InstanceManager.Instance.GetInstance(destinationInstanceId);
        return instance == null ? destinationInstanceId : $"{instance.DisplayName} ({instance.ProviderId})";
    }

    private static UploaderCategory GuessUploaderCategory(string filePathOrName)
    {
        if (FileHelpers.IsImageFile(filePathOrName))
        {
            return UploaderCategory.Image;
        }

        if (FileHelpers.IsTextFile(filePathOrName))
        {
            return UploaderCategory.Text;
        }

        return UploaderCategory.File;
    }

    private static JsonObject CreateUploadTaskResult(WorkerTask task, string fileName)
    {
        var url = task.Info.Metadata?.UploadURL;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(task.Error?.Message ?? "Upload failed.");
        }

        return new JsonObject
        {
            ["url"] = url,
            ["filename"] = fileName,
            ["size_bytes"] = task.Info.FilePath is { Length: > 0 } path && File.Exists(path) ? new FileInfo(path).Length : 0
        };
    }

    private static List<HistoryItem> LoadHistoryItems()
    {
        var historyPath = SettingsManager.GetHistoryFilePath();
        if (!File.Exists(historyPath))
        {
            return [];
        }

        using var manager = new HistoryManagerSQLite(historyPath);
        var count = manager.GetTotalCount();
        return count > 0 ? manager.GetHistoryItems(0, count) : [];
    }

    private static HistoryItem FindHistoryItem(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("id is required.", nameof(id));
        }

        if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
        {
            throw new ArgumentException("History item IDs must be integer row IDs.", nameof(id));
        }

        var item = LoadHistoryItems().FirstOrDefault(historyItem => historyItem.Id == parsedId);
        return item ?? throw new InvalidOperationException($"History item '{id}' was not found.");
    }

    private async Task<JsonObject> CreateHistoryDetailsAsync(HistoryItem item, CancellationToken cancellationToken)
    {
        long fileSize = 0;
        string? fileHash = null;
        int? width = null;
        int? height = null;
        string? ocrText = new HistoryOcrIndexStore(SettingsManager.GetHistoryFilePath()).GetText(item.Id);

        if (!string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
        {
            var fileInfo = new FileInfo(item.FilePath);
            fileSize = fileInfo.Length;

            using var hash = MD5.Create();
            await using var stream = File.OpenRead(item.FilePath);
            fileHash = Convert.ToHexString(await hash.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();

            if (FileHelpers.IsImageFile(item.FilePath))
            {
                using var bitmap = SkiaSharp.SKBitmap.Decode(item.FilePath);
                if (bitmap != null)
                {
                    width = bitmap.Width;
                    height = bitmap.Height;

                    if (string.IsNullOrWhiteSpace(ocrText) && PlatformServices.Ocr?.IsSupported == true)
                    {
                        var result = await PlatformServices.Ocr.RecognizeAsync(bitmap, new OcrOptions());
                        if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
                        {
                            ocrText = result.Text;
                            await OcrIndexingService.PersistRecognizedTextAsync(item, result.Text, "mcp-history-details", null, cancellationToken);
                        }
                    }
                }
            }
        }

        return new JsonObject
        {
            ["id"] = item.Id.ToString(CultureInfo.InvariantCulture),
            ["file_path"] = item.FilePath,
            ["file_url"] = CreateFileUrl(item.FilePath),
            ["thumbnail_path"] = string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL,
            ["thumbnail_resource"] = CreateHistoryBlobResourceUriIfLocal(item),
            ["capture_type"] = InferHistoryCaptureType(item),
            ["capture_width"] = width,
            ["capture_height"] = height,
            ["created_at"] = item.DateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["file_size_bytes"] = fileSize,
            ["file_hash_md5"] = fileHash,
            ["upload_url"] = string.IsNullOrWhiteSpace(item.URL) ? null : item.URL,
            ["ocr_text"] = ocrText,
            ["window_title"] = item.TagsWindowTitle,
            ["application_name"] = item.TagsProcessName,
            ["tags"] = ToJsonArray(item.Tags.Keys),
            ["host"] = item.Host,
            ["type"] = item.Type
        };
    }

    internal static string ResolveHistoryBlobPath(HistoryItem item)
    {
        if (TryResolveLocalFilePath(item.ThumbnailURL, out var thumbnailPath) && File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        if (TryResolveLocalFilePath(item.FilePath, out var filePath) && File.Exists(filePath))
        {
            return filePath;
        }

        throw new FileNotFoundException("History item thumbnail source file was not found.", item.FilePath);
    }

    internal static string CreateHistoryBlobResourceUri(HistoryItem item)
    {
        return $"xerahs://history/thumb/{item.Id.ToString(CultureInfo.InvariantCulture)}";
    }

    internal static JsonObject CreateHistoryBlobTooLargeResponse(string uri, string blobPath, long byteLength)
    {
        var details = new JsonObject
        {
            ["error"] = "history_blob_too_large",
            ["message"] = "History item blob is too large to inline. Open the local file path directly or reduce the capture/thumbnail size.",
            ["resource_uri"] = uri,
            ["file_path"] = blobPath,
            ["file_size_bytes"] = byteLength,
            ["max_inline_bytes"] = MaxInlineHistoryBlobBytes
        };

        return new JsonObject
        {
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["uri"] = uri,
                    ["mimeType"] = "application/json",
                    ["text"] = details.ToJsonString()
                })
        };
    }

    internal static JsonObject CreateHistoryBlobMissingResponse(string uri, HistoryItem item)
    {
        var details = new JsonObject
        {
            ["error"] = "history_blob_missing",
            ["message"] = "History item thumbnail/source file is no longer available locally. The capture may have been moved, deleted, or the thumbnail cache may have been cleaned.",
            ["resource_uri"] = uri,
            ["history_id"] = item.Id.ToString(CultureInfo.InvariantCulture),
            ["file_path"] = string.IsNullOrWhiteSpace(item.FilePath) ? null : item.FilePath,
            ["thumbnail_path"] = string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL
        };

        return new JsonObject
        {
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["uri"] = uri,
                    ["mimeType"] = "application/json",
                    ["text"] = details.ToJsonString()
                })
        };
    }

    internal static string? CreateFileUrl(string? filePath)
    {
        if (!TryResolveLocalFilePath(filePath, out var resolvedPath))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(resolvedPath);
        // Escape special URI characters (e.g. #, ?) that would break URI parsing.
        // new Uri(string) does not escape these, producing invalid URIs for paths
        // containing characters outside the reserved set.
        string escapedPath = Uri.EscapeDataString(fullPath)
            .Replace("%5C", "/"); // Uri.EscapeDataString escapes backslash; restore for file URIs.
        return new Uri("file:///" + escapedPath.Replace("//", "/")).AbsoluteUri;
    }

    private static bool TryResolveLocalFilePath(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(value))
            {
                path = Path.GetFullPath(value);
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                return false;
            }

            path = uri.LocalPath;
            return !string.IsNullOrWhiteSpace(path);
        }

        try
        {
            path = Path.GetFullPath(value);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string? CreateHistoryBlobResourceUriIfLocal(HistoryItem item)
    {
        if (TryResolveLocalFilePath(item.ThumbnailURL, out var thumbnailPath) && File.Exists(thumbnailPath))
        {
            return CreateHistoryBlobResourceUri(item);
        }

        if (TryResolveLocalFilePath(item.FilePath, out var filePath) && File.Exists(filePath))
        {
            return CreateHistoryBlobResourceUri(item);
        }

        return null;
    }

    private static JsonObject CreateHistorySummary(HistoryItem item, string? ocrText)
    {
        long size = 0;
        if (!string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
        {
            size = new FileInfo(item.FilePath).Length;
        }

        var thumbnailResource = CreateHistoryBlobResourceUriIfLocal(item);

        return new JsonObject
        {
            ["id"] = item.Id.ToString(CultureInfo.InvariantCulture),
            ["file_path"] = item.FilePath,
            ["thumbnail_url"] = string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL,
            ["thumbnail_resource"] = thumbnailResource,
            ["created_at"] = item.DateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["file_size_bytes"] = size,
            ["ocr_text"] = string.IsNullOrWhiteSpace(ocrText) ? null : ocrText,
            ["tags"] = ToJsonArray(item.Tags.Keys)
        };
    }

    private static bool MatchesDate(HistoryItem item, string? fromDate, string? toDate)
    {
        if (DateOnly.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) &&
            item.DateTime.Date < from.ToDateTime(TimeOnly.MinValue).Date)
        {
            return false;
        }

        if (DateOnly.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) &&
            item.DateTime.Date > to.ToDateTime(TimeOnly.MaxValue).Date)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesFileType(HistoryItem item, string? fileType)
    {
        return fileType?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => true,
            "image" => string.Equals(item.Type, "Image", StringComparison.OrdinalIgnoreCase),
            "text" => string.Equals(item.Type, "Text", StringComparison.OrdinalIgnoreCase),
            "video" => string.Equals(item.Type, "Video", StringComparison.OrdinalIgnoreCase),
            "file" => string.Equals(item.Type, "File", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static bool MatchesQuery(HistoryItem item, string? query, string? indexedOcrText)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var needle = query.Trim();
        return Contains(item.FileName, needle) ||
               Contains(item.FilePath, needle) ||
               Contains(item.URL, needle) ||
               Contains(item.Host, needle) ||
               Contains(item.TagsWindowTitle, needle) ||
               Contains(item.TagsProcessName, needle) ||
               Contains(indexedOcrText, needle) ||
               item.Tags.Any(pair => Contains(pair.Key, needle) || Contains(pair.Value, needle));
    }

    private static bool Contains(string? source, string needle) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string InferHistoryCaptureType(HistoryItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.TagsWindowTitle))
        {
            return "window";
        }

        return item.Type.Equals("Image", StringComparison.OrdinalIgnoreCase) ? "screen" : item.Type.ToLowerInvariant();
    }

    private static string InferCaptureMode(WorkflowType workflowType)
    {
        return workflowType switch
        {
            WorkflowType.RectangleRegion or WorkflowType.RectangleTransparent => "region",
            WorkflowType.PrintScreen => "fullscreen",
            WorkflowType.ActiveWindow => "window",
            WorkflowType.ActiveMonitor => "monitor",
            WorkflowType.ScrollingCapture => "scrolling",
            _ => workflowType.ToString()
        };
    }

    private static JsonObject CreateDestinationsResource()
    {
        var manager = InstanceManager.Instance;
        JsonNode[] destinations = manager.GetInstances()
            .OrderBy(instance => instance.Category)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(instance => new JsonObject
            {
                ["id"] = instance.InstanceId,
                ["provider_id"] = instance.ProviderId,
                ["name"] = instance.DisplayName,
                ["category"] = instance.Category.ToString().ToLowerInvariant(),
                ["is_available"] = instance.IsAvailable,
                ["is_default"] = manager.IsDefaultInstance(instance.Category, instance.InstanceId)
            })
            .Cast<JsonNode>()
            .ToArray();

        return new JsonObject
        {
            ["destinations"] = new JsonArray(destinations)
        };
    }

    private static JsonObject CreateCaptureSettings()
    {
        var captureSettings = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.RectangleRegion).TaskSettings;

        return new JsonObject
        {
            ["default_capture_mode"] = InferCaptureMode(captureSettings.Job),
            ["use_modern_capture"] = captureSettings.CaptureSettings.UseModernCapture,
            ["show_cursor"] = captureSettings.CaptureSettings.ShowCursor,
            ["macos_play_capture_sound"] = captureSettings.CaptureSettings.MacOSPlayCaptureSound,
            ["capture_delay_seconds"] = captureSettings.CaptureSettings.ScreenshotDelay,
            ["capture_transparent"] = captureSettings.CaptureSettings.CaptureTransparent,
            ["capture_shadow"] = captureSettings.CaptureSettings.CaptureShadow,
            ["capture_client_area"] = captureSettings.CaptureSettings.CaptureClientArea,
            ["image_format"] = captureSettings.ImageSettings.ImageFormat.ToString().ToLowerInvariant(),
            ["jpeg_quality"] = captureSettings.ImageSettings.ImageJPEGQuality,
            ["screenshot_folder"] = SettingsManager.ScreenshotsFolder
        };
    }

    private static JsonObject CreateUploadSettings()
    {
        var manager = InstanceManager.Instance;

        return new JsonObject
        {
            ["default_image_destination"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.Image)?.InstanceId),
            ["default_text_destination"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.Text)?.InstanceId),
            ["default_file_destination"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.File)?.InstanceId),
            ["default_url_shortener"] = ResolveDestinationSummary(manager.GetDefaultInstance(UploaderCategory.UrlShortener)?.InstanceId),
            ["copy_url_after_upload"] = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload).TaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard),
            ["destinations"] = CreateDestinationsResource()["destinations"]?.DeepClone()
        };
    }

    private static JsonObject CreateHistorySettings()
    {
        return new JsonObject
        {
            ["save_history"] = SettingsManager.Settings.HistorySaveTasks,
            ["verify_urls"] = SettingsManager.Settings.HistoryCheckURL,
            ["save_recent_tasks"] = SettingsManager.Settings.RecentTasksSave,
            ["recent_tasks_limit"] = SettingsManager.Settings.RecentTasksMaxCount,
            ["screenshot_content_search_enabled"] = SettingsManager.Settings.ScreenshotContentSearchEnabled,
            ["ocr_indexed_count"] = new HistoryOcrIndexStore(SettingsManager.GetHistoryFilePath()).CountIndexed(),
            ["history_folder"] = SettingsManager.HistoryFolder,
            ["history_file"] = SettingsManager.GetHistoryFilePath()
        };
    }

    private static JsonObject CreateGeneralSettings()
    {
        return new JsonObject
        {
            ["language"] = SettingsManager.Settings.Language.ToString(),
            ["theme_mode"] = SettingsManager.Settings.ThemeMode.ToString(),
            ["show_tray"] = SettingsManager.Settings.ShowTray,
            ["run_at_startup"] = SettingsManager.Settings.RunAtStartup,
            ["disable_hotkeys"] = SettingsManager.Settings.DisableHotkeys,
            ["settings_folder"] = SettingsManager.SettingsFolder,
            ["screenshots_folder"] = SettingsManager.ScreenshotsFolder
        };
    }

    private static JsonObject CreateIntegrationSettings()
    {
        var apiKey = SettingsManager.Settings.McpApiKey;
        var preview = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : $"{new string('*', Math.Max(apiKey.Length - 4, 0))}{apiKey[^Math.Min(4, apiKey.Length)..]}";

        return new JsonObject
        {
            ["mcp_api_key_configured"] = !string.IsNullOrWhiteSpace(apiKey),
            ["mcp_api_key_preview"] = preview,
            ["mcp_manifest_url"] = "https://xerahs.com/.well-known/mcp/manifest.json"
        };
    }

    private static JsonObject CreateJsonResource(string uri, JsonObject payload)
    {
        return new JsonObject
        {
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["uri"] = uri,
                    ["mimeType"] = "application/json",
                    ["text"] = payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
                })
        };
    }

    private static JsonArray FlagsToNames<TEnum>(TEnum flags)
        where TEnum : struct, Enum
    {
        var value = (Enum)(object)flags;
        JsonNode[] names = Enum.GetValues<TEnum>()
            .Where(flag => Convert.ToUInt64(flag, CultureInfo.InvariantCulture) != 0)
            .Where(flag => value.HasFlag((Enum)(object)flag))
            .Select(flag => JsonValue.Create(flag.ToString())!)
            .Cast<JsonNode>()
            .ToArray();

        return new JsonArray(names);
    }

    private static JsonObject SerializeRectangle(System.Drawing.Rectangle rectangle)
    {
        return new JsonObject
        {
            ["x"] = rectangle.X,
            ["y"] = rectangle.Y,
            ["width"] = rectangle.Width,
            ["height"] = rectangle.Height
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<string?> values)
    {
        JsonNode[] nodes = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => JsonValue.Create(value)!)
            .Cast<JsonNode>()
            .ToArray();

        return new JsonArray(nodes);
    }

    private static string GuessMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".txt" or ".log" or ".md" => "text/plain",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}

public sealed class McpUserCancelledException(string message) : OperationCanceledException(message);
