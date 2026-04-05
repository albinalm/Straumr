using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Cli.Console.PromptHelpers;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestEditCommand(
    IStraumrOptionsService optionsService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService)
    : AsyncCommand<RequestEditCommand.Settings>
{
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionUrl = "Edit URL";
    private const string ActionMethod = "Edit method";
    private const string ActionParams = "Edit params";
    private const string ActionHeaders = "Edit headers";
    private const string ActionBody = "Edit body";
    private const string ActionAuth = "Edit auth";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        if (settings.UseEditor)
        {
            return await ExecuteEditorAsync(settings.Identifier, cancellation);
        }

        StraumrRequest request;
        try
        {
            request = await requestService.GetAsync(settings.Identifier);
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

        return await ExecutePromptMenuAsync(request, cancellation);
    }

    private async Task<int> ExecutePromptMenuAsync(StraumrRequest request, CancellationToken cancellation)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        IReadOnlyList<StraumrAuth> auths = await authService.ListAsync();
        EditableRequestState state = EditableRequestState.FromRequest(request);

        while (true)
        {
            string? action = await PromptEditMenuAsync(console, state, auths);
            if (action is null)
            {
                continue;
            }

            if (action == ActionSave)
            {
                if (await TrySaveChangesAsync(request, state))
                {
                    return 0;
                }

                continue;
            }

            await HandleEditActionAsync(console, state, action, cancellation);
        }
    }

    private static async Task<string?> PromptEditMenuAsync(
        EscapeCancellableConsole console, EditableRequestState state, IReadOnlyList<StraumrAuth> auths)
    {
        var nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string urlDisplay = string.IsNullOrWhiteSpace(state.Uri)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Uri)}[/]";
        var methodDisplay = $"[blue]{state.Method}[/]";
        string paramsDisplay = state.Params.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Params.Count}[/]";
        string headersDisplay = state.Headers.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Headers.Count}[/]";
        string bodyDisplay = state.BodyType == BodyType.None
            ? "[grey]none[/]"
            : $"[blue]{BodyTypeDisplayName(state.BodyType)}[/]";
        string authDisplay = AuthDisplayName(state.AuthId, auths);

        var menuChoices = new List<string>
        {
            ActionSave, ActionName, ActionUrl, ActionMethod, ActionParams, ActionHeaders, ActionBody, ActionAuth
        };

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Edit request")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionUrl => $"URL: {urlDisplay}",
                ActionMethod => $"Method: {methodDisplay}",
                ActionParams => $"Params: {paramsDisplay}",
                ActionHeaders => $"Headers: {headersDisplay}",
                ActionBody => $"Body: {bodyDisplay}",
                ActionAuth => $"Auth: {authDisplay}",
                _ => choice
            })
            .AddChoices(menuChoices);

        return await PromptAsync(console, prompt);
    }

    private async Task<bool> TrySaveChangesAsync(StraumrRequest request, EditableRequestState state)
    {
        state.ApplyTo(request);

        try
        {
            await requestService.UpdateAsync(request);
            AnsiConsole.MarkupLine($"[green]Updated request[/] [bold]{request.Name}[/] ({request.Id})");
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
        EscapeCancellableConsole console, EditableRequestState state, string action, CancellationToken cancellation)
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
            case ActionUrl:
            {
                string? updated = await PromptUrlAsync(console, state.Uri);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Uri = updated;
                }

                break;
            }
            case ActionMethod:
            {
                string? selected = await PromptMethodAsync(console);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    state.Method = selected;
                }

                break;
            }
            case ActionParams:
                await EditKeyValuePairsAsync(console, "Params", state.Params);
                break;
            case ActionHeaders:
                await EditKeyValuePairsAsync(console, "Headers", state.Headers);
                break;
            case ActionBody:
                state.BodyType =
                    await EditBodyAsync(console, state.Headers, state.Bodies, state.BodyType, cancellation);
                break;
            case ActionAuth:
            {
                StraumrAuth? selected = await SelectAuthAsync(console, authService);
                state.AuthId = selected?.Id;
                break;
            }
        }
    }

    private async Task<int> ExecuteEditorAsync(string identifier, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        Guid requestId;
        string tempPath;
        try
        {
            (requestId, tempPath) = await requestService.PrepareEditAsync(identifier);
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

        try
        {
            int? exitCode = await LaunchEditorAsync(editor, tempPath, cancellation);
            if (exitCode is not null)
            {
                return exitCode.Value;
            }

            string editedJson = await File.ReadAllTextAsync(tempPath, cancellation);
            StraumrRequest? deserializedJson;
            try
            {
                deserializedJson = JsonSerializer.Deserialize<StraumrRequest>(editedJson,
                    StraumrJsonContext.Default.StraumrRequest);
            }
            catch (JsonException ex)
            {
                AnsiConsole.MarkupLine($"[red]Invalid request JSON: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }

            if (deserializedJson is null)
            {
                AnsiConsole.MarkupLine("[red]Invalid request JSON.[/]");
                return 1;
            }

            if (deserializedJson.Id != requestId)
            {
                AnsiConsole.MarkupLine("[red]Request ID cannot be changed.[/]");
                return 1;
            }

            try
            {
                requestService.ApplyEdit(requestId, tempPath);
                AnsiConsole.MarkupLine(
                    $"[green]Updated request[/] [bold]{deserializedJson.Name}[/] ({deserializedJson.Id})");
                return 0;
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
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the request to edit")]
        public required string Identifier { get; set; }

        [CommandOption("-e|--editor")]
        [Description("Open the request in the default editor instead of interactive prompts")]
        public bool UseEditor { get; set; }
    }

    private sealed class EditableRequestState
    {
        private EditableRequestState(string name, string uri, string method, Dictionary<string, string> parameters,
            Dictionary<string, string> headers, Dictionary<BodyType, string> bodies, BodyType bodyType,
            Guid? authId)
        {
            Name = name;
            Uri = uri;
            Method = method;
            Params = parameters;
            Headers = headers;
            Bodies = bodies;
            BodyType = bodyType;
            AuthId = authId;
        }

        public string Name { get; set; }
        public string Uri { get; set; }
        public string Method { get; set; }
        public Dictionary<string, string> Params { get; }
        public Dictionary<string, string> Headers { get; }
        public Dictionary<BodyType, string> Bodies { get; }
        public BodyType BodyType { get; set; }
        public Guid? AuthId { get; set; }

        public static EditableRequestState FromRequest(StraumrRequest request)
        {
            return new EditableRequestState(
                request.Name,
                request.Uri,
                request.Method.Method,
                new Dictionary<string, string>(request.Params, StringComparer.Ordinal),
                new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase),
                new Dictionary<BodyType, string>(request.Bodies),
                request.BodyType,
                request.AuthId);
        }

        public void ApplyTo(StraumrRequest request)
        {
            request.Name = Name;
            request.Uri = Uri;
            request.Method = new HttpMethod(Method);
            request.Params = new Dictionary<string, string>(Params, StringComparer.Ordinal);
            request.Headers = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase);
            request.BodyType = BodyType;
            request.Bodies = new Dictionary<BodyType, string>(Bodies);
            request.AuthId = AuthId;
        }
    }
}
