using System.ComponentModel;
using Spectre.Console.Cli;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public sealed class WorkspaceDeleteCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the workspace to delete")]
    public required string Identifier { get; set; }

    [CommandOption("-j|--json")]
    [Description("Suppress human-readable output; errors are emitted as JSON to stderr")]
    public bool Json { get; set; }
}
