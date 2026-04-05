using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Console.PromptHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Auth;

public class AuthCreateCommand(
    IStraumrOptionsService optionsService,
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
        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
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
