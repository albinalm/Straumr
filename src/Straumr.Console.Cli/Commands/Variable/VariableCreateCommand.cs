using System.Text.Json;
using JetBrains.Annotations;
using Straumr.Console.Cli.Helpers;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
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
    IStraumrVariableService variableService,
    IStraumrRequestService requestService) : AsyncCommand<VariableCreateCommandSettings>
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
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry is null)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        if ((settings.FromRequest is null) != (settings.Extract is null) ||
            settings.FromRequest is null && settings.TimeToLiveSeconds.HasValue ||
            settings.FromRequest is not null && settings.Value is not null)
        {
            WriteError("Use --from-request with --extract, optional --ttl, and no literal value", settings.Json);
            return 1;
        }
        if (settings.Json && (settings.Name is null || settings.FromRequest is null && settings.Value is null))
        {
            WriteError("Pass a name and a value or source request when using --json", true);
            return 1;
        }

        string name = settings.Name ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Name:")
                .Validate(value => string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Error("Name cannot be empty.")
                    : VariableHelpers.IsSecretName(value)
                        ? ValidationResult.Error($"A name cannot start with {VariableHelpers.SecretPrefix}.")
                        : ValidationResult.Success()));

        string value = settings.FromRequest is not null ? string.Empty : settings.Value ?? AnsiConsole.Prompt(new TextPrompt<string>("Value:"));

        try
        {
            var variable = new StraumrVariable
            {
                Name = name,
                Value = value
            };

            if (settings.FromRequest is { } sourceIdentifier)
            {
                StraumrRequest request = await GetRequestAsync(requestService, workspaceEntry, sourceIdentifier, cancellationToken: cancellation);
                (ExtractionSource source, string expression) = ExtractionOptionHelpers.Parse(settings.Extract!);
                variable.Source = new VariableSource
                {
                    RequestId = request.Id,
                    ExtractionSource = source,
                    Expression = expression,
                    TimeToLiveSeconds = settings.TimeToLiveSeconds
                };
            }
            await variableService.CreateAsync(workspaceEntry, variable, cancellation);
            if (settings.Json)
            {
                var result = new VariableListItem(variable.Id.ToString(), variable.Name, "Valid");
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.VariableListItem));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Created variable[/] [bold]{Markup.Escape(variable.Name)}[/] ({variable.Id})");
            }
            return 0;
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return ex.Reason is StraumrError.EntryConflict or StraumrError.MissingEntry or StraumrError.InvalidEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return -1;
        }
    }
}
