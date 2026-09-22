using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Config;

[UsedImplicitly]
public sealed class ConfigWorkspacePathCommandSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("Display the default workspace path. It is set in settings.toml.")]
    public string? Path { get; [UsedImplicitly] set; }

    [CommandOption("-j|--json")]
    [Description("Output the result as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
