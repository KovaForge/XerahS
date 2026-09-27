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
using XerahS.Core;
using XerahS.Core.Services;
using XerahS.Core.Tasks.Processors;
using XerahS.History;
using XerahS.Uploaders;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class RemoteUploadDeletionTests
{
    private static S3ConfigModel Config(bool customDomain = false) => new()
    {
        Endpoint = "s3.amazonaws.com",
        BucketName = "shots",
        UseCustomCNAME = customDomain,
        CustomDomain = customDomain ? "https://i.example.com" : string.Empty
    };

    [Test]
    public void S3_PrefersRecordedObjectKey()
    {
        var metadata = new Dictionary<string, string?> { ["S3Key"] = "ShareX/2026/09/a b.png", ["S3Bucket"] = "shots" };

        Assert.That(AmazonS3Provider.TryResolveUploadedObjectKey(Config(), "https://unrelated/x.png", metadata, out string key), Is.True);
        Assert.That(key, Is.EqualTo("ShareX/2026/09/a b.png"));
    }

    [Test]
    public void S3_RefusesKeyRecordedForAnotherBucket()
    {
        var metadata = new Dictionary<string, string?> { ["S3Key"] = "a.png", ["S3Bucket"] = "other" };

        Assert.That(AmazonS3Provider.TryResolveUploadedObjectKey(Config(), "https://s3.amazonaws.com/shots/a.png", metadata, out _), Is.False);
    }

    [TestCase(false, "https://s3.amazonaws.com/shots/ShareX/2026/09/a%20b.png", "ShareX/2026/09/a b.png")]
    [TestCase(true, "https://i.example.com/ShareX/2026/09/a%20b.png?x=1", "ShareX/2026/09/a b.png")]
    public void S3_DerivesKeyFromUrlForOlderUploads(bool customDomain, string url, string expected)
    {
        Assert.That(AmazonS3Provider.TryResolveUploadedObjectKey(Config(customDomain), url, new Dictionary<string, string?>(), out string key), Is.True);
        Assert.That(key, Is.EqualTo(expected));
    }

    [Test]
    public void S3_RejectsUrlFromAnotherHost()
    {
        Assert.That(AmazonS3Provider.TryResolveUploadedObjectKey(Config(), "https://imgur.com/abc.png", new Dictionary<string, string?>(), out _), Is.False);
    }

    [Test]
    public void ApplyUploadResult_RecordsHostInstanceAndMetadata()
    {
        var info = new TaskInfo(new TaskSettings());
        info.Result = new UploadResult { URL = "https://s3.amazonaws.com/shots/a.png", DeletionURL = "https://del" };
        info.Result.Metadata["S3Key"] = "a.png";
        info.ResolvedUploaderHost = "My S3";
        info.ResolvedUploaderInstanceId = "instance-1";
        var item = new HistoryItem { URL = info.Result.URL };

        UploadJobProcessor.ApplyUploadResult(item, info);

        Assert.That(item.Host, Is.EqualTo("My S3"));
        Assert.That(item.DeletionURL, Is.EqualTo("https://del"));
        Assert.That(item.Tags[UploadRemoteDeletionService.UploaderInstanceIdTag], Is.EqualTo("instance-1"));
        Assert.That(UploadRemoteDeletionService.GetUploadMetadata(item)["S3Key"], Is.EqualTo("a.png"));
    }

    [Test]
    public void CanDelete_IsFalseWithoutInstanceOrAfterRemoteDeletion()
    {
        Assert.That(UploadRemoteDeletionService.CanDelete(new HistoryItem { URL = "https://x/a.png" }), Is.False);

        var deleted = new HistoryItem { URL = "https://x/a.png" };
        deleted.Tags[UploadRemoteDeletionService.UploaderInstanceIdTag] = "instance-1";
        deleted.Tags[UploadRemoteDeletionService.RemoteDeletedAtTag] = DateTime.UtcNow.ToString("O");
        Assert.That(UploadRemoteDeletionService.CanDelete(deleted), Is.False);
    }
}
