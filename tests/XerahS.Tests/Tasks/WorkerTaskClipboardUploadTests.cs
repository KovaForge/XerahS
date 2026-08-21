using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Uploaders;

namespace XerahS.Tests.Tasks;

[TestFixture]
public sealed class WorkerTaskClipboardUploadTests
{
    [Test]
    public void ApplyLastClipboardUploadInfo_PreservesResolvedUploaderHost()
    {
        var destination = new TaskInfo();

        var lastInfo = new TaskInfo()
        {
            DataType = EDataType.File,
            Job = TaskJob.FileUpload,
            Result = new UploadResult
            {
                IsSuccess = true,
                URL = "https://example.test/last.txt"
            },
            ResolvedUploaderHost = "Actual Clipboard Uploader"
        };
        lastInfo.FilePath = "/tmp/last.txt";
        lastInfo.Metadata.UploadURL = "https://example.test/last.txt";

        WorkerTask.ApplyLastClipboardUploadInfo(destination, lastInfo);

        Assert.Multiple(() =>
        {
            Assert.That(destination.DataType, Is.EqualTo(EDataType.File));
            Assert.That(destination.FilePath, Is.EqualTo("/tmp/last.txt"));
            Assert.That(destination.Job, Is.EqualTo(TaskJob.FileUpload));
            Assert.That(destination.Result, Is.SameAs(lastInfo.Result));
            Assert.That(destination.Metadata.UploadURL, Is.EqualTo("https://example.test/last.txt"));
            Assert.That(destination.UploaderHost, Is.EqualTo("Actual Clipboard Uploader"));
        });
    }
}
