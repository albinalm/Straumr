using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretListCommandSettings : CommandSettings
{
    [CommandOption("-j|--json")]
    [Description("Output as JSON array")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("--filter")]
    [Description("Filter results by name (substring) or ID prefix")]
    public string? Filter { get; [UsedImplicitly] set; }
}
