using Avalonia.Input;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.CaptureCommandPalette;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.CaptureCommandPalette;

public sealed class CaptureCommandPaletteTests
{
    [Test]
    public void FuzzyMatcher_RanksExactAboveSubsequenceAndRejectsNoMatch()
    {
        double exact = CaptureCommandPaletteFuzzyMatcher.Score("region capture", "Region Capture");
        double subsequence = CaptureCommandPaletteFuzzyMatcher.Score("rgc", "Region Capture");
        double noMatch = CaptureCommandPaletteFuzzyMatcher.Score("zzz", "Region Capture");

        Assert.Multiple(() =>
        {
            Assert.That(exact, Is.GreaterThan(subsequence));
            Assert.That(subsequence, Is.GreaterThan(0));
            Assert.That(noMatch, Is.EqualTo(0));
        });
    }

    [Test]
    public void FuzzyMatcher_CollapsesRepeatedWhitespace()
    {
        double score = CaptureCommandPaletteFuzzyMatcher.Score("region   capture", "Region Capture");

        Assert.That(score, Is.GreaterThan(0));
    }

    [Test]
    public void CreateItems_IncludesOnlyEnabledCaptureAndRecordingWorkflows()
    {
        WorkflowSettings capture = CreateWorkflow(WorkflowType.RectangleRegion, "Region capture");
        WorkflowSettings recording = CreateWorkflow(WorkflowType.ScreenRecorder, "Record screen");
        WorkflowSettings upload = CreateWorkflow(WorkflowType.FileUpload, "File upload");
        WorkflowSettings disabled = CreateWorkflow(WorkflowType.ActiveWindow, "Active window");
        disabled.Enabled = false;

        var items = CaptureCommandPaletteProvider.CreateItems([capture, recording, upload, disabled]);

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(item => item.Workflow), Is.EquivalentTo(new[] { capture, recording }));
            Assert.That(items.Select(item => item.Id), Is.EquivalentTo(new[] { capture.Id, recording.Id }));
        });
    }

    [Test]
    public void FilterAndRank_UsesLabelAndDescription()
    {
        WorkflowSettings fullScreen = CreateWorkflow(WorkflowType.PrintScreen, "Full screen capture");
        WorkflowSettings activeWindow = CreateWorkflow(WorkflowType.ActiveWindow, "Active window");
        var items = CaptureCommandPaletteProvider.CreateItems([fullScreen, activeWindow]);

        var filtered = CaptureCommandPaletteProvider.FilterAndRank(items, "window");

        Assert.Multiple(() =>
        {
            Assert.That(filtered, Has.Count.EqualTo(1));
            Assert.That(filtered[0].Workflow, Is.SameAs(activeWindow));
        });
    }

    [Test]
    public async Task ViewModel_ExecuteSelected_InvokesSelectedWorkflow()
    {
        WorkflowSettings region = CreateWorkflow(WorkflowType.RectangleRegion, "Region capture");
        var items = CaptureCommandPaletteProvider.CreateItems([region]);
        string? executedWorkflowId = null;
        bool closed = false;
        var viewModel = new CaptureCommandPaletteViewModel(
            () => items,
            item =>
            {
                executedWorkflowId = item.Workflow.Id;
                return Task.CompletedTask;
            });
        viewModel.RequestClose += () => closed = true;

        await viewModel.ExecuteSelectedCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(executedWorkflowId, Is.EqualTo(region.Id));
            Assert.That(closed, Is.True);
        });
    }

    [Test]
    public void ViewModel_MoveSelection_WrapsAtListEdges()
    {
        WorkflowSettings region = CreateWorkflow(WorkflowType.RectangleRegion, "Region capture");
        WorkflowSettings window = CreateWorkflow(WorkflowType.ActiveWindow, "Active window");
        var items = CaptureCommandPaletteProvider.CreateItems([region, window]);
        var viewModel = new CaptureCommandPaletteViewModel(
            () => items,
            _ => Task.CompletedTask);

        viewModel.MoveSelection(-1);
        CaptureCommandPaletteItem? wrappedFromFirst = viewModel.SelectedItem;
        viewModel.MoveSelection(1);
        CaptureCommandPaletteItem? wrappedFromLast = viewModel.SelectedItem;

        Assert.Multiple(() =>
        {
            Assert.That(wrappedFromFirst?.Workflow, Is.SameAs(window));
            Assert.That(wrappedFromLast?.Workflow, Is.SameAs(region));
        });
    }

    [Test]
    public void ViewModel_MoveSelection_UpFromNoSelection_SelectsLastItem()
    {
        WorkflowSettings region = CreateWorkflow(WorkflowType.RectangleRegion, "Region capture");
        WorkflowSettings window = CreateWorkflow(WorkflowType.ActiveWindow, "Active window");
        var items = CaptureCommandPaletteProvider.CreateItems([region, window]);
        var viewModel = new CaptureCommandPaletteViewModel(
            () => items,
            _ => Task.CompletedTask)
        {
            SelectedItem = null
        };

        viewModel.MoveSelection(-1);

        Assert.That(viewModel.SelectedItem?.Workflow, Is.SameAs(window));
    }

    [Test]
    public void ViewModel_HandleEscape_WithWhitespaceQuery_RequestsClose()
    {
        WorkflowSettings region = CreateWorkflow(WorkflowType.RectangleRegion, "Region capture");
        var items = CaptureCommandPaletteProvider.CreateItems([region]);
        var viewModel = new CaptureCommandPaletteViewModel(
            () => items,
            _ => Task.CompletedTask)
        {
            Query = "   "
        };
        bool closed = false;
        bool focusedSearch = false;
        viewModel.RequestClose += () => closed = true;
        viewModel.RequestFocusSearch += () => focusedSearch = true;

        viewModel.HandleEscape();

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.True);
            Assert.That(focusedSearch, Is.False);
            Assert.That(viewModel.Query, Is.EqualTo("   "));
        });
    }

    [Test]
    public void ViewModel_ReloadItems_WhenProviderThrows_KeepsPaletteUsable()
    {
        var viewModel = new CaptureCommandPaletteViewModel(
            () => throw new InvalidOperationException("provider failed"),
            _ => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Items, Is.Empty);
            Assert.That(viewModel.SelectedItem, Is.Null);
            Assert.That(viewModel.StatusText, Is.EqualTo("Capture workflows are unavailable."));
        });
    }

    private static WorkflowSettings CreateWorkflow(WorkflowType workflowType, string description)
    {
        var workflow = new WorkflowSettings(
            workflowType,
            new HotkeyInfo(Key.F, KeyModifiers.Control | KeyModifiers.Shift));
        workflow.TaskSettings.Description = description;
        workflow.EnsureId();
        return workflow;
    }
}
