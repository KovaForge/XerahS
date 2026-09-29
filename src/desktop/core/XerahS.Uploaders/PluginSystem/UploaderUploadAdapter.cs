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

namespace XerahS.Uploaders.PluginSystem;

public static class UploaderUploadAdapter
{
    public static Task<UploadOutcome> UploadAsync(object instance, UploadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(request);

        if (instance is IUploadHandler handler)
        {
            return handler.UploadAsync(request, cancellationToken);
        }

        if (instance is GenericUploader generic)
        {
            return UploadLegacyAsync(generic, request, cancellationToken);
        }

        return Task.FromResult(UploadOutcome.Failed("Uploader type not supported."));
    }

    private static async Task<UploadOutcome> UploadLegacyAsync(
        GenericUploader uploader,
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Uploader.ProgressEventHandler? progressHandler = null;
        if (request.Progress != null)
        {
            long lastPosition = 0;
            progressHandler = progress =>
            {
                long delta = Math.Max(0, progress.Position - lastPosition);
                lastPosition = progress.Position;
                request.Progress.Report(new UploadProgressReport(delta, progress.Length));
            };
            uploader.ProgressChanged += progressHandler;
        }

        try
        {
            UploadResult result = await Task.Run(
                () => uploader.Upload(request.Content, request.FileName),
                cancellationToken).ConfigureAwait(false);
            return UploadOutcomeMapper.FromUploadResult(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UploadOutcome.Failed(ex.Message);
        }
        finally
        {
            if (progressHandler != null)
            {
                uploader.ProgressChanged -= progressHandler;
            }
        }
    }
}
