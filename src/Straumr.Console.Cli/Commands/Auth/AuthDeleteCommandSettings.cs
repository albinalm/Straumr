using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public sealed class AuthDeleteCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the auth to delete")]
    public required string Identifier { get; set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Suppress human-readable output; errors are emitted as JSON to stderr")]
    public bool Json { get; [UsedImplicitly] set; }
}
