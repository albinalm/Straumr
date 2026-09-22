using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public sealed class WorkspaceCreateCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name>")]
    [Description("Name of the workspace to create")]
    public required string Name { get; set; }

    [CommandOption("-o|--output <DIR>")]
    [Description("Directory where the workspace folder should be created")]
    public string? Output { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Output the created workspace as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
