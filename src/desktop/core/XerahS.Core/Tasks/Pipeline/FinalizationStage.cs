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

using XerahS.Common;
using XerahS.Core.Tasks.Processors;

namespace XerahS.Core.Tasks.Pipeline
{
    /// <summary>
    /// Pipeline stage that executes post-capture processing (CaptureJobProcessor)
    /// and upload (UploadJobProcessor).
    /// Extracted from the tail end of WorkerTask.DoWorkAsync().
    /// </summary>
    public class FinalizationStage : IPipelineStage
    {
        public string StageName => "Finalization";

        public async Task<PipelineStageResult> ExecuteAsync(PipelineContext context, CancellationToken token)
        {
            // Execute Capture Job (File Save, Clipboard, etc)
            var captureProcessor = new CaptureJobProcessor();
            await captureProcessor.ProcessAsync(context.Info, token);

            // Execute Upload Job
            var uploadProcessor = new UploadJobProcessor();
            await uploadProcessor.ProcessAsync(context.Info, token);

            // Check upload result
            if (ShouldRequireSuccessfulUpload(context.Info) && !IsUploadResultSuccessful(context.Info.Result))
            {
                string message = string.IsNullOrWhiteSpace(context.Info.Result?.Response)
                    ? "Upload failed."
                    : context.Info.Result.Response!;

                DebugHelper.WriteLine($"Upload failed during task execution: {message}");
                context.Error = new InvalidOperationException(message);
                context.Status = TaskStatus.Failed;
                return PipelineStageResult.Failed;
            }

            return PipelineStageResult.Continue;
        }

        private static bool ShouldRequireSuccessfulUpload(TaskInfo info)
        {
            return info.Job == TaskJob.FileUpload || info.Job == TaskJob.TextUpload;
        }

        private static bool IsUploadResultSuccessful(XerahS.Uploaders.UploadResult? result)
        {
            if (result == null) return false;
            if (result.IsError) return false;
            if (!string.IsNullOrEmpty(result.URL)) return true;
            if (!string.IsNullOrEmpty(result.ShortenedURL)) return true;
            return false;
        }
    }
}
