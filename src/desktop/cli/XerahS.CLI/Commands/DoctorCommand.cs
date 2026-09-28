using System.CommandLine;
using XerahS.CLI.Services;

namespace XerahS.CLI.Commands;

public static class DoctorCommand
{
    public static Command Create()
    {
        var doctorCommand = new Command("doctor", "Diagnose XerahS CLI configuration");

        var linuxInputOption = new Option<bool>("--linux-input")
        {
            Description = "Diagnose Linux global hotkey input device permissions (direct evdev listener)."
        };
        var linuxDesktopOption = new Option<bool>("--linux-desktop")
        {
            Description = "Report the Linux desktop profile (Omarchy, Hyprland) and the OmaSnap host-mode probe (XIP0088)."
        };
        var doctorJsonOption = new Option<bool>("--json") { Description = "Write diagnostic output as JSON." };
        doctorCommand.Add(linuxInputOption);
        doctorCommand.Add(linuxDesktopOption);
        doctorCommand.Add(doctorJsonOption);
        doctorCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(linuxInputOption))
            {
                Environment.ExitCode = RunLinuxInputDoctor(parseResult.GetValue(doctorJsonOption));
            }
            else if (parseResult.GetValue(linuxDesktopOption))
            {
                Environment.ExitCode = RunLinuxDesktopDoctor(parseResult.GetValue(doctorJsonOption));
            }
            else
            {
                Console.WriteLine("Specify a diagnostic, e.g. 'doctor --linux-input' or 'doctor uploaders'.");
            }
        });

        var uploadersCommand = new Command("uploaders", "Diagnose uploader plugins, instances, and category defaults");
        var fixOption = new Option<bool>("--fix") { Description = "Create or repair safe zero-config uploader instances where possible." };
        var jsonOption = new Option<bool>("--json") { Description = "Write diagnostic output as JSON." };
        uploadersCommand.Add(fixOption);
        uploadersCommand.Add(jsonOption);
        uploadersCommand.SetAction(parseResult =>
        {
            Environment.ExitCode = CliUploaderBootstrapper.DoctorUploaders(parseResult.GetValue(fixOption), parseResult.GetValue(jsonOption));
        });
        doctorCommand.Add(uploadersCommand);
        return doctorCommand;
    }

    private static int RunLinuxDesktopDoctor(bool json)
    {
#if LINUX
        var profile = XerahS.Platform.Linux.Services.LinuxDesktopProfile.Current;
        var (summary, probeJson) = profile.DescribeOmaSnapAsync().GetAwaiter().GetResult();
        if (json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                isWayland = profile.IsWayland,
                isHyprland = profile.IsHyprland,
                isOmarchy = profile.IsOmarchy,
                isSandboxed = profile.IsSandboxed,
                isOmarchyLike = profile.IsOmarchyLike,
                omaSnap = summary,
                omaSnapProbe = probeJson?.Trim()
            }));
        }
        else
        {
            Console.WriteLine(profile.Describe());
            Console.WriteLine($"OmaSnap probe: {probeJson?.Trim() ?? "<none>"}");
        }

        return 0;
#else
        Console.WriteLine(json
            ? "{\"error\":\"--linux-desktop is only available on Linux builds\"}"
            : "doctor --linux-desktop is only available on Linux builds.");
        return 1;
#endif
    }

    private static int RunLinuxInputDoctor(bool json)
    {
#if LINUX
        var (report, exitCode) = XerahS.Platform.Linux.Input.Evdev.LinuxInputDiagnostics.BuildReport(json);
        Console.WriteLine(report);
        return exitCode;
#else
        Console.WriteLine(json
            ? "{\"error\":\"--linux-input is only available on Linux builds\"}"
            : "doctor --linux-input is only available on Linux builds.");
        return 1;
#endif
    }
}
