using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using XerahS.RegionCapture.ViewModels;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public sealed class RegionCaptureAnnotationToolCoordinatorTests
{
    [Test]
    public void ActiveTool_SelectionSynchronizesAcrossRegisteredMonitorViewModels()
    {
        var coordinator = new RegionCaptureAnnotationToolCoordinator();
        var primaryMonitor = new RegionCaptureAnnotationViewModel();
        var secondaryMonitor = new RegionCaptureAnnotationViewModel();

        coordinator.Register(primaryMonitor);
        coordinator.Register(secondaryMonitor);

        primaryMonitor.ActiveTool = EditorTool.Arrow;

        Assert.That(coordinator.ActiveTool, Is.EqualTo(EditorTool.Arrow));
        Assert.That(secondaryMonitor.ActiveTool, Is.EqualTo(EditorTool.Arrow));
    }

    [Test]
    public void Unregister_StopsSynchronizingClosedOverlayViewModel()
    {
        var coordinator = new RegionCaptureAnnotationToolCoordinator();
        var activeMonitor = new RegionCaptureAnnotationViewModel();
        var closedMonitor = new RegionCaptureAnnotationViewModel();

        coordinator.Register(activeMonitor);
        coordinator.Register(closedMonitor);
        coordinator.Unregister(closedMonitor);

        activeMonitor.ActiveTool = EditorTool.Rectangle;

        Assert.That(closedMonitor.ActiveTool, Is.EqualTo(EditorTool.Select));
    }
}
