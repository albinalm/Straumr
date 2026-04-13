using System.Diagnostics;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Core.Helpers;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Models;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Models;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens;

public sealed class RequestsScreen(
    TuiInteractiveConsole interactiveConsole,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrOptionsService optionsService,
    ScreenNavigationContext navigationContext,
    StraumrTheme theme)
    : ModelScreen<RequestEntry>(theme,
        screenTitle: "Requests",
        emptyStateText: "No requests found",
        itemTypeNamePlural: "requests")
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

    private StraumrWorkspaceEntry? _workspaceEntry;
    private string? _workspaceDir;
    private bool _editorActive;

    protected override string ModelHintsText => "s Send  c Create  d Delete  e Edit  y Copy";

    protected override void OnInitialized()
    {
        int requestCount = optionsService.Options.Workspaces.Count;
        ShowSuccess($"{requestCount} request{(requestCount == 1 ? string.Empty : "s")} loaded");
    }

    protected override string GetDisplayText(RequestEntry entry) => entry.Display;

    protected override bool IsSameEntry(RequestEntry? left, RequestEntry? right)
        => left?.Id == right?.Id;

    protected override bool HandleModelKeyDown(Key key, RequestEntry? selectedEntry)
    {
        if (_editorActive)
        {
            return true;
        }

        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 's':
                    SendRequest(selectedEntry);
                    return true;
                case 'c':
                    CreateRequest();
                    return true;
                case 'd':
                    DeleteRequest(selectedEntry);
                    return true;
                case 'e':
                    EditRequest(selectedEntry);
                    return true;
                case 'y':
                    CopyRequest(selectedEntry);
                    return true;
            }
        }

        return false;
    }

    private void SendRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot send damaged request \"{selectedEntry.Identifier}\".");
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        navigationContext.SetWorkspace(workspaceEntry);
        navigationContext.SetRequest(selectedEntry.Id);
        NavigateTo<SendScreen>();
    }

    private void CreateRequest()
    {
        if (_editorActive)
        {
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        RequestEditorState state = RequestEditorState.CreateNew();
        RunEditorWithGuard(state, workspaceEntry, null);
    }

    private void DeleteRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        string? confirm = interactiveConsole.Select(
            $"Delete \"{selectedEntry.Identifier}\"?",
            ["Cancel", "Delete"],
            enableFilter: false,
            enableTypeahead: true);

        if (confirm is not "Delete")
        {
            return;
        }

        try
        {
            requestService.DeleteAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($"Deleted request \"{selectedEntry.Identifier}\".");
        }
        catch (StraumrException ex)
        {
            ShowDanger($"{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowDanger($"{ex.Message}");
        }
    }

    private void EditRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null || selectedEntry.IsDamaged)
        {
            return;
        }

        if (_editorActive)
        {
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        StraumrRequest request;
        try
        {
            request = requestService.GetAsync(selectedEntry.Id.ToString(), workspaceEntry).GetAwaiter().GetResult();
        }
        catch (StraumrException ex)
        {
            ShowDanger($"{ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            ShowDanger($"{ex.Message}");
            return;
        }

        RequestEditorState state = RequestEditorState.FromRequest(request);
        RunEditorWithGuard(state, workspaceEntry, request);
    }

    private void CopyRequest(RequestEntry? selectedEntry)
    {
        if (selectedEntry is null)
        {
            return;
        }

        if (selectedEntry.IsDamaged)
        {
            ShowDanger($"Cannot copy damaged request \"{selectedEntry.Identifier}\".");
            return;
        }

        if (!TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry))
        {
            return;
        }

        string? newName = interactiveConsole.TextInput(
            "New name",
            selectedEntry.Name ?? selectedEntry.Identifier,
            validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);

        if (newName is null)
        {
            return;
        }

        try
        {
            requestService.CopyAsync(selectedEntry.Id.ToString(), newName, workspaceEntry).GetAwaiter().GetResult();
            _ = RefreshAsync();
            ShowSuccess($"Copied request to \"{newName}\".");
        }
        catch (StraumrException ex)
        {
            ShowDanger($"{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowDanger($"{ex.Message}");
        }

    }

    protected override IEnumerable<ModelCommand> GetCommands()
    {
        yield return new ModelCommand("send", _ => SendRequest(SelectedEntry), "run");
        yield return new ModelCommand("create", _ => CreateRequest(), "new");
        yield return new ModelCommand("delete", _ => DeleteRequest(SelectedEntry), "rm",
            "remove");
        yield return new ModelCommand("edit", _ => EditRequest(SelectedEntry));
        yield return new ModelCommand("copy", _ => CopyRequest(SelectedEntry),
            "cp");
    }

    protected override void OpenSelectedEntry() { }

    protected override void InspectSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        interactiveConsole.ShowDetails(
            string.Empty,
            [
                ("ID", $"[bold]{SelectedEntry.Id}[/]"),
                ("Name", SelectedEntry.Name is not null ? $"{SelectedEntry.Identifier}" : "[secondary]N/A[/]"),
                ("Method", SelectedEntry.Method?.ToString() ?? "[secondary]N/A[/]"),
                ("Host", SelectedEntry.ShortUriHostname ?? "[secondary]N/A[/]"),
                ("Body", SelectedEntry.BodyTypeText ?? "[secondary]N/A[/]"),
                ("Auth", SelectedEntry.Auth ?? "[secondary]N/A[/]"),
                ("Last Accessed",
                    SelectedEntry.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[warning]N/A[/]"),
            ("Status", SelectedEntry.IsDamaged ? "[danger][bold]Damaged[/][/]" : "[success][bold]Valid[/][/]")
        ]);
    }

    private bool TryGetWorkspaceEntry(out StraumrWorkspaceEntry workspaceEntry)
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            interactiveConsole.ShowMessage("No workspace selected.",
                "You will now be navigated to the workspaces menu. Set an active workspace to continue.");
            NavigateTo<WorkspacesScreen>();
            workspaceEntry = null!;
            return false;
        }

        workspaceEntry = _workspaceEntry;
        return true;
    }

    private void RunEditorWithGuard(RequestEditorState state, StraumrWorkspaceEntry workspaceEntry, StraumrRequest? existingRequest)
    {
        if (_editorActive)
        {
            return;
        }

        _editorActive = true;
        try
        {
            RunRequestEditor(state, workspaceEntry, existingRequest);
        }
        finally
        {
            _editorActive = false;
            FocusList();
        }
    }

    protected override async Task<IReadOnlyList<RequestEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        _workspaceEntry ??= navigationContext.GetWorkspaceEntry();
        if (_workspaceEntry is null)
        {
            interactiveConsole.ShowMessage("No workspace selected.",
                "You will now be navigated to workspaces menu. Set an active workspace to load underlying entities");
            NavigateTo<WorkspacesScreen>();
            return [];
        }

        _workspaceDir = Path.GetDirectoryName(_workspaceEntry.Path);

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.GetWorkspace(_workspaceEntry.Path);
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            interactiveConsole.ShowMessage("Workspace corrupt",
                "Your selected workspace is corrupt. Your active workspace will be cleared and you will be navigated to the workspaces menu. " +
                "From there you can attempt to rescue your workspace using the edit tool.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.MissingEntry)
        {
            interactiveConsole.ShowMessage("Workspace missing",
                "Your selected workspace is missing from disk. Your active workspace will be cleared and you will be navigated to the workspaces menu.");
            NavigateTo<WorkspacesScreen>();
            return [];
        }

        string workspaceName = string.IsNullOrWhiteSpace(workspace.Name)
            ? _workspaceEntry.Id.ToString()
            : workspace.Name;
        UpdateTitle($"Requests - {workspaceName}");

        var items = new List<RequestEntry>();
        foreach (Guid requestId in workspace.Requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(await BuildRequestEntryAsync(requestId));
        }

        return items
            .OrderBy(item => item.Name is null ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private async Task<RequestEntry> BuildRequestEntryAsync(Guid requestId)
    {
        string identifier = requestId.ToString();
        string? name = null;
        string status = "Valid";
        bool isDamaged = false;
        HttpMethod? method = null;
        string? shortUri = null;
        string? bodyTypeText = null;
        string? authText = null;
        DateTimeOffset? lastAccessed = null;
        string display;

        try
        {
            string requestPath = Path.Combine(_workspaceDir!, $"{requestId}.json");
            if (!File.Exists(requestPath))
            {
                throw new StraumrException("Request file missing", StraumrError.MissingEntry);
            }

            StraumrRequest request = await requestService.GetAsync(requestId.ToString(), _workspaceEntry!);
            identifier = request.Name;
            name = request.Name;
            method = request.Method;
            Uri.TryCreate(request.Uri, UriKind.Absolute, out Uri? uri);
            shortUri = uri?.Host ?? request.Uri;
            bodyTypeText = request.BodyType == BodyType.None ? "No body" : request.BodyType.ToString();
            authText = await GetAuthText(request.AuthId?.ToString());
            lastAccessed = request.LastAccessed;

            string line0 = $"[accent]◇ {request.Method}[/] [bold]{request.Name}[/]";
            string line1 = $"  [secondary]{request.Id}[/]";
            string statsRight =
                $"{shortUri} · {bodyTypeText} · {authText}  [/][info]{request.LastAccessed.LocalDateTime:yyyy-MM-dd}[/]";
            string line2 = $"  [secondary]{statsRight}";
            display = $"{line0}\n{line1}\n{line2}";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            isDamaged = true;
            status = "Corrupt";
            display = $"[danger]✖[/] [bold]{requestId}[/]  [danger](Corrupt)[/]\n  [danger]Request file is corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            isDamaged = true;
            status = "Missing";
            display = $"[danger]✖[/] [bold]{requestId}[/]  [warning](Missing)[/]\n  [warning]Request file is missing[/]";
        }

        return new RequestEntry
        {
            Id = requestId,
            Display = display,
            Identifier = identifier,
            Status = status,
            IsDamaged = isDamaged,
            Method = method,
            ShortUriHostname = shortUri,
            BodyTypeText = bodyTypeText,
            Auth = authText,
            LastAccessed = lastAccessed,
            Name = name,
        };
    }

    private async Task<string> GetAuthText(string? authId)
    {
        var authText = "No auth";
        if (authId == null) return authText;

        try
        {
            StraumrAuth auth = await authService.GetAsync(authId, _workspaceEntry!);
            return $"{auth.Name}({auth.Config.Type})";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            authText = "Corrupted auth";
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.MissingEntry or StraumrError.EntryNotFound)
        {
            authText = "Missing auth";
        }
        catch (Exception)
        {
            authText = "Failed to read auth";
        }

        return authText;
    }

    private void RunRequestEditor(
        RequestEditorState state,
        StraumrWorkspaceEntry workspaceEntry,
        StraumrRequest? existingRequest)
    {
        string completionAction = existingRequest is null ? ActionFinish : ActionSave;

        while (true)
        {
            IReadOnlyList<StraumrAuth> auths = authService.ListAsync(workspaceEntry).GetAwaiter().GetResult();
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

            string promptTitle = existingRequest is null ? "Create request" : "Edit request";
            string? action = interactiveConsole.Select(promptTitle, choices,
                choice => DescribeMenuChoice(choice, state, auths, completionAction));

            if (action is null)
            {
                return;
            }

            if (action == completionAction)
            {
                if (TryPersistRequest(state, workspaceEntry, existingRequest))
                {
                    return;
                }

                continue;
            }

            HandleRequestEditAction(action, state, auths);
        }
    }

    private bool TryPersistRequest(
        RequestEditorState state,
        StraumrWorkspaceEntry workspaceEntry,
        StraumrRequest? existingRequest)
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

        try
        {
            if (existingRequest is null)
            {
                StraumrRequest request = state.ToRequest();
                requestService.CreateAsync(request, workspaceEntry).GetAwaiter().GetResult();
                ShowSuccess($"Created request \"{request.Name}\".");
            }
            else
            {
                state.ApplyTo(existingRequest);
                requestService.UpdateAsync(existingRequest, workspaceEntry).GetAwaiter().GetResult();
                ShowSuccess($"Updated request \"{existingRequest.Name}\".");
            }

            RefreshAsync().GetAwaiter().GetResult();
            return true;
        }
        catch (StraumrException ex)
        {
            ShowDanger($"{ex.Message}");
        }
        catch (Exception ex)
        {
            ShowDanger($"{ex.Message}");
        }

        return false;
    }

    private static string DescribeMenuChoice(
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
        string urlDisplay = string.IsNullOrWhiteSpace(state.Uri) ? "not set" : state.Uri;
        string paramsDisplay = state.Params.Count == 0 ? "none" : $"{state.Params.Count}";
        string headersDisplay = state.Headers.Count == 0 ? "none" : $"{state.Headers.Count}";
        string bodyDisplay = state.BodyType == BodyType.None ? "none" : RequestEditingHelpers.BodyTypeDisplayName(state.BodyType);
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

    private void HandleRequestEditAction(
        string action,
        RequestEditorState state,
        IReadOnlyList<StraumrAuth> auths)
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
                string? updated = PromptUrl(state.Uri);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Uri = updated;
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
                state.BodyType = EditBody(state.Headers, state.Bodies, state.BodyType);
                break;
            case ActionAuth:
                state.AuthId = SelectAuth(state.AuthId, auths);
                break;
        }
    }

    private string? PromptUrl(string? current)
    {
        return interactiveConsole.TextInput("URL", current,
            validate: value => IsValidAbsoluteUrl(value) ? null : "Please enter a valid absolute URL.");
    }

    private string? PromptMethod()
    {
        return interactiveConsole.Select("Method",
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

    private Guid? SelectAuth(
        Guid? current,
        IReadOnlyList<StraumrAuth> auths)
    {
        const string noneOption = "None";
        List<string> choices = [noneOption];
        Dictionary<string, Guid> mapping = new(StringComparer.OrdinalIgnoreCase);

        foreach (StraumrAuth auth in auths.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            string label = $"{auth.Name} ({GetAuthTypeName(auth.Config)})";
            choices.Add(label);
            mapping[label] = auth.Id;
        }

        string? selected = interactiveConsole.Select(
            "Auth",
            choices,
            choice =>
            {
                if (choice == noneOption)
                {
                    return "None";
                }

                return choice;
            });

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

    private static string GetAuthTypeName(StraumrAuthConfig? auth)
    {
        return auth switch
        {
            null => "none",
            BearerAuthConfig => "Bearer",
            BasicAuthConfig => "Basic",
            OAuth2Config => "OAuth 2.0",
            CustomAuthConfig => "Custom",
            _ => "none"
        };
    }

    private BodyType EditBody(
        IDictionary<string, string> headers,
        Dictionary<BodyType, string> bodies,
        BodyType currentType)
    {
        while (true)
        {
                string typeDisplay = currentType == BodyType.None
                ? "none"
                : RequestEditingHelpers.BodyTypeDisplayName(currentType);

            bool hasContent = currentType != BodyType.None && bodies.ContainsKey(currentType);
            string contentDisplay = hasContent ? "set" : "empty";

            const string actionBack = "Back";
            const string actionType = "Body type";
            const string actionContent = "Edit body";
            const string actionClear = "Clear body";

            string? action = interactiveConsole.Select(
                "Body",
                [actionBack, actionType, actionContent, actionClear],
                choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    actionContent => $"Content: {contentDisplay}",
                    _ => choice
                });

            if (action is null or actionBack)
            {
                return currentType;
            }

            switch (action)
            {
                case actionType:
                {
                    string? selected = interactiveConsole.Select(
                        "Select body type",
                        ["No body", "JSON", "XML", "Text", "Form URL Encoded", "Multipart Form", "Raw"]);
                    if (selected is not null)
                    {
                        currentType = selected switch
                        {
                            "No body" => BodyType.None,
                            "JSON" => BodyType.Json,
                            "XML" => BodyType.Xml,
                            "Text" => BodyType.Text,
                            "Form URL Encoded" => BodyType.FormUrlEncoded,
                            "Multipart Form" => BodyType.MultipartForm,
                            "Raw" => BodyType.Raw,
                            _ => currentType
                        };

                        SyncContentTypeHeader(headers, currentType);
                    }

                    break;
                }
                case actionContent:
                {
                    string? current = bodies.GetValueOrDefault(currentType);
                    if (currentType == BodyType.FormUrlEncoded)
                    {
                        string? edited = EditFormBody(current);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }
                    else if (currentType == BodyType.MultipartForm)
                    {
                        string? edited = EditMultipartBody(current);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }
                    else
                    {
                        string defaultContent = current ?? string.Empty;
                        string? edited = EditBodyWithEditor(defaultContent);
                        if (edited is not null)
                        {
                            bodies[currentType] = edited;
                        }
                        else
                        {
                            bodies.Remove(currentType);
                        }
                    }

                    break;
                }
                case actionClear:
                    bodies.Remove(currentType);
                    break;
            }
        }
    }

    private string? EditFormBody(string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            const string actionBack = "Back";
            const string actionAdd = "Add or update";
            const string actionRemove = "Remove";
            const string actionList = "List";

            string? action = interactiveConsole.Select(
                "Form fields",
                [actionBack, actionAdd, actionRemove, actionList]);

            if (action is null or actionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case actionAdd:
                {
                    string? key = interactiveConsole.TextInput(
                        "Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    string? existing = fields.GetValueOrDefault(key);
                    string? value = interactiveConsole.TextInput("Field value", existing);
                    if (value is not null)
                    {
                        fields[key] = value;
                    }

                    break;
                }
                case actionRemove:
                {
                    if (fields.Count == 0)
                    {
                        interactiveConsole.ShowMessage("No fields to remove.");
                        break;
                    }

                    string? key = interactiveConsole.Select(
                        "Select field to remove",
                        fields.Keys.OrderBy(k => k).ToList());
                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case actionList:
                {
                    interactiveConsole.ShowTable(
                        "Name",
                        "Value",
                        fields.OrderBy(k => k.Key).Select(kv => (kv.Key, kv.Value)),
                        "No fields set.");
                    break;
                }
            }
        }
    }

    private string? EditMultipartBody(string? currentBody)
    {
        Dictionary<string, string> fields = ParseFormFields(currentBody);

        while (true)
        {
            const string actionBack = "Back";
            const string actionAddText = "Add text field";
            const string actionAddFile = "Add file field";
            const string actionRemove = "Remove";
            const string actionList = "List";

            string? action = interactiveConsole.Select(
                "Multipart form fields",
                [actionBack, actionAddText, actionAddFile, actionRemove, actionList]);

            if (action is null or actionBack)
            {
                return SerializeFormFields(fields);
            }

            switch (action)
            {
                case actionAddText:
                {
                    string? key = interactiveConsole.TextInput(
                        "Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    string? value = interactiveConsole.TextInput("Field value");
                    if (value is not null)
                    {
                        fields[key] = value;
                    }

                    break;
                }
                case actionAddFile:
                {
                    string? key = interactiveConsole.TextInput(
                        "Field name",
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Field name cannot be empty." : null);
                    if (key is null)
                    {
                        break;
                    }

                    string? value = interactiveConsole.TextInput("File path");
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        fields[key] = value.StartsWith('@') ? value : $"@{value}";
                    }

                    break;
                }
                case actionRemove:
                {
                    if (fields.Count == 0)
                    {
                        interactiveConsole.ShowMessage("No fields to remove.");
                        break;
                    }

                    string? key = interactiveConsole.Select(
                        "Select field to remove",
                        fields.Keys.OrderBy(k => k).ToList());
                    if (key is not null)
                    {
                        fields.Remove(key);
                    }

                    break;
                }
                case actionList:
                {
                    IEnumerable<(string Key, string Value)> rows = fields
                        .OrderBy(k => k.Key)
                        .Select(kv =>
                        {
                            string value = kv.Value.StartsWith('@')
                                ? $"@{Path.GetFileName(kv.Value[1..])}"
                                : kv.Value;
                            return (kv.Key, value);
                        });

                    interactiveConsole.ShowTable("Name", "Value", rows, "No fields set.");
                    break;
                }
            }
        }
    }

    private string? EditBodyWithEditor(string content)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            interactiveConsole.ShowMessage("Editor", "Set the $EDITOR environment variable to edit body content.");
            return content;
        }

        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, content);

            Process? process = Process.Start(new ProcessStartInfo(editor, tempPath)
            {
                UseShellExecute = false
            });

            if (process is null)
            {
                interactiveConsole.ShowMessage("Editor", "Failed to launch editor.");
                return content;
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                interactiveConsole.ShowMessage("Editor", "Editor exited with an error. Changes discarded.");
                return content;
            }

            string edited = File.ReadAllText(tempPath);
            return string.IsNullOrWhiteSpace(edited) ? null : edited;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Dictionary<string, string> ParseFormFields(string? body)
    {
        Dictionary<string, string> fields = new();
        if (string.IsNullOrWhiteSpace(body))
        {
            return fields;
        }

        foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq < 0)
            {
                fields[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                fields[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return fields;
    }

    private static string? SerializeFormFields(Dictionary<string, string> fields)
    {
        if (fields.Count == 0)
        {
            return null;
        }

        return string.Join('&', fields.Select(kv =>
            $"{RequestEditingHelpers.EscapeFormFieldComponent(kv.Key)}={RequestEditingHelpers.EscapeFormFieldComponent(kv.Value)}"));
    }

    private static void SyncContentTypeHeader(IDictionary<string, string> headers, BodyType bodyType)
    {
        const string contentTypeHeader = "Content-Type";
        switch (bodyType)
        {
            case BodyType.Json:
                headers[contentTypeHeader] = "application/json";
                break;
            case BodyType.Xml:
                headers[contentTypeHeader] = "application/xml";
                break;
            case BodyType.Text:
                headers[contentTypeHeader] = "text/plain";
                break;
            case BodyType.FormUrlEncoded:
                headers[contentTypeHeader] = "application/x-www-form-urlencoded";
                break;
            case BodyType.MultipartForm:
                headers[contentTypeHeader] = "multipart/form-data";
                break;
            case BodyType.Raw:
            default:
                headers.Remove(contentTypeHeader);
                break;
        }
    }

}
