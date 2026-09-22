using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public class AuthEditCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService,
    IStraumrFileService fileService,
    IInteractiveConsole interactiveConsole) : AsyncCommand<AuthEditCommandSettings>
{
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";
    private const string ActionAutoRenew = "Auto-renew auth";

    public override async Task<int> ExecuteAsync(CommandContext context, AuthEditCommandSettings settings,
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

        bool hasWorkspace = workspaceEntry != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        if (settings.UseEditor || settings.Json)
        {
            return await ExecuteEditorAsync(settings.Identifier, settings.Json, workspaceEntry!, cancellation);
        }

        StraumrAuth auth;
        try
        {
            auth = await GetAuthAsync(
                authService, workspaceEntry!, settings.Identifier, cancellationToken: cancellation);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, false);
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, false);
            return -1;
        }

        return await ExecutePromptMenuAsync(auth, workspaceEntry!, cancellation);
    }

    private async Task<int> ExecutePromptMenuAsync(
        StraumrAuth auth,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellationToken)
    {
        AuthEditStateModel state = AuthEditStateModel.FromAuth(auth);

        while (true)
        {
            string? action = PromptEditMenu(state);
            if (action is null)
            {
                return 1;
            }

            if (action == ActionSave)
            {
                if (await TrySaveChangesAsync(auth, state, workspaceEntry))
                {
                    return 0;
                }

                continue;
            }

            await HandleEditActionAsync(state, action, cancellationToken);
        }
    }

    private async Task<int> ExecuteEditorAsync(
        string identifier,
        bool json,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        StraumrAuth auth;
        string tempPath;
        try
        {
            auth = await GetAuthAsync(
                authService, workspaceEntry, identifier, cancellationToken: cancellation);
            tempPath = await CreateEditorFileAsync(
                auth, StraumrJsonContext.Default.StraumrAuth, cancellation,
                authService.PathFor(workspaceEntry, auth.Id));
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, json);
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, json);
            return -1;
        }

        try
        {
            int? exitCode = await LaunchEditorAsync(editor, tempPath, cancellation);
            if (exitCode is not null)
            {
                return exitCode.Value;
            }

            string editedJson = await File.ReadAllTextAsync(tempPath, cancellation);
            StraumrAuth? deserialized;
            try
            {
                deserialized = JsonSerializer.Deserialize<StraumrAuth>(editedJson,
                    StraumrJsonContext.Default.StraumrAuth);
            }
            catch (JsonException ex)
            {
                WriteError($"Invalid auth JSON: {ex.Message}", json);
                return 1;
            }

            if (deserialized is null)
            {
                WriteError("Invalid auth JSON.", json);
                return 1;
            }

            if (deserialized.Id != auth.Id)
            {
                WriteError("Auth ID cannot be changed.", json);
                return 1;
            }

            try
            {
                fileService.CarryCommentsFrom(
                    authService.PathFor(workspaceEntry, deserialized.Id), editedJson);
                await authService.SaveAsync(workspaceEntry, deserialized, cancellation);
                if (json)
                {
                    var result = new AuthListItem(deserialized.Id.ToString(), deserialized.Name, AuthTypeName(deserialized.Config));
                    System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.AuthListItem));
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[green]Updated auth[/] [bold]{deserialized.Name}[/] ({deserialized.Id})");
                }
                return 0;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, json);
                return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
            }
            catch (Exception ex)
            {
                WriteError(ex.Message, json);
                return -1;
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private string? PromptEditMenu(AuthEditStateModel state)
    {
        string nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";
        List<string> menuChoices = new()
            { ActionSave, ActionName, ActionConfigure, ActionAutoRenew };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        return interactiveConsole.Select("Edit auth", menuChoices,
            choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionConfigure => $"Auth: {authDisplay}",
                ActionAutoRenew => $"Auto-renew auth: {autoRenewDisplay}",
                _ => choice
            });
    }

    private async Task<bool> TrySaveChangesAsync(
        StraumrAuth auth,
        AuthEditStateModel state,
        StraumrWorkspaceEntry workspaceEntry)
    {
        if (state.Auth is null)
        {
            interactiveConsole.ShowMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        state.ApplyTo(auth);

        try
        {
            await authService.SaveAsync(workspaceEntry, auth);
            AnsiConsole.MarkupLine($"[green]Updated auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            return true;
        }
        catch (StraumrException ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }
        catch (Exception ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }

        return false;
    }

    private async Task HandleEditActionAsync(
        AuthEditStateModel state,
        string action,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case ActionName:
                {
                    string? updated = interactiveConsole.TextInput("Name", state.Name,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);

                    if (!string.IsNullOrWhiteSpace(updated))
                    {
                        state.Name = updated;
                    }

                    break;
                }
            case ActionConfigure:
                state.Auth = await EditAuthAsync(interactiveConsole, state.Auth);
                break;
            case ActionAutoRenew:
                state.AutoRenewAuth = !state.AutoRenewAuth;
                break;
            case ActionFetch:
                await FetchAuthValueAsync(interactiveConsole, authService, state.Auth, cancellationToken);
                break;
        }
    }
}
