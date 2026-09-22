using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public sealed class WorkspaceImportCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Path>")]
    [Description("Path to the workspace file to import")]
    public required string Path { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the imported workspace as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
