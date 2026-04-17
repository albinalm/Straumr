using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Console.Shared.Interfaces;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Auth;

public class AuthCreateCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService,
    IInteractiveConsole interactiveConsole)
    : AsyncCommand<AuthCreateCommand.Settings>
{
    private const string ActionFinish = "Finish";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";
    private const string ActionAutoRenew = "Auto-renew auth";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
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

        if (settings.Type is not null && workspaceEntry is not null)
        {
            return await ExecuteInlineAsync(settings, workspaceEntry);
        }

        CreateAuthState state = new CreateAuthState(settings.Name ?? string.Empty);

        while (true)
        {
            string? action = PromptCreateMenu(state);
            if (action is null)
            {
                return 1;
            }

            if (action == ActionFinish && workspaceEntry is not null)
            {
                if (await TryCreateAuthAsync(state, workspaceEntry))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(state, action);
        }
    }

    private async Task<int> ExecuteInlineAsync(Settings settings, StraumrWorkspaceEntry workspaceEntry)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            WriteError("A name is required when creating auth inline.", settings.Json);
            return 1;
        }

        if (!settings.TryGetAutoRenewOverride(message => WriteError(message, settings.Json), out bool? autoRenewOverride))
        {
            return 1;
        }

        StraumrAuthConfig? config = AuthInlineConfigBuilder.Build(settings, message => WriteError(message, settings.Json));
        if (config is null)
        {
            return 1;
        }

        StraumrAuth auth = new StraumrAuth
        {
            Name = settings.Name!,
            Config = config,
            AutoRenewAuth = autoRenewOverride ?? true
        };

        try
        {
            await authService.CreateAsync(auth, workspaceEntry);

            if (settings.Json)
            {
                AuthListItem result = new AuthListItem(auth.Id.ToString(), auth.Name, AuthTypeName(auth.Config));
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.AuthListItem));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{Markup.Escape(auth.Name)}[/] ({auth.Id})");
            }

            return 0;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return 1;
        }
    }

    private string? PromptCreateMenu(CreateAuthState state)
    {
        string nameDisplay = string.IsNullOrWhiteSpace(state.Name)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";
        List<string> menuChoices = new List<string> { ActionFinish, ActionName, ActionConfigure, ActionAutoRenew };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        return interactiveConsole.Select("Auth setup", menuChoices,
            choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionConfigure => $"Auth: {authDisplay}",
                ActionAutoRenew => $"Auto-renew auth: {autoRenewDisplay}",
                _ => choice
            });
    }

    private async Task<bool> TryCreateAuthAsync(CreateAuthState state, StraumrWorkspaceEntry workspaceEntry)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("[red]A name is required.[/]");
            return false;
        }

        if (state.Auth is null)
        {
            interactiveConsole.ShowMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        StraumrAuth auth = state.ToAuth();
        try
        {
            await authService.CreateAsync(auth, workspaceEntry);
            AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            return true;
        }
        catch (Exception ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private async Task HandleCreateActionAsync(CreateAuthState state, string action)
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
                await FetchAuthValueAsync(interactiveConsole, authService, state.Auth);
                break;
        }
    }

    public sealed class Settings : AuthInlineSettingsBase
    {
        [CommandArgument(0, "[Name]")]
        [Description("Name of the auth to create")]
        public string? Name { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the created auth as JSON")]
        public bool Json { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }

    private sealed class CreateAuthState(string name)
    {
        public string Name { get; set; } = name;
        public StraumrAuthConfig? Auth { get; set; }
        public bool AutoRenewAuth { get; set; } = true;

        public StraumrAuth ToAuth()
        {
            return new StraumrAuth
            {
                Name = Name,
                Config = Auth ?? throw new InvalidOperationException("Auth must be configured before saving"),
                AutoRenewAuth = AutoRenewAuth
            };
        }
    }
}
