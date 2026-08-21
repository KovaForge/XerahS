using global::Avalonia.Input;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class WorkflowsViewModelTests
{
    [Test]
    public void CreateWorkflowDuplicate_AssignsNewWorkflowIdToClonedTaskSettings()
    {
        WorkflowSettings source = new(WorkflowType.FileUpload, new HotkeyInfo(Key.T, KeyModifiers.Control))
        {
            Name = "Upload text",
            Enabled = true,
            PinnedToTray = true
        };
        source.TaskSettings.Job = WorkflowType.FileUpload;
        source.TaskSettings.WorkflowId = source.Id;
        source.TaskSettings.Description = "Upload text";

        WorkflowSettings clone = WorkflowsViewModel.CreateWorkflowDuplicate(source);

        Assert.Multiple(() =>
        {
            Assert.That(clone.Id, Is.Not.EqualTo(source.Id));
            Assert.That(clone.TaskSettings.WorkflowId, Is.EqualTo(clone.Id));
            Assert.That(clone.TaskSettings.WorkflowId, Is.Not.EqualTo(source.TaskSettings.WorkflowId));
            Assert.That(clone.Name, Is.EqualTo(source.Name));
            Assert.That(clone.PinnedToTray, Is.EqualTo(source.PinnedToTray));
        });
    }
}
