using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Hotkeys;

[TestFixture]
public sealed class WorkflowsConfigTests
{
    [Test]
    public void EnsureWorkflowIds_RepairsMissingTaskWorkflowIdForExistingWorkflow()
    {
        WorkflowSettings workflow = new(WorkflowType.FileUpload, new HotkeyInfo())
        {
            Id = "workflow-1"
        };
        workflow.TaskSettings.WorkflowId = string.Empty;

        WorkflowsConfig config = new()
        {
            Hotkeys = new() { workflow }
        };

        config.EnsureWorkflowIds();

        Assert.That(workflow.TaskSettings.WorkflowId, Is.EqualTo("workflow-1"));
    }

    [Test]
    public void EnsureWorkflowIds_RepairsMismatchedTaskWorkflowIdForExistingWorkflow()
    {
        WorkflowSettings workflow = new(WorkflowType.FileUpload, new HotkeyInfo())
        {
            Id = "workflow-2"
        };
        workflow.TaskSettings.WorkflowId = "stale-id";

        WorkflowsConfig config = new()
        {
            Hotkeys = new() { workflow }
        };

        config.EnsureWorkflowIds();

        Assert.That(workflow.TaskSettings.WorkflowId, Is.EqualTo("workflow-2"));
    }
}
