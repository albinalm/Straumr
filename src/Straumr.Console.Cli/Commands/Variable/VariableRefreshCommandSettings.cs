using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public sealed class VariableRefreshCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Request-backed variable to refresh")]
    public required string Identifier { get; set; }
    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID")]
    public string? Workspace { get; [UsedImplicitly] set; }
    [CommandOption("-j|--json")]
    [Description("Output the variable ID, name, and cache timestamp as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
