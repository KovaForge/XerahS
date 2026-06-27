using System.CommandLine;
using XerahS.CLI.Services;

namespace XerahS.CLI.Commands;

public static class BootstrapCommand
{
    public static Command Create()
    {
        var command = new Command("bootstrap", "Initialize safe first-use CLI defaults");
        var uploadersCommand = new Command("uploaders", "Create or repair safe zero-config uploader instances and defaults");
        var jsonOption = new Option<bool>("--json") { Description = "Write bootstrap output as JSON." };
        uploadersCommand.Add(jsonOption);
        uploadersCommand.SetAction(parseResult =>
        {
            bool json = parseResult.GetValue(jsonOption);
            var report = CliUploaderBootstrapper.Bootstrap(quiet: json);
            if (json)
            {
                CliUploaderBootstrapper.WriteJson(report);
            }
            Environment.ExitCode = report.HasBlockingIssues ? 1 : 0;
        });
        command.Add(uploadersCommand);
        return command;
    }
}
