using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Cli.Infrastructure;
using Straumr.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Helpers.ConsoleHelpers;
using static Straumr.Cli.Console.PromptHelpers;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Auth;

public class AuthCreateCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
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

        if (settings.Type is not null)
        {
            return await ExecuteInlineAsync(settings);
        }

        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        var state = new CreateAuthState(settings.Name ?? string.Empty);

        while (true)
        {
            string? action = await PromptCreateMenuAsync(console, state);
            if (action is null)
            {
                continue;
            }

            if (action == ActionFinish)
            {
                if (await TryCreateAuthAsync(state))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(console, state, action);
        }
    }

    private async Task<int> ExecuteInlineAsync(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            WriteError("A name is required when creating auth inline.", settings.Json);
            return 1;
        }

        StraumrAuthConfig config;
        switch (settings.Type!.ToLowerInvariant())
        {
            case "bearer":
                config = new BearerAuthConfig
                {
                    Token = settings.Secret ?? string.Empty,
                    Prefix = settings.Prefix ?? "Bearer"
                };
                break;
            case "basic":
                config = new BasicAuthConfig
                {
                    Username = settings.Username ?? string.Empty,
                    Password = settings.Password ?? string.Empty
                };
                break;
            default:
                WriteError($"Unknown auth type: {settings.Type}. Use bearer or basic for inline creation.", settings.Json);
                return 1;
        }

        var auth = new StraumrAuth
        {
            Name = settings.Name!,
            Config = config,
            AutoRenewAuth = settings.AutoRenew
        };

        try
        {
            await authService.CreateAsync(auth);

            if (settings.Json)
            {
                var result = new AuthListItem(auth.Id.ToString(), auth.Name, AuthTypeName(auth.Config));
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.AuthListItem));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            }

            return 0;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return 1;
        }
    }

    private static async Task<string?> PromptCreateMenuAsync(
        EscapeCancellableConsole console, CreateAuthState state)
    {
        string nameDisplay = string.IsNullOrWhiteSpace(state.Name)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";
        var menuChoices = new List<string> { ActionFinish, ActionName, ActionConfigure, ActionAutoRenew };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Auth setup")
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

    private async Task<bool> TryCreateAuthAsync(CreateAuthState state)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            ShowTransientMessage("[red]A name is required.[/]");
            return false;
        }

        if (state.Auth is null)
        {
            ShowTransientMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        StraumrAuth auth = state.ToAuth();
        try
        {
            await authService.CreateAsync(auth);
            AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            return true;
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private async Task HandleCreateActionAsync(
        EscapeCancellableConsole console, CreateAuthState state, string action)
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
        [CommandArgument(0, "[Name]")]
        [Description("Name of the auth to create")]
        public string? Name { get; set; }

        [CommandOption("-t|--type")]
        [Description("Auth type for non-interactive creation: bearer, basic")]
        public string? Type { get; set; }

        [CommandOption("-s|--secret")]
        [Description("Token or secret value (bearer: token value)")]
        public string? Secret { get; set; }

        [CommandOption("--prefix")]
        [Description("Token prefix for bearer auth (default: Bearer)")]
        public string? Prefix { get; set; }

        [CommandOption("-u|--username")]
        [Description("Username for basic auth")]
        public string? Username { get; set; }

        [CommandOption("-p|--password")]
        [Description("Password for basic auth")]
        public string? Password { get; set; }

        [CommandOption("--no-auto-renew")]
        [Description("Disable auto-renewal of auth tokens")]
        public bool NoAutoRenew { get; set; }

        public bool AutoRenew => !NoAutoRenew;

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
