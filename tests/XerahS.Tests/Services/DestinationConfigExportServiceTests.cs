#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.UI.Services;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Services;

[TestFixture]
public class DestinationConfigExportServiceTests
{
    [Test]
    public void BuildEncryptedExport_S3WithoutBucket_ThrowsBeforeExportingIncompleteMobileConfig()
    {
        var instance = new UploaderInstance
        {
            InstanceId = "s3-default",
            ProviderId = "amazons3",
            Category = UploaderCategory.File,
            DisplayName = "S3",
            SettingsJson = """
            {
              "AuthMode": 0,
              "SecretKey": "secret-key",
              "BucketName": "   ",
              "Region": "us-west-2"
            }
            """
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DestinationConfigExportService.BuildEncryptedExport(instance, "correct horse battery staple"));

        Assert.That(ex!.Message, Is.EqualTo("Amazon S3 bucket name is required before exporting to mobile."));
    }
}
