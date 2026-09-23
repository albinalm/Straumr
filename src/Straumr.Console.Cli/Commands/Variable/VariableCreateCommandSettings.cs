using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public sealed class VariableCreateCommandSettings : CommandSettings
{
    [CommandArgument(0, "[Name]")]
    [Description("Name of the variable to create")]
    public string? Name { get; [UsedImplicitly] set; }

    [CommandArgument(1, "[Value]")]
    [Description("Value of the variable to create")]
    public string? Value { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
