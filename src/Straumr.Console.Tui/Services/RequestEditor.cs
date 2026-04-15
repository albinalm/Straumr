using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Models;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Services.Interfaces;
using Straumr.Core.Enums;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

public sealed class RequestEditor(
    TuiInteractiveConsole interactiveConsole,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IBodyEditor bodyEditor,
    ITuiOperationExecutor executor) : IRequestEditor
{
    private const string ActionFinish = "Finish";
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionUrl = "Edit URL";
    private const string ActionMethod = "Edit method";
    private const string ActionParams = "Edit params";
    private const string ActionHeaders = "Edit headers";
    private const string ActionBody = "Edit body";
    private const string ActionAuth = "Edit auth";

    public void Run(RequestEditorContext context)
    {
        RequestEditorState state = context.Mode == RequestEditorMode.Create
            ? RequestEditorState.CreateNew()
            : RequestEditorState.FromRequest(
                context.ExistingRequest ?? throw new InvalidOperationException("Request required."));

        RunLoop(state, context);
    }

    private void RunLoop(RequestEditorState state, RequestEditorContext context)
    {
        string completionAction = context.Mode == RequestEditorMode.Create ? ActionFinish : ActionSave;
        string promptTitle = context.Mode == RequestEditorMode.Create ? "Create request" : "Edit request";

        while (true)
        {
            if (!executor.TryExecute(
                    () => authService.ListAsync(context.WorkspaceEntry).GetAwaiter().GetResult(),
                    context.ShowDanger,
                    out IReadOnlyList<StraumrAuth>? auths) || auths is null)
            {
                return;
            }

            List<string> choices =
            [
                completionAction,
                ActionName,
                ActionUrl,
                ActionMethod,
                ActionParams,
                ActionHeaders,
                ActionBody,
                ActionAuth
            ];

            string? action = interactiveConsole.Select(
                promptTitle,
                choices,
                choice => DescribeChoice(choice, state, auths, completionAction));

            if (action is null)
            {
                return;
            }

            if (action == completionAction)
            {
                if (TryPersist(state, context))
                {
                    return;
                }

                continue;
            }

            HandleAction(action, state, auths);
        }
    }

    private bool TryPersist(RequestEditorState state, RequestEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("Validation", "A name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.Uri))
        {
            interactiveConsole.ShowMessage("Validation", "A URL is required.");
            return false;
        }

        if (context.Mode == RequestEditorMode.Create)
        {
            StraumrRequest request = state.ToRequest();
            if (!executor.TryExecute(
                    () => requestService.CreateAsync(request, context.WorkspaceEntry).GetAwaiter().GetResult(),
                    context.ShowDanger))
            {
                return false;
            }

            _ = context.RefreshEntries();
            context.ShowSuccess($"Created request \"{request.Name}\"");
            return true;
        }

        StraumrRequest existing = context.ExistingRequest ?? throw new InvalidOperationException("Request required.");
        state.ApplyTo(existing);
        if (!executor.TryExecute(
                () => requestService.UpdateAsync(existing, context.WorkspaceEntry).GetAwaiter().GetResult(),
                context.ShowDanger))
        {
            return false;
        }

        _ = context.RefreshEntries();
        context.ShowSuccess($"Updated request \"{existing.Name}\"");
        return true;
    }

    private static string DescribeChoice(
        string choice,
        RequestEditorState state,
        IReadOnlyList<StraumrAuth> auths,
        string completionAction)
    {
        if (choice == completionAction)
        {
            return choice;
        }

        string nameDisplay = string.IsNullOrWhiteSpace(state.Name) ? "not set" : state.Name;
        string urlDisplay = string.IsNullOrWhiteSpace(state.Uri) ? "not set" : state.GetDisplayUri();
        string paramsDisplay = state.Params.Count == 0 ? "none" : $"{state.Params.Count}";
        string headersDisplay = state.Headers.Count == 0 ? "none" : $"{state.Headers.Count}";
        string bodyDisplay = state.BodyType == BodyType.None
            ? "none"
            : RequestEditingHelpers.BodyTypeDisplayName(state.BodyType);
        string authDisplay = GetAuthLabel(state.AuthId, auths);

        return choice switch
        {
            ActionName => $"Name: {nameDisplay}",
            ActionUrl => $"URL: {urlDisplay}",
            ActionMethod => $"Method: {state.Method}",
            ActionParams => $"Params: {paramsDisplay}",
            ActionHeaders => $"Headers: {headersDisplay}",
            ActionBody => $"Body: {bodyDisplay}",
            ActionAuth => $"Auth: {authDisplay}",
            _ => choice
        };
    }

    private void HandleAction(string action, RequestEditorState state, IReadOnlyList<StraumrAuth> auths)
    {
        switch (action)
        {
            case ActionName:
            {
                string? updated = interactiveConsole.TextInput(
                    "Name",
                    state.Name,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionUrl:
            {
                string? updated = PromptUrl(state.GetDisplayUri());
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.SetDisplayUri(updated);
                }

                break;
            }
            case ActionMethod:
            {
                string? selected = PromptMethod();
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    state.Method = selected;
                }

                break;
            }
            case ActionParams:
                EditKeyValuePairs("Params", state.Params);
                break;
            case ActionHeaders:
                EditKeyValuePairs("Headers", state.Headers);
                break;
            case ActionBody:
                state.BodyType = bodyEditor.Edit(state.Headers, state.Bodies, state.BodyType);
                break;
            case ActionAuth:
                state.AuthId = SelectAuth(state.AuthId, auths);
                break;
        }
    }

    private string? PromptUrl(string? current)
    {
        return interactiveConsole.TextInput(
            "URL",
            current,
            validate: value => IsValidAbsoluteUrl(value) ? null : "Please enter a valid absolute URL.");
    }

    private string? PromptMethod()
    {
        return interactiveConsole.Select(
            "Method",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"]);
    }

    private static bool IsValidAbsoluteUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = SecretHelpers.SecretPattern.Replace(value, "secret");
        return Uri.TryCreate(normalized, UriKind.Absolute, out _);
    }

    private void EditKeyValuePairs(string title, IDictionary<string, string> items)
    {
        if (interactiveConsole.TryEditKeyValuePairs(title, items))
        {
            return;
        }

        string lowerTitle = title.ToLowerInvariant();
        while (true)
        {
            string? action = interactiveConsole.Select(title, ["Back", "Add or update", "Remove", "List"]);
            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Add or update":
                {
                    string? key = interactiveConsole.TextInput(
                        $"{title} name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    items.TryGetValue(key, out string? existing);
                    string? value = interactiveConsole.TextInput($"{title} value", existing);
                    if (value is not null)
                    {
                        items[key] = value;
                    }

                    break;
                }
                case "Remove":
                {
                    if (items.Count == 0)
                    {
                        interactiveConsole.ShowMessage($"No {lowerTitle} to remove.");
                        break;
                    }

                    string? key = interactiveConsole.Select("Select to remove", items.Keys.OrderBy(k => k).ToList());
                    if (key is not null)
                    {
                        items.Remove(key);
                    }

                    break;
                }
                case "List":
                {
                    interactiveConsole.ShowTable(
                        "Name",
                        "Value",
                        items.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        $"No {lowerTitle} set.");
                    break;
                }
            }
        }
    }

    private Guid? SelectAuth(Guid? current, IReadOnlyList<StraumrAuth> auths)
    {
        const string noneOption = "None";
        List<string> choices = [noneOption];
        Dictionary<string, Guid> mapping = new(StringComparer.OrdinalIgnoreCase);

        foreach (StraumrAuth auth in auths.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            string label = $"{auth.Name} ({AuthDisplayFormatter.GetAuthTypeName(auth.Config)})";
            choices.Add(label);
            mapping[label] = auth.Id;
        }

        string? selected = interactiveConsole.Select("Auth", choices);

        if (selected is null or noneOption)
        {
            return null;
        }

        return mapping.TryGetValue(selected, out Guid id) ? id : current;
    }

    private static string GetAuthLabel(Guid? authId, IReadOnlyList<StraumrAuth> auths)
    {
        if (authId is null)
        {
            return "none";
        }

        StraumrAuth? auth = auths.FirstOrDefault(a => a.Id == authId.Value);
        return auth is null ? "unknown" : auth.Name;
    }
}
