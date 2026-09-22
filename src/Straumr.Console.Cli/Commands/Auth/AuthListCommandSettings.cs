using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public sealed class AuthListCommandSettings : CommandSettings
{
    [CommandOption("-j|--json")]
    [Description("Output as JSON array")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("--filter")]
    [Description("Filter results by name (substring) or ID prefix")]
    public string? Filter { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
