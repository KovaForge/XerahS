using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
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

    [Test]
    public void ApplyResolvedUploaderHost_PrefersActualSuccessfulUploaderOverConfiguredDefault()
    {
        var configuredDefault = CreateInstance("Configured Default");
        var fallbackInstance = CreateInstance("Actual Fallback");
        InstanceManager.Instance.AddInstance(configuredDefault);
        InstanceManager.Instance.AddInstance(fallbackInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, configuredDefault.InstanceId);

        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image
        };

        UploadJobProcessor.ApplyResolvedUploaderHost(info, fallbackInstance, new UploadResult
        {
            URL = "https://example.test/image.png"
        });

        Assert.That(info.UploaderHost, Is.EqualTo("Actual Fallback"));
    }

    [Test]
    public void ApplyResolvedUploaderHost_DoesNotReplaceConfiguredHostAfterFailedUpload()
    {
        var configuredDefault = CreateInstance("Configured Default");
        var failedFallback = CreateInstance("Failed Fallback");
        InstanceManager.Instance.AddInstance(configuredDefault);
        InstanceManager.Instance.AddInstance(failedFallback);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, configuredDefault.InstanceId);

        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image
        };

        UploadJobProcessor.ApplyResolvedUploaderHost(info, failedFallback, new UploadResult
        {
            IsSuccess = false,
            Response = "boom"
        });

        Assert.That(info.UploaderHost, Is.EqualTo("Configured Default"));
    }

    [Test]
    public void CreateHistoryItem_PreservesAllUploaderResultUrls()
    {
        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image,
            Result = new UploadResult
            {
                URL = "https://example.test/image.png",
                ThumbnailURL = "https://example.test/thumb.png",
                DeletionURL = "https://example.test/delete/image",
                ShortenedURL = "https://ex.test/i"
            }
        };
        info.FilePath = "/tmp/image.png";

        var historyItem = UploadJobProcessor.CreateHistoryItem(info, info.Result.URL!);

        Assert.Multiple(() =>
        {
            Assert.That(historyItem.URL, Is.EqualTo("https://example.test/image.png"));
            Assert.That(historyItem.ThumbnailURL, Is.EqualTo("https://example.test/thumb.png"));
            Assert.That(historyItem.DeletionURL, Is.EqualTo("https://example.test/delete/image"));
            Assert.That(historyItem.ShortenedURL, Is.EqualTo("https://ex.test/i"));
        });
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
