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
using ShareX.AmazonS3.Plugin.Multipart;
using XerahS.Uploaders.Multipart;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class MultipartUploadOptionsTests
{
    [Test]
    public void Validate_RejectsNonPositivePartSize()
    {
        MultipartUploadOptions options = new()
        {
            PartSizeBytes = 0,
            MaxConcurrency = 4
        };

        Assert.That(() => options.Validate(), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Validate_RejectsNonPositiveConcurrency()
    {
        MultipartUploadOptions options = new()
        {
            PartSizeBytes = 10,
            MaxConcurrency = 0
        };

        Assert.That(() => options.Validate(), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void RetryPolicy_GetDelay_StaysWithinBounds()
    {
        RetryPolicy policy = new()
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(4),
            JitterEnabled = false
        };

        Assert.That(policy.GetDelay(1), Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(policy.GetDelay(2), Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(policy.GetDelay(3), Is.EqualTo(TimeSpan.FromSeconds(4)));
        Assert.That(policy.GetDelay(4), Is.EqualTo(TimeSpan.FromSeconds(4)));
    }

    [Test]
    public void Validate_RejectsMissingS3BucketName()
    {
        S3MultipartUploadOptions options = new()
        {
            BucketName = string.Empty,
            ObjectKey = "uploads/test.bin",
            PartSizeBytes = 10 * 1024 * 1024,
            MaxConcurrency = 4
        };

        Assert.That(() => options.Validate(), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void MultipartUploadProgress_CalculatesPercentageAndEstimatedRemaining()
    {
        MultipartUploadProgress progress = new(
            bytesUploaded: 50,
            totalBytes: 100,
            completedParts: 1,
            totalParts: 2,
            elapsed: TimeSpan.FromSeconds(5));

        Assert.That(progress.Percentage, Is.EqualTo(50d));
        Assert.That(progress.EstimatedRemaining, Is.EqualTo(TimeSpan.FromSeconds(5)).Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void MultipartUploadPlanner_CreatePlan_AdjustsPartSizeToStayWithinPartLimit()
    {
        long fileSize = 101;
        MultipartUploadPlan plan = MultipartUploadPlanner.CreatePlan(
            fileSize,
            requestedPartSizeBytes: 5,
            minPartSizeBytes: 5,
            maxPartSizeBytes: 100,
            maxPartCount: 10);

        Assert.That(plan.EffectivePartSizeBytes, Is.EqualTo(11));
        Assert.That(plan.TotalParts, Is.EqualTo(10));
        Assert.That(plan.PartRanges[^1], Is.EqualTo(new PartRange(10, 99, 2)));
    }
}
