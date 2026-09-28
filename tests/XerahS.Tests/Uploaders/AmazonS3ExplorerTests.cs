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

using NUnit.Framework;
using ShareX.AmazonS3.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class AmazonS3ExplorerTests
{
    [Test]
    public void ResolveListPrefix_EmptyFolder_UsesStaticObjectPrefix()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("ShareX/%y/%mo", "");

        Assert.That(prefix, Is.EqualTo("ShareX/"));
    }

    [Test]
    public void ResolveListPrefix_FolderPath_AppendsToLogicalRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("ShareX/%y/%mo", "2026/");

        Assert.That(prefix, Is.EqualTo("ShareX/2026/"));
    }

    [Test]
    public void ResolveListPrefix_TokenEmbeddedInSegment_ListsNearestFolder()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("customers/tenant-%y/%mo", null);

        Assert.That(prefix, Is.EqualTo("customers/"));
    }

    [Test]
    public void ResolveListPrefix_TokenAtStart_ListsBucketRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("%y/%mo", null);

        Assert.That(prefix, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ResolveListPrefix_StaticFolder_NavigatesBelowLogicalRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("uploads", "images/");

        Assert.That(prefix, Is.EqualTo("uploads/images/"));
    }

    [Test]
    public void ResolveListPrefix_NoObjectPrefix_ListsBucketRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix(null, null);

        Assert.That(prefix, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetExplorerPath_ConfiguredPrefix_IsLogicalRoot()
    {
        string path = S3ExplorerListHelper.GetExplorerPath("ShareX/2026/", "ShareX/%y/%mo");

        Assert.That(path, Is.EqualTo("2026/"));
    }

    [Test]
    public void IsListBucketDenied_DetectsAwsAccessDeniedMessage()
    {
        const string serviceMessage =
            "S3 request failed: AccessDenied - User is not authorized to perform: s3:ListBucket";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.True);
    }

    [Test]
    public void IsListBucketDenied_DetectsStandardAccessDeniedMessage()
    {
        const string serviceMessage = "S3 request failed: AccessDenied - Access Denied";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.True);
    }

    [Test]
    public void IsListBucketDenied_DetectsEmptyBodyForbiddenFallback()
    {
        const string serviceMessage = "S3 request failed: 403 Forbidden";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.True);
    }

    [Test]
    public void IsListBucketDenied_DoesNotRewriteUnrelatedS3Failure()
    {
        const string serviceMessage = "S3 request failed: SignatureDoesNotMatch - The request signature does not match";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.False);
    }

    [Test]
    public void BuildListBucketDeniedMessage_IncludesBucketArnAndRequiredAction()
    {
        string message = S3ExplorerListHelper.BuildListBucketDeniedMessage(
            "example-bucket",
            "AccessDenied - s3:ListBucket");

        Assert.That(message, Does.Contain("s3:ListBucket"));
        Assert.That(message, Does.Contain("arn:aws:s3:::example-bucket"));
        Assert.That(message, Does.Contain("bucket-level list permission"));
        Assert.That(message, Does.Contain("s3:prefix"));
    }

    [TestCase(301, "<Error><Code>PermanentRedirect</Code><Message>m</Message><Endpoint>b.s3.ap-southeast-2.amazonaws.com</Endpoint></Error>", true)]
    [TestCase(400, "<Error><Code>AuthorizationHeaderMalformed</Code></Error>", true)]
    [TestCase(301, "", true)]
    [TestCase(403, "<Error><Code>AccessDenied</Code></Error>", false)]
    public void IsWrongRegionResponse_DetectsRedirects(int status, string body, bool expected)
    {
        Assert.That(S3ExplorerListHelper.IsWrongRegionResponse(status, body), Is.EqualTo(expected));
    }

    [TestCase("s3.amazonaws.com", "ap-southeast-2", "s3.ap-southeast-2.amazonaws.com")]
    [TestCase("bucket.s3.us-east-1.amazonaws.com", "eu-west-1", "bucket.s3.eu-west-1.amazonaws.com")]
    [TestCase("bucket.s3-us-west-2.amazonaws.com", "us-east-2", "bucket.s3.us-east-2.amazonaws.com")]
    [TestCase("minio.example.com", "us-east-1", null)]
    [TestCase("s3.amazonaws.com", "bad region", null)]
    public void TryRewriteAwsHostForRegion(string host, string region, string? expected)
    {
        Assert.That(S3ExplorerListHelper.TryRewriteAwsHostForRegion(host, region), Is.EqualTo(expected));
    }

    [Test]
    public void BuildWrongRegionMessage_NamesTheRegionToUse()
    {
        string message = S3ExplorerListHelper.BuildWrongRegionMessage("us-east-1", "ap-southeast-2", null, "S3 request failed: PermanentRedirect - m");
        Assert.That(message, Does.Contain("Set the region to ap-southeast-2"));
        Assert.That(message, Does.Contain("us-east-1"));
    }

    [Test]
    public void BuildWrongRegionMessage_FallsBackToEndpointHint()
    {
        string endpoint = S3ExplorerListHelper.GetErrorElement(
            "<Error><Code>PermanentRedirect</Code><Endpoint>b.s3.eu-west-1.amazonaws.com</Endpoint></Error>", "Endpoint")!;
        string message = S3ExplorerListHelper.BuildWrongRegionMessage("us-east-1", null, endpoint, "x");
        Assert.That(message, Does.Contain("Use the endpoint b.s3.eu-west-1.amazonaws.com"));
    }
}
