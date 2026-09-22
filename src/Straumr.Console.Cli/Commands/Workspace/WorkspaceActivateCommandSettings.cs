using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public sealed class WorkspaceActivateCommandSettings : CommandSettings
{
    [CommandArgument(0, "<Name or ID>")]
    [Description("Name or ID of the workspace to activate")]
    public required string Identifier { get; set; }
}
