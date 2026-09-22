using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public sealed class AuthEditCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the auth to edit")]
    public required string Identifier { get; set; }

    [CommandOption("-e|--editor")]
    [Description("Open the auth in the default editor instead of interactive prompts")]
    public bool UseEditor { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Open in editor and output the updated auth as JSON on success; implies --editor")]
    public bool Json { get; [UsedImplicitly] set; }
}
