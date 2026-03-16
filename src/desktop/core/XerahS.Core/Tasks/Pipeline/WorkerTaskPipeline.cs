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

namespace XerahS.Core.Tasks.Pipeline
{
    /// <summary>
    /// Orchestrates sequential execution of pipeline stages for a WorkerTask.
    /// Each stage is run in order; if any stage returns Stop or Failed, the pipeline halts.
    /// </summary>
    public class WorkerTaskPipeline
    {
        private readonly List<IPipelineStage> _stages = new();

        /// <summary>
        /// Adds a stage to the end of the pipeline.
        /// </summary>
        public WorkerTaskPipeline AddStage(IPipelineStage stage)
        {
            _stages.Add(stage ?? throw new ArgumentNullException(nameof(stage)));
            return this;
        }

        /// <summary>
        /// Executes all stages sequentially. Stops on the first non-Continue result.
        /// </summary>
        public async Task<PipelineStageResult> ExecuteAsync(PipelineContext context, CancellationToken token)
        {
            foreach (var stage in _stages)
            {
                token.ThrowIfCancellationRequested();

                DebugHelper.WriteLine($"[Pipeline] Entering stage: {stage.StageName}");
                var result = await stage.ExecuteAsync(context, token);
                DebugHelper.WriteLine($"[Pipeline] Stage '{stage.StageName}' returned: {result}");

                if (result != PipelineStageResult.Continue)
                {
                    return result;
                }
            }

            return PipelineStageResult.Continue;
        }
    }
}
