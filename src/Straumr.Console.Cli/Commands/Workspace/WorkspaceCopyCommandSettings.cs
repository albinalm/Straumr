using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public sealed class WorkspaceCopyCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Identifier>")]
    [Description("Name or ID of the workspace to copy")]
    public required string Identifier { get; set; }

    [CommandArgument(1, "<NewName>")]
    [Description("Name for the new workspace")]
    public required string NewName { get; set; }

    [CommandOption("-o|--output <DIR>")]
    [Description("Directory where the workspace folder should be created")]
    public string? Output { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Output the result as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
