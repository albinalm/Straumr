using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public sealed class WorkspaceGetCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the workspace to get")]
    public required string Identifier { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the workspace as raw JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
