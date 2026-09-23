using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public sealed class VariableCopyCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Identifier>")]
    [Description("Name or ID of the variable to copy")]
    public required string Identifier { get; set; }

    [CommandArgument(1, "<NewName>")]
    [Description("Name for the new variable")]
    public required string NewName { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the result as JSON")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
