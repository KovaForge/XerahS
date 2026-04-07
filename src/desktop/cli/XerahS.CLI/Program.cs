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

using System.CommandLine;
using XerahS.Bootstrap;
using XerahS.CLI.Commands;
using XerahS.CLI.Services;
using Microsoft.Extensions.DependencyInjection;
using XerahS.Common;

namespace XerahS.CLI
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Initialize in headless mode
                var bootstrapOptions = new BootstrapOptions
                {
                    EnableLogging = true,
                    InitializeRecording = true, // Wait for recording init (critical for ScreenRecorder)
                    UIService = new HeadlessUIService(),
                    ToastService = new HeadlessToastService()
                };

                var result = await ShareXBootstrap.InitializeAsync(bootstrapOptions);

                if (!result.PlatformServicesInitialized)
                {
                    Console.Error.WriteLine("Failed to initialize platform services");
                    return 1;
                }

                if (!result.ConfigurationLoaded)
                {
                    Console.Error.WriteLine("Failed to load configuration");
                    return 1;
                }

                IServiceProvider services = result.ServiceProvider
                    ?? throw new InvalidOperationException("Bootstrap completed without a service provider.");

                var taskManager = services.GetRequiredService<IDesktopTaskManager>();
                var recordingCoordinator = services.GetRequiredService<IScreenRecordingCoordinator>();

                // Build command tree
                var rootCommand = new RootCommand("XerahS CLI - ShareX workflow automation");

                // Add commands
                rootCommand.Add(WorkflowCommand.Create(taskManager, recordingCoordinator));
                rootCommand.Add(RecordCommand.Create(recordingCoordinator));
                rootCommand.Add(CaptureCommand.Create(taskManager));
                rootCommand.Add(ListCommand.Create());
                rootCommand.Add(ConfigCommand.Create());
                rootCommand.Add(BackupSettingsCommand.Create());
                rootCommand.Add(VerifyRegionCaptureCommand.Create());
                rootCommand.Add(CompareCaptureCommand.Create());
                rootCommand.Add(VerifyRecordingCommand.Create(recordingCoordinator));
                rootCommand.Add(VerifyGifRecordingCommand.Create(recordingCoordinator));
                rootCommand.Add(VerifyVideoEditorCommand.Create());
                rootCommand.Add(OpenVideoEditorCommand.Create());
                rootCommand.Add(WatchFolderDaemonCommand.Create());
                rootCommand.Add(UploadCommand.Create(taskManager));

                // Execute
                return await rootCommand.Parse(args).InvokeAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                DebugHelper.WriteException(ex);
                return 1;
            }
        }
    }
}
