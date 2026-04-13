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
    public sealed class MultipartUploadPlan
    {
        public long FileSizeBytes { get; }
        public long EffectivePartSizeBytes { get; }
        public IReadOnlyList<PartRange> PartRanges { get; }
        public int TotalParts => PartRanges.Count;

        public MultipartUploadPlan(long fileSizeBytes, long effectivePartSizeBytes, IReadOnlyList<PartRange> partRanges)
        {
            FileSizeBytes = fileSizeBytes;
            EffectivePartSizeBytes = effectivePartSizeBytes;
            PartRanges = partRanges ?? throw new ArgumentNullException(nameof(partRanges));
        }
    }
}
