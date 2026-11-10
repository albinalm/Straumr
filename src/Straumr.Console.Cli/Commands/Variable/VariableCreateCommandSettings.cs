using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public sealed class VariableCreateCommandSettings : CommandSettings
{
    [CommandOption("--from-request <REQUEST>")]
    [Description("Source request name or ID for an automatically fetched value")]
    public string? FromRequest { get; [UsedImplicitly] set; }
    [CommandOption("--extract <RULE>")]
    [Description("Extraction rule: jsonpath:<path>, header:<name>, or regex:<pattern>")]
    public string? Extract { get; [UsedImplicitly] set; }
    [CommandOption("--ttl <SECONDS>")]
    [Description("Cache lifetime in seconds; omitted means cached until refreshed or inputs change")]
    public int? TimeToLiveSeconds { get; [UsedImplicitly] set; }
    [CommandOption("-j|--json")]
    [Description("Output the created variable ID and name as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
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
