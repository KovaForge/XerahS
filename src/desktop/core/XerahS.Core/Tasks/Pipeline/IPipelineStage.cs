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

namespace XerahS.Core.Tasks.Pipeline
{
    /// <summary>
    /// Represents a stage in the WorkerTask processing pipeline.
    /// Each stage performs a focused piece of work (capture, processing, upload, etc).
    /// </summary>
    public interface IPipelineStage
    {
        /// <summary>
        /// The display name of this stage (for logging/diagnostics).
        /// </summary>
        string StageName { get; }

        /// <summary>
        /// Executes this stage of the pipeline.
        /// </summary>
        /// <param name="context">Shared pipeline context containing task info and state.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A result indicating whether the pipeline should continue or stop.</returns>
        Task<PipelineStageResult> ExecuteAsync(PipelineContext context, CancellationToken token);
    }

    /// <summary>
    /// Result returned by a pipeline stage indicating whether to continue or stop.
    /// </summary>
    public enum PipelineStageResult
    {
        /// <summary>Continue to the next stage.</summary>
        Continue,

        /// <summary>Stop the pipeline (task completed or cancelled, not an error).</summary>
        Stop,

        /// <summary>Stop the pipeline due to an error.</summary>
        Failed
    }

    /// <summary>
    /// Shared context passed between pipeline stages.
    /// Carries the TaskInfo, status, and any intermediate results.
    /// </summary>
    public class PipelineContext
    {
        public required TaskInfo Info { get; init; }
        public TaskStatus Status { get; set; }
        public Exception? Error { get; set; }

        /// <summary>
        /// Action to fire StatusChanged on the owning WorkerTask.
        /// </summary>
        public Action? OnStatusChanged { get; init; }
    }
}
