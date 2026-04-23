using NUnit.Framework;
using XerahS.Core.Tasks.Processors;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Tasks;

[TestFixture]
[NonParallelizable]
public class UploadJobProcessorTests
{
    [SetUp]
    public void SetUp()
    {
        ClearInstances();
    }

    [TearDown]
    public void TearDown()
    {
        ClearInstances();
    }

    [Test]
    public void ResolveRequestedInstance_ReturnsNullWhenConfiguredInstanceIsUnavailable()
    {
        var unavailableConfigured = CreateInstance("Unavailable Configured", isAvailable: false);
        InstanceManager.Instance.AddInstance(unavailableConfigured);

        var result = UploadJobProcessor.ResolveRequestedInstance(
            InstanceManager.Instance,
            unavailableConfigured.InstanceId,
            UploaderCategory.Image);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveDefaultInstance_ReturnsNullWhenDefaultInstanceIsUnavailable()
    {
        var unavailableDefault = CreateInstance("Unavailable Default", isAvailable: false);
        InstanceManager.Instance.AddInstance(unavailableDefault);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, unavailableDefault.InstanceId);

        var result = UploadJobProcessor.ResolveDefaultInstance(InstanceManager.Instance, UploaderCategory.Image);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveRequestedInstance_PreservesAvailableConfiguredInstance()
    {
        var availableInstance = CreateInstance("Available Instance");
        InstanceManager.Instance.AddInstance(availableInstance);

        var result = UploadJobProcessor.ResolveRequestedInstance(
            InstanceManager.Instance,
            availableInstance.InstanceId,
            UploaderCategory.Image);

        Assert.That(result, Is.SameAs(availableInstance));
    }

    private static UploaderInstance CreateInstance(string name, bool isAvailable = true)
    {
        return new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = name,
            IsAvailable = isAvailable
        };
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }
}
