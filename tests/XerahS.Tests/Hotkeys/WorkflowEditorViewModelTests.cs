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

using Avalonia.Input;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Tests.Xip0052;
using XerahS.UI.ViewModels;
using HotkeyInfo = XerahS.Platform.Abstractions.HotkeyInfo;

namespace XerahS.Tests.Hotkeys;

[TestFixture]
[NonParallelizable]
public class WorkflowEditorViewModelTests
{
    private static readonly FakeUiViewModelFactory ViewModelFactory = new();

    [AvaloniaTest]
    public void Save_DoesNotMutateSourceWorkflowUntilCommitted()
    {
        var workflow = new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo(Key.X, KeyModifiers.Control | KeyModifiers.Shift))
        {
            TaskSettings = new TaskSettings
            {
                Job = WorkflowType.RectangleRegion,
                Description = "Region capture"
            }
        };

        var viewModel = new WorkflowEditorViewModel(workflow, ViewModelFactory, loadUploaderCategories: false)
        {
            Description = "Area capture",
            SelectedJob = WorkflowType.ActiveWindow,
            SelectedKey = Key.A,
            SelectedModifiers = KeyModifiers.Control
        };

        Assert.Multiple(() =>
        {
            Assert.That(workflow.TaskSettings.Description, Is.EqualTo("Region capture"));
            Assert.That(workflow.Job, Is.EqualTo(WorkflowType.RectangleRegion));
            Assert.That(workflow.HotkeyInfo.Key, Is.EqualTo(Key.X));
            Assert.That(workflow.HotkeyInfo.Modifiers, Is.EqualTo(KeyModifiers.Control | KeyModifiers.Shift));
        });

        viewModel.Save();

        Assert.Multiple(() =>
        {
            Assert.That(workflow.TaskSettings.Description, Is.EqualTo("Area capture"));
            Assert.That(workflow.Job, Is.EqualTo(WorkflowType.ActiveWindow));
            Assert.That(workflow.HotkeyInfo.Key, Is.EqualTo(Key.A));
            Assert.That(workflow.HotkeyInfo.Modifiers, Is.EqualTo(KeyModifiers.Control));
        });
    }

    [AvaloniaTest]
    public void SelectedJobChange_UpdatesDefaultDescriptionWhenNameWasNotCustomized()
    {
        var workflow = new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo())
        {
            TaskSettings = new TaskSettings
            {
                Job = WorkflowType.RectangleRegion,
                Description = "Region capture"
            }
        };

        var viewModel = new WorkflowEditorViewModel(workflow, ViewModelFactory, loadUploaderCategories: false);
        viewModel.SelectedJob = WorkflowType.ActiveWindow;

        Assert.That(viewModel.Description, Is.EqualTo("Active window capture"));
    }

    [AvaloniaTest]
    public void SelectedJobChange_PreservesCustomizedDescription()
    {
        var workflow = new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo())
        {
            TaskSettings = new TaskSettings
            {
                Job = WorkflowType.RectangleRegion,
                Description = "My custom workflow"
            }
        };

        var viewModel = new WorkflowEditorViewModel(workflow, ViewModelFactory, loadUploaderCategories: false);
        viewModel.SelectedJob = WorkflowType.ActiveWindow;

        Assert.That(viewModel.Description, Is.EqualTo("My custom workflow"));
    }
}
