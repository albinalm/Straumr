using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretDeleteCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the secret to delete")]
    public required string Identifier { get; set; }
}
