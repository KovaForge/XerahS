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
using XerahS.Common;
using XerahS.OmaXerahs.Commands;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        JsonStdout.Enabled = JsonStdout.ShouldEnable(args);

        try
        {
            var rootCommand = BuildRootCommand();
            return await rootCommand.Parse(args).InvokeAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            var (code, message) = ErrorMapper.FromException(ex);
            if (code == CliErrorCodes.Provider)
            {
                code = CliErrorCodes.Incompatible;
            }

            if (JsonStdout.Enabled)
            {
                JsonStdout.WriteFailure(code, message);
            }
            else
            {
                Console.Error.WriteLine($"Fatal error: {message}");
            }

            return 1;
        }
    }

    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("OmaXerahs — upload Omarchy screenshots through the configured XerahS image destination.");
        rootCommand.Add(CapabilitiesCommand.Create());
        rootCommand.Add(DoctorCommand.Create());
        rootCommand.Add(UploadCommand.Create());
        rootCommand.SetAction(parseResult =>
        {
            JsonStdout.Enabled = JsonStdout.ShouldEnable(Environment.GetCommandLineArgs());
            return JsonStdout.WriteFailureAndExit(
                CliErrorCodes.Usage,
                "No command specified. Use capabilities, doctor, or upload. See --help.");
        });
        return rootCommand;
    }
}
