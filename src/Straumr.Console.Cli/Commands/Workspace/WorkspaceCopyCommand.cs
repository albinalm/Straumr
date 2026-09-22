using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public class WorkspaceCopyCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCopyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceCopyCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspace workspace =
            await GetWorkspaceAsync(workspaceService, settings.Identifier, cancellationToken: cancellation);
        StraumrWorkspaceEntry newEntry =
            await workspaceService.CopyAsync(workspace.Id, settings.NewName, settings.Output, cancellation);

        if (settings.Json)
        {
            var result = new WorkspaceCreateResult(newEntry.Id.ToString(), settings.NewName, newEntry.Path);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Copied workspace[/] [bold]{Markup.Escape(settings.Identifier)}[/] to [bold]{Markup.Escape(settings.NewName)}[/]");
        }

        return 0;
    }
}
