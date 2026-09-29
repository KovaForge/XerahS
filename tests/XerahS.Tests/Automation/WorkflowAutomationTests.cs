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

using System.IO.Compression;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Automation;
using XerahS.Core.Hotkeys;

namespace XerahS.Tests.Automation;

[TestFixture]
[NonParallelizable]
public class WorkflowAutomationTests
{
    private WorkflowsConfig _originalConfig = null!;
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalConfig = SettingsManager.WorkflowsConfig;
        SettingsManager.WorkflowsConfig = new WorkflowsConfig
        {
            Hotkeys =
            [
                CreateWorkflow("bc904c7e-4edc-4726-b53e-4f6cf8646eee", "Region capture", WorkflowType.RectangleRegion),
                CreateWorkflow("c46346f8-aaaa-4726-b53e-4f6cf8646eee", "Full screen capture", WorkflowType.PrintScreen),
                CreateWorkflow("c4eb91e8-bbbb-4726-b53e-4f6cf8646eee", "Twin", WorkflowType.PrintScreen),
                CreateWorkflow("c4eb91e8-cccc-4726-b53e-4f6cf8646eee", "Twin", WorkflowType.ActiveWindow)
            ]
        };

        _tempDirectory = Path.Combine(Path.GetTempPath(), "xerahs-automation-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        SettingsManager.WorkflowsConfig = _originalConfig;
        Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public void FindWorkflow_MatchesIdNameAndPrefix()
    {
        Assert.That(WorkflowAutomation.FindWorkflow("bc904c7e-4edc-4726-b53e-4f6cf8646eee").TaskSettings.Description, Is.EqualTo("Region capture"));
        Assert.That(WorkflowAutomation.FindWorkflow("region CAPTURE").Id, Does.StartWith("bc904c7e"));
        Assert.That(WorkflowAutomation.FindWorkflow("c46346f8").TaskSettings.Description, Is.EqualTo("Full screen capture"));
    }

    [Test]
    public void FindWorkflow_ReportsNotFoundAndAmbiguous()
    {
        var notFound = Assert.Throws<AutomationException>(() => WorkflowAutomation.FindWorkflow("nothing"));
        Assert.That(notFound!.Code, Is.EqualTo(AutomationErrorCodes.NotFound));

        var byName = Assert.Throws<AutomationException>(() => WorkflowAutomation.FindWorkflow("Twin"));
        Assert.That(byName!.Code, Is.EqualTo(AutomationErrorCodes.Ambiguous));

        var byPrefix = Assert.Throws<AutomationException>(() => WorkflowAutomation.FindWorkflow("c4eb91e8"));
        Assert.That(byPrefix!.Code, Is.EqualTo(AutomationErrorCodes.Ambiguous));
    }

    [Test]
    public void UpdateAfterCaptureTasks_AddsAndRemovesByName()
    {
        var workflow = WorkflowAutomation.FindWorkflow("Region capture");
        workflow.TaskSettings.AfterCaptureJob = AfterCaptureTasks.ShowAfterCaptureWindow | AfterCaptureTasks.SaveImageToFile;

        WorkflowAutomation.UpdateAfterCaptureTasks(workflow, ["addimageeffects", "AnnotateImage"], ["ShowAfterCaptureWindow"]);
        WorkflowSummary summary = WorkflowAutomation.Describe(workflow);

        Assert.That(workflow.TaskSettings.AfterCaptureJob,
            Is.EqualTo(AfterCaptureTasks.AddImageEffects | AfterCaptureTasks.AnnotateMedia | AfterCaptureTasks.SaveImageToFile));
        Assert.That(summary.AfterCapture, Is.EqualTo(new[] { "AddImageEffects", "AnnotateMedia", "SaveImageToFile" }),
            "The obsolete AnnotateImage alias is accepted as input but reported by its current name.");

        var invalid = Assert.Throws<AutomationException>(() =>
            WorkflowAutomation.UpdateAfterCaptureTasks(workflow, ["Bogus"], []));
        Assert.That(invalid!.Code, Is.EqualTo(AutomationErrorCodes.InvalidValue));
    }

    [Test]
    public void ImportImageEffects_LoadsShareXPresetAndEnablesEffects()
    {
        string presetPath = WriteSxie("GoldBorder.sxie", """
            {
              "Name": "GoldBorder",
              "Effects": [
                { "$type": "Canvas", "Margin": "5, 0, 5, 0", "Color": "Transparent", "Enabled": true },
                { "$type": "Shadow", "Opacity": 0.6, "Size": 20, "Color": "218, 180, 0", "Offset": "0, 0", "Enabled": true },
                { "$type": "Sharpen", "Enabled": true },
                { "$type": "AutoCrop", "Enabled": true }
              ]
            }
            """);
        var workflow = WorkflowAutomation.FindWorkflow("Region capture");

        IReadOnlyList<string> skipped = WorkflowAutomation.ImportImageEffects(workflow, presetPath, enable: true);
        WorkflowSummary summary = WorkflowAutomation.Describe(workflow);

        Assert.That(skipped, Is.EqualTo(new[] { "Sharpen" }));
        Assert.That(summary.ImageEffects.Enabled, Is.True);
        Assert.That(summary.ImageEffects.PresetName, Is.EqualTo("GoldBorder"));
        Assert.That(summary.ImageEffects.Effects, Has.Length.EqualTo(3));
        Assert.That(summary.AfterCapture, Does.Contain("AddImageEffects"));
    }

    [Test]
    public void ImportImageEffects_RejectsMissingAndUnsupportedFiles()
    {
        var workflow = WorkflowAutomation.FindWorkflow("Region capture");

        var missing = Assert.Throws<AutomationException>(() =>
            WorkflowAutomation.ImportImageEffects(workflow, Path.Combine(_tempDirectory, "none.sxie"), enable: true));
        Assert.That(missing!.Code, Is.EqualTo(AutomationErrorCodes.InvalidPath));

        string text = Path.Combine(_tempDirectory, "notes.txt");
        File.WriteAllText(text, "hello");
        var unsupported = Assert.Throws<AutomationException>(() =>
            WorkflowAutomation.ImportImageEffects(workflow, text, enable: true));
        Assert.That(unsupported!.Code, Is.EqualTo(AutomationErrorCodes.UnsupportedType));
    }

    [Test]
    public void ClearImageEffects_ResetsPresetAndTurnsEffectsOff()
    {
        var workflow = WorkflowAutomation.FindWorkflow("Region capture");
        workflow.TaskSettings.AfterCaptureJob |= AfterCaptureTasks.AddImageEffects;
        workflow.TaskSettings.ImageSettings.ImageEffectsPreset = new ImageEffectPreset
        {
            Name = "Old",
            Effects = [new ShareX.ImageEditor.Core.ImageEffects.Drawings.DrawBackgroundEffect()]
        };

        WorkflowAutomation.ClearImageEffects(workflow);
        WorkflowSummary summary = WorkflowAutomation.Describe(workflow);

        Assert.That(summary.ImageEffects.Enabled, Is.False);
        Assert.That(summary.ImageEffects.Effects, Is.Empty);
    }

    private static WorkflowSettings CreateWorkflow(string id, string name, WorkflowType job)
    {
        var workflow = new WorkflowSettings { Id = id };
        workflow.TaskSettings.Job = job;
        workflow.TaskSettings.Description = name;
        return workflow;
    }

    private string WriteSxie(string fileName, string configJson)
    {
        string path = Path.Combine(_tempDirectory, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("Config.json").Open());
        writer.Write(configJson);
        return path;
    }
}
