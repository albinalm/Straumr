using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Enums;

namespace Straumr.Console.Cli.Commands.Autocomplete;

[UsedImplicitly]
public sealed class AutocompleteInstallCommandSettings : CommandSettings
{
    [CommandOption("-s|--shell")]
    [Description("Shell to install autocomplete for (zsh, bash, pwsh)")]
    public ShellKind? Shell { get; [UsedImplicitly] set; }

    [CommandOption("-p|--profile")]
    [Description("The path to your profile file.")]
    public string? ProfilePath { get; [UsedImplicitly] set; }

    [CommandOption("-a|--alias")]
    [Description("Additional aliases to register for autocomplete")]
    public string[] Aliases { get; [UsedImplicitly] set; } = [];
}
