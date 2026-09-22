using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretCreateCommandSettings : CommandSettings
{
    [CommandArgument(0, "[Name]")]
    [Description("Name of the secret to create")]
    public string? Name { get; [UsedImplicitly] set; }

    [CommandArgument(1, "[Value]")]
    [Description("Value of the secret to create")]
    public string? Value { get; [UsedImplicitly] set; }
}
