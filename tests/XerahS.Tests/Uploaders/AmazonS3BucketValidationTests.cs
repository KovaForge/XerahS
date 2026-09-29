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

using Newtonsoft.Json;
using NUnit.Framework;
using ShareX.AmazonS3.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class AmazonS3BucketValidationTests
{
    [TestCase("")]
    [TestCase("   ")]
    public void ValidateSettings_RejectsAMissingBucket(string bucket)
    {
        var provider = new AmazonS3Provider();
        string json = JsonConvert.SerializeObject(new S3ConfigModel { BucketName = bucket });

        Assert.That(provider.ValidateSettings(json), Is.False);
    }

    [Test]
    public void ValidateSettings_AcceptsABucket()
    {
        var provider = new AmazonS3Provider();
        string json = JsonConvert.SerializeObject(new S3ConfigModel { BucketName = "example-bucket", Region = "ap-southeast-2" });

        Assert.That(provider.ValidateSettings(json), Is.True);
    }

    [Test]
    public void Upload_WithoutABucket_FailsWithAReasonAndNeverCreatesAClient()
    {
        var uploader = new AmazonS3Uploader(
            new S3ConfigModel { BucketName = string.Empty },
            "access-key",
            "secret-key",
            sessionToken: null,
            s3ClientFactory: () => throw new AssertionException("no S3 client may be created without a bucket"));

        using var stream = new MemoryStream([1, 2, 3]);
        var result = uploader.Upload(stream, "capture.png");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Response, Is.EqualTo(AmazonS3Uploader.MissingBucketMessage));
            Assert.That(uploader.IsError, Is.True);
        });
    }
}
