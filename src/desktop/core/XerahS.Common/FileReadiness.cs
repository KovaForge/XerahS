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

namespace XerahS.Common;

public enum FileReadinessStatus
{
    Ready,
    Missing,
    Empty,
    NotAFile
}

/// <summary>
/// Confirms that a handed-off file (omaxerahs upload, send-to, watch folder, screenshot tools
/// that write asynchronously) exists and has stopped growing before XerahS reads it, so a path
/// accepted too early or after the file moved produces a clear error instead of a stack trace.
/// </summary>
public static class FileReadiness
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task<FileReadinessStatus> WaitUntilReadyAsync(
        string path,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return FileReadinessStatus.Missing;
        }

        if (Directory.Exists(path))
        {
            return FileReadinessStatus.NotAFile;
        }

        DateTime deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        long lastLength = -1;
        FileReadinessStatus status = FileReadinessStatus.Missing;

        while (true)
        {
            long length = TryGetLength(path);
            if (length < 0)
            {
                status = FileReadinessStatus.Missing;
            }
            else if (length == 0)
            {
                status = FileReadinessStatus.Empty;
            }
            else if (length == lastLength)
            {
                // Same non-zero size on two consecutive polls: the writer has finished.
                return FileReadinessStatus.Ready;
            }
            else
            {
                status = FileReadinessStatus.Ready;
            }

            if (DateTime.UtcNow >= deadline)
            {
                // A file with content at the deadline is usable even if we saw it only once.
                return status;
            }

            lastLength = length;
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string Describe(FileReadinessStatus status, string path)
    {
        return status switch
        {
            FileReadinessStatus.Ready => $"File is ready: {path}",
            FileReadinessStatus.Missing => $"File not found: {path}. It may have been moved or deleted before the upload started.",
            FileReadinessStatus.Empty => $"File is empty: {path}. The program that created it may not have finished writing it.",
            FileReadinessStatus.NotAFile => $"Path is a folder, not a file: {path}",
            _ => path
        };
    }

    private static long TryGetLength(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : -1;
        }
        catch (IOException)
        {
            return -1;
        }
        catch (UnauthorizedAccessException)
        {
            return -1;
        }
    }
}
