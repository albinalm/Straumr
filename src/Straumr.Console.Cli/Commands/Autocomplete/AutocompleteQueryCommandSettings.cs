using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Autocomplete;

[UsedImplicitly]
public sealed class AutocompleteQueryCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Query>")]
    [Description("Current command line to complete")]
    public required string Query { get; set; }
}
