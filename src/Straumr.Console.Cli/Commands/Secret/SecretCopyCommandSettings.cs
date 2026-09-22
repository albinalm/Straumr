using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretCopyCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Identifier>")]
    [Description("Name or ID of the secret to copy")]
    public required string Identifier { get; set; }

    [CommandArgument(1, "<NewName>")]
    [Description("Name for the new secret")]
    public required string NewName { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the result as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
