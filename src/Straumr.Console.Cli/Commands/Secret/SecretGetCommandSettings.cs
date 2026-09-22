using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretGetCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the secret to get")]
    public required string Identifier { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the secret as raw JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
