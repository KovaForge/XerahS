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

namespace XerahS.Uploaders.Multipart;

public sealed class MultipartUploadProgress
{
    public MultipartUploadProgress(
        long bytesUploaded,
        long totalBytes,
        int completedParts,
        int totalParts,
        TimeSpan elapsed)
    {
        BytesUploaded = Math.Max(0, bytesUploaded);
        TotalBytes = Math.Max(0, totalBytes);
        CompletedParts = Math.Max(0, completedParts);
        TotalParts = Math.Max(0, totalParts);
        Elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;

        Percentage = TotalBytes == 0
            ? 0
            : Math.Min(100d, (double)BytesUploaded / TotalBytes * 100d);

        double elapsedSeconds = Math.Max(Elapsed.TotalSeconds, 0.001d);
        double bytesPerSecond = BytesUploaded / elapsedSeconds;
        EstimatedRemaining = bytesPerSecond <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Math.Max(0, (TotalBytes - BytesUploaded) / bytesPerSecond));
    }

    public long BytesUploaded { get; }

    public long TotalBytes { get; }

    public int CompletedParts { get; }

    public int TotalParts { get; }

    public double Percentage { get; }

    public TimeSpan Elapsed { get; }

    public TimeSpan EstimatedRemaining { get; }
}
