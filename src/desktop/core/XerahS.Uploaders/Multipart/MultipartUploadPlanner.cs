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

namespace XerahS.Uploaders.Multipart
{
    public static class MultipartUploadPlanner
    {
        public static MultipartUploadPlan CreatePlan(long fileSizeBytes,
            long requestedPartSizeBytes,
            long minPartSizeBytes,
            long maxPartSizeBytes,
            int maxPartCount)
        {
            if (fileSizeBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "Multipart upload requires a non-empty file.");
            }

            if (requestedPartSizeBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedPartSizeBytes), "Requested part size must be greater than zero.");
            }

            if (minPartSizeBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minPartSizeBytes), "Minimum part size must be greater than zero.");
            }

            if (maxPartSizeBytes < minPartSizeBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPartSizeBytes), "Maximum part size must be greater than or equal to the minimum part size.");
            }

            if (maxPartCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPartCount), "Maximum part count must be greater than zero.");
            }

            long effectivePartSizeBytes = Math.Max(requestedPartSizeBytes, minPartSizeBytes);
            long minimumRequiredPartSize = DivideRoundUp(fileSizeBytes, maxPartCount);
            effectivePartSizeBytes = Math.Max(effectivePartSizeBytes, minimumRequiredPartSize);

            if (effectivePartSizeBytes > maxPartSizeBytes)
            {
                throw new InvalidOperationException("The file is too large to fit within the configured multipart upload limits.");
            }

            int totalParts = checked((int)DivideRoundUp(fileSizeBytes, effectivePartSizeBytes));
            List<PartRange> partRanges = new List<PartRange>(totalParts);

            for (int partNumber = 1; partNumber <= totalParts; partNumber++)
            {
                long offset = (partNumber - 1L) * effectivePartSizeBytes;
                long length = Math.Min(effectivePartSizeBytes, fileSizeBytes - offset);
                partRanges.Add(new PartRange(partNumber, offset, length));
            }

            return new MultipartUploadPlan(fileSizeBytes, effectivePartSizeBytes, partRanges);
        }

        private static long DivideRoundUp(long dividend, long divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }
    }
}
