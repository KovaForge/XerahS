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

using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;

namespace XerahS.Bootstrap
{
    internal sealed class DesktopTaskManagerAdapter(TaskManager taskManager) : IDesktopTaskManager
    {
        public event EventHandler<WorkerTask>? TaskCompleted
        {
            add => taskManager.TaskCompleted += value;
            remove => taskManager.TaskCompleted -= value;
        }

        public event EventHandler<WorkerTask>? TaskStarted
        {
            add => taskManager.TaskStarted += value;
            remove => taskManager.TaskStarted -= value;
        }

        public IEnumerable<WorkerTask> Tasks => taskManager.Tasks;

        public Task StartTask(TaskSettings? taskSettings, SKBitmap? inputImage = null) =>
            taskManager.StartTask(taskSettings, inputImage);

        public Task StartFileTask(TaskSettings? taskSettings, string filePath) =>
            taskManager.StartFileTask(taskSettings, filePath);

        public Task StartImageUploadTask(TaskSettings? taskSettings, SKBitmap image) =>
            taskManager.StartImageUploadTask(taskSettings, image);

        public Task StartTextTask(TaskSettings? taskSettings, string text) =>
            taskManager.StartTextTask(taskSettings, text);

        public void StopAllTasks() => taskManager.StopAllTasks();
    }
}
