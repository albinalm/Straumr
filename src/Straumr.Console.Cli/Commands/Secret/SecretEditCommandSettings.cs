using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretEditCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the secret to edit")]
    public required string Identifier { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the updated secret as JSON on success; errors emitted as JSON to stderr")]
    public bool Json { get; [UsedImplicitly] set; }
}
