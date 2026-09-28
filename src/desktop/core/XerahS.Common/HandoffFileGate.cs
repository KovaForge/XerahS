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

namespace XerahS.Common
{
    public enum HandoffFileState
    {
        Ready,
        Missing,
        Empty,
        StillWriting
    }

    public readonly record struct HandoffFileCheck(HandoffFileState State, string Path, long Length)
    {
        public bool IsReady => State == HandoffFileState.Ready;

        /// <summary>A one-line reason suitable for logs and user notifications.</summary>
        public string Describe() => State switch
        {
            HandoffFileState.Ready => $"Ready: {Path} ({Length} bytes)",
            HandoffFileState.Missing => $"File not found: {Path}. It may have been moved or deleted before XerahS could read it.",
            HandoffFileState.Empty => $"File is empty: {Path}. The program that created it may not have finished writing.",
            _ => $"File is still being written: {Path}."
        };
    }

    /// <summary>
    /// Validates a file handed to XerahS by another program (omaxerahs upload, Send to, shell
    /// integration) before it is queued. A screenshot tool can hand over a path before its PNG is
    /// fully written, or move it right after; waiting briefly for a stable, non-empty file turns
    /// a later upload-time FileNotFoundException into a clear, early error.
    /// </summary>
    public static class HandoffFileGate
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        public static async Task<HandoffFileCheck> WaitForReadyAsync(
            string path,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            DateTime deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
            long previousLength = -1;

            while (true)
            {
                HandoffFileCheck current = Inspect(path);

                // Ready means readable, non-empty, and the same size on two consecutive polls.
                if (current.IsReady && current.Length == previousLength)
                {
                    return current;
                }

                previousLength = current.IsReady ? current.Length : -1;

                if (DateTime.UtcNow >= deadline)
                {
                    // A readable, non-empty file whose size is still settling is usable.
                    return current;
                }

                try
                {
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return current;
                }
            }
        }

        private static HandoffFileCheck Inspect(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    return new HandoffFileCheck(HandoffFileState.Missing, path, 0);
                }

                if (info.Length == 0)
                {
                    return new HandoffFileCheck(HandoffFileState.Empty, path, 0);
                }

                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                }

                return new HandoffFileCheck(HandoffFileState.Ready, path, info.Length);
            }
            catch (FileNotFoundException)
            {
                return new HandoffFileCheck(HandoffFileState.Missing, path, 0);
            }
            catch (DirectoryNotFoundException)
            {
                return new HandoffFileCheck(HandoffFileState.Missing, path, 0);
            }
            catch (IOException)
            {
                return new HandoffFileCheck(HandoffFileState.StillWriting, path, 0);
            }
            catch (UnauthorizedAccessException)
            {
                return new HandoffFileCheck(HandoffFileState.StillWriting, path, 0);
            }
        }
    }
}
