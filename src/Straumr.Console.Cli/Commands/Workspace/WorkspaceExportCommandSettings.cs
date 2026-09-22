using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public sealed class WorkspaceExportCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the workspace to export")]
    public required string Workspace { get; set; }

    [CommandArgument(1, "<Output folder>")]
    [Description("Path to the folder where the exported file will be saved")]
    public required string OutputPath { get; set; }

    [CommandOption("-j|--json")]
    [Description("Output the result as JSON")]
    public bool Json { get; [UsedImplicitly] set; }
}
