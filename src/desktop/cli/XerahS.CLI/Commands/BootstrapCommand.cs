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
            CliUploaderBootstrapper.BootstrapUploaders(parseResult.GetValue(jsonOption));
            Environment.ExitCode = 0;
        });
        command.Add(uploadersCommand);
        return command;
    }
}
