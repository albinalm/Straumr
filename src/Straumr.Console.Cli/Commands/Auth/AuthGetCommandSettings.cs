using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public sealed class AuthGetCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the auth to get")]
    public required string Identifier { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the auth as raw JSON")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
