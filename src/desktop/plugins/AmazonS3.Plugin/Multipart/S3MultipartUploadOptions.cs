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

using XerahS.Uploaders.Multipart;

namespace ShareX.AmazonS3.Plugin.Multipart;

public sealed class S3MultipartUploadOptions : MultipartUploadOptions
{
    public string BucketName { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string? URL { get; set; }

    public S3StorageClass StorageClass { get; set; } = S3StorageClass.Standard;

    public bool SetPublicAcl { get; set; }

    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(BucketName))
        {
            throw new ArgumentException("S3 bucket name is required.", nameof(BucketName));
        }

        if (string.IsNullOrWhiteSpace(ObjectKey))
        {
            throw new ArgumentException("S3 object key is required.", nameof(ObjectKey));
        }
    }
}
