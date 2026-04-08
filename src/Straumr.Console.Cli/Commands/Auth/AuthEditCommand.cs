using System.Text.Json;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Console;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Console.PromptHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Auth;

public class AuthEditCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService) : AsyncCommand<AuthEditCommand.Settings>
{
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";
    private const string ActionAutoRenew = "Auto-renew auth";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            optionsService.Options.CurrentWorkspace = resolved;
        }

        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        if (settings.UseEditor || settings.Json)
        {
            return await ExecuteEditorAsync(settings.Identifier, settings.Json, cancellation);
        }

        StraumrAuth auth;
        try
        {
            auth = await authService.GetAsync(settings.Identifier);
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

        return await ExecutePromptMenuAsync(auth);
    }

    private async Task<int> ExecutePromptMenuAsync(StraumrAuth auth)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        EditableAuthState state = EditableAuthState.FromAuth(auth);

        while (true)
        {
            string? action = await PromptEditMenuAsync(console, state);
            if (action is null)
            {
                continue;
            }

            if (action == ActionSave)
            {
                if (await TrySaveChangesAsync(auth, state))
                {
                    return 0;
                }

                continue;
            }

            await HandleEditActionAsync(console, state, action);
        }
    }

    private async Task<int> ExecuteEditorAsync(string identifier, bool json, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        Guid authId;
        string tempPath;
        try
        {
            (authId, tempPath) = await authService.PrepareEditAsync(identifier);
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

            if (deserialized.Id != authId)
            {
                WriteError("Auth ID cannot be changed.", json);
                return 1;
            }

            try
            {
                authService.ApplyEdit(authId, tempPath);
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

    private static async Task<string?> PromptEditMenuAsync(
        EscapeCancellableConsole console, EditableAuthState state)
    {
        var nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";
        var menuChoices = new List<string> { ActionSave, ActionName, ActionConfigure, ActionAutoRenew };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Edit auth")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionConfigure => $"Auth: {authDisplay}",
                ActionAutoRenew => $"Auto-renew auth: {autoRenewDisplay}",
                _ => choice
            })
            .AddChoices(menuChoices);

        return await PromptAsync(console, prompt);
    }

    private async Task<bool> TrySaveChangesAsync(StraumrAuth auth, EditableAuthState state)
    {
        if (state.Auth is null)
        {
            ShowTransientMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        state.ApplyTo(auth);

        try
        {
            await authService.UpdateAsync(auth);
            AnsiConsole.MarkupLine($"[green]Updated auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            return true;
        }
        catch (StraumrException ex)
        {
            ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }

        return false;
    }

    private async Task HandleEditActionAsync(
        EscapeCancellableConsole console, EditableAuthState state, string action)
    {
        switch (action)
        {
            case ActionName:
            {
                TextPrompt<string> prompt = new TextPrompt<string>("Name")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Name cannot be empty.")
                        : ValidationResult.Success());
                string? updated = await PromptTextAsync(console, prompt, state.Name);

                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionConfigure:
                state.Auth = await EditAuthAsync(console, state.Auth);
                break;
            case ActionAutoRenew:
                state.AutoRenewAuth = !state.AutoRenewAuth;
                break;
            case ActionFetch:
                await FetchAuthValueAsync(authService, state.Auth);
                break;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the auth to edit")]
        public required string Identifier { get; set; }

        [CommandOption("-e|--editor")]
        [Description("Open the auth in the default editor instead of interactive prompts")]
        public bool UseEditor { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }

        [CommandOption("-j|--json")]
        [Description("Open in editor and output the updated auth as JSON on success; implies --editor")]
        public bool Json { get; set; }
    }

    private sealed class EditableAuthState
    {
        private EditableAuthState(string name, StraumrAuthConfig? auth, bool autoRenewAuth)
        {
            Name = name;
            Auth = auth;
            AutoRenewAuth = autoRenewAuth;
        }

        public string Name { get; set; }
        public StraumrAuthConfig? Auth { get; set; }
        public bool AutoRenewAuth { get; set; }

        public static EditableAuthState FromAuth(StraumrAuth auth)
        {
            return new EditableAuthState(auth.Name, auth.Config, auth.AutoRenewAuth);
        }

        public void ApplyTo(StraumrAuth auth)
        {
            auth.Name = Name;
            auth.Config = Auth ?? throw new InvalidOperationException("Auth config must be set.");
            auth.AutoRenewAuth = AutoRenewAuth;
        }
    }
}
