using System.CommandLine;
using XerahS.CLI.Services;

namespace XerahS.CLI.Commands;

public static class DoctorCommand
{
    public static Command Create()
    {
        var doctorCommand = new Command("doctor", "Diagnose XerahS CLI configuration");
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
}
