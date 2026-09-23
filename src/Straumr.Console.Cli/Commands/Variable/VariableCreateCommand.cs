using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public class VariableCreateCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrVariableService variableService) : AsyncCommand<VariableCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VariableCreateCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = stateService.State.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, workspaceService);
            if (resolved is null)
            {
                AnsiConsole.MarkupLine($"[red]Workspace not found: {Markup.Escape(settings.Workspace)}[/]");
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry is null)
        {
            AnsiConsole.MarkupLine("[red]No workspace loaded. Please load a workspace using 'workspace use <name>'[/]");
            return 1;
        }

        string name = settings.Name ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Name:")
                .Validate(value => string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Error("Name cannot be empty.")
                    : VariableHelpers.IsSecretName(value)
                        ? ValidationResult.Error($"A name cannot start with {VariableHelpers.SecretPrefix}.")
                        : ValidationResult.Success()));

        string value = settings.Value ?? AnsiConsole.Prompt(new TextPrompt<string>("Value:"));

        try
        {
            var variable = new StraumrVariable
            {
                Name = name,
                Value = value
            };

            await variableService.CreateAsync(workspaceEntry, variable, cancellation);
            AnsiConsole.MarkupLine($"[green]Created variable[/] [bold]{Markup.Escape(variable.Name)}[/] ({variable.Id})");
            return 0;
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason is StraumrError.EntryConflict or StraumrError.MissingEntry or StraumrError.InvalidEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }
    }
}
