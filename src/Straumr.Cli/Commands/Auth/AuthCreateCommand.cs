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

namespace Straumr.Cli.Commands.Auth;

public class AuthCreateCommand(
    IStraumrOptionsService optionsService,
    IStraumrAuthTemplateService templateService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthCreateCommand.Settings>
{
    private const string ActionFinish = "Finish";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";

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
        var state = new CreateAuthTemplateState(settings.Name);

        while (true)
        {
            string? action = await PromptCreateMenuAsync(console, state);
            if (action is null)
            {
                continue;
            }

            if (action == ActionFinish)
            {
                if (await TryCreateTemplateAsync(state))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(console, state, action);
        }
    }

    private static async Task<string?> PromptCreateMenuAsync(
        EscapeCancellableConsole console, CreateAuthTemplateState state)
    {
        var nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        var menuChoices = new List<string> { ActionFinish, ActionName, ActionConfigure };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Auth template setup")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionConfigure => $"Auth: {authDisplay}",
                _ => choice
            })
            .AddChoices(menuChoices);

        return await PromptAsync(console, prompt);
    }

    private async Task<bool> TryCreateTemplateAsync(CreateAuthTemplateState state)
    {
        if (state.Auth is null)
        {
            ShowTransientMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        StraumrAuthTemplate template = state.ToTemplate();
        try
        {
            await templateService.CreateAsync(template);
            AnsiConsole.MarkupLine($"[green]Created auth preset[/] [bold]{template.Name}[/] ({template.Id})");
            return true;
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private async Task HandleCreateActionAsync(
        EscapeCancellableConsole console, CreateAuthTemplateState state, string action)
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
            case ActionFetch:
                await FetchAuthValueAsync(authService, state.Auth);
                break;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")]
        [Description("Name of the auth template to create")]
        public required string Name { get; set; }
    }

    private sealed class CreateAuthTemplateState(string name)
    {
        public string Name { get; set; } = name;
        public StraumrAuthConfig? Auth { get; set; }

        public StraumrAuthTemplate ToTemplate()
        {
            return new StraumrAuthTemplate
            {
                Name = Name,
                Config = Auth ?? throw new InvalidOperationException("Auth must be configured before saving")
            };
        }
    }
}
