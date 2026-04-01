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

public class AuthEditCommand(
    IStraumrAuthTemplateService templateService,
    IStraumrAuthService authService) : AsyncCommand<AuthEditCommand.Settings>
{
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrAuthTemplate template;
        try
        {
            template = await templateService.GetAsync(settings.Identifier);
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }

        return await ExecutePromptMenuAsync(template);
    }

    private async Task<int> ExecutePromptMenuAsync(StraumrAuthTemplate template)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        EditableAuthTemplateState state = EditableAuthTemplateState.FromTemplate(template);

        while (true)
        {
            string? action = await PromptEditMenuAsync(console, state);
            if (action is null)
            {
                continue;
            }

            if (action == ActionSave)
            {
                if (await TrySaveChangesAsync(template, state))
                {
                    return 0;
                }

                continue;
            }

            await HandleEditActionAsync(console, state, action);
        }
    }

    private static async Task<string?> PromptEditMenuAsync(
        EscapeCancellableConsole console, EditableAuthTemplateState state)
    {
        var nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        var menuChoices = new List<string> { ActionSave, ActionName, ActionConfigure };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Edit auth preset")
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

    private async Task<bool> TrySaveChangesAsync(StraumrAuthTemplate template, EditableAuthTemplateState state)
    {
        if (state.Auth is null)
        {
            ShowTransientMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        state.ApplyTo(template);

        try
        {
            await templateService.UpdateAsync(template);
            AnsiConsole.MarkupLine($"[green]Updated auth preset[/] [bold]{template.Name}[/] ({template.Id})");
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
        EscapeCancellableConsole console, EditableAuthTemplateState state, string action)
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
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
    }

    private sealed class EditableAuthTemplateState
    {
        private EditableAuthTemplateState(string name, StraumrAuthConfig? auth)
        {
            Name = name;
            Auth = auth;
        }

        public string Name { get; set; }
        public StraumrAuthConfig? Auth { get; set; }

        public static EditableAuthTemplateState FromTemplate(StraumrAuthTemplate template)
        {
            return new EditableAuthTemplateState(template.Name, template.Config);
        }

        public void ApplyTo(StraumrAuthTemplate template)
        {
            template.Name = Name;
            template.Config = Auth ?? throw new InvalidOperationException("Auth config must be set.");
        }
    }
}