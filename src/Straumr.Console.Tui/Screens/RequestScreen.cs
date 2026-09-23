using System.Text.Json;
using Straumr.Console.Tui.Screens.Components.Request;
using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;
using ScrollableContent = Straumr.Console.Tui.Screens.Components.Shared.ScrollableContent;

namespace Straumr.Console.Tui.Screens;

public sealed class RequestScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Requests);
    private const int BodyPage = 0;
    private const int ParamsPage = 2;
    private const int NetworkPage = 3;
    private readonly ScrollableContent _authView;
    private readonly State<RequestAuthenticationModel?> _authentication = new(null);
    private readonly IStraumrAuthService _auths;
    private readonly State<int> _count = new(0);
    private readonly ExternalEditorService _editor;
    private readonly State<string> _emptyMessage = new("Loading requests…");
    private readonly IStraumrFileService _files;
    private readonly ResourceFilter _filter;
    private readonly ResourceList _list;
    private readonly State<bool> _loadError = new(false);
    private readonly State<bool> _loading = new(true);
    private readonly State<int> _matchCount = new(0);
    private readonly State<string> _query = new(string.Empty);
    private readonly BodyPreviewService _requestBody;
    private readonly HeadersView _requestHeaders;
    private readonly PreviewPane _requestPreview;
    private readonly IStraumrRequestService _requests;
    private readonly BodyPreviewService _responseBody;
    private readonly State<bool> _responseFailed = new(false);
    private readonly HeadersView _responseHeaders;
    private readonly PreviewPane _responsePreview;
    private readonly HeadersView _sentHeaders;
    private readonly State<string> _responseSummary = new("Not sent");
    private readonly Dictionary<(Guid Workspace, Guid Request), StraumrResponse> _responses = [];
    private readonly IStraumrSecretService _secrets;
    private readonly ScrollableContent _referencesView;
    private readonly IStraumrVariableService _variables;
    private readonly Visual _sections;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly IStraumrSettingsService _settings;
    private readonly PaneSplits _splits;

    private readonly IStraumrStateService _state;
    private readonly State<List<RequestScreenItemModel>> _visible = new([]);
    private readonly IStraumrWorkspaceService _workspaces;
    private Guid? _displayedId;

    private Guid? _editingId;

    private string? _editorNotice;
    private RequestEditor? _editorView;
    private List<RequestScreenItemModel> _items = [];

    private Guid? _pendingDeleteId;

    private Func<CancellationToken, Task<TuiCommandResultModel>>? _pendingSave;
    private Guid? _pendingSend;
    private RequestResponseView? _responseView;
    private bool _savePaneLayout;
    private CancellationTokenSource? _sendCancellation;
    private StraumrWorkspaceEntry? _workspace;

    private IReadOnlyList<StraumrAuth> _workspaceAuths = [];

    public RequestScreen(IStraumrStateService state, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, IStraumrSecretService secrets,
        IStraumrVariableService variables, IStraumrFileService files, IStraumrSettingsService settings,
        ExternalEditorService editor)
    {
        (_state, _workspaces, _requests, _auths, _secrets, _variables, _files, _settings, _editor) =
            (state, workspaces, requests, auths, secrets, variables, files, settings, editor);
        _requestHeaders = new HeadersView(Notify);
        _responseHeaders = new HeadersView(Notify);
        _sentHeaders = new HeadersView(Notify);
        _requestPreview = new PreviewPane(PreviewPanePageModel.Text("Body"),
            PreviewPanePageModel.Custom("Headers", _requestHeaders.Root, () => _requestHeaders.FocusTarget),
            PreviewPanePageModel.Text("Params"));
        _responsePreview = new PreviewPane(PreviewPanePageModel.Text("Body"),
            PreviewPanePageModel.Custom("Headers", _responseHeaders.Root, () => _responseHeaders.FocusTarget),
            PreviewPanePageModel.Custom("Sent headers", _sentHeaders.Root, () => _sentHeaders.FocusTarget),
            PreviewPanePageModel.Text("Network"));
        _requestBody = new BodyPreviewService(_requestPreview, Notify,
            () => BodyOptions with { Format = ResponseBodyFormat.Beautify });
        _responseBody = new BodyPreviewService(_responsePreview, Notify, () => BodyOptions);
        _responsePreview.Root.AddCommand(ActionCommand("Fullscreen", OpenResponse,
            () => _workspace is { } workspace && SelectedItem is { IsBroken: false } item
                  && _responses.ContainsKey((workspace.Id, item.Id)) && !_responsePreview.Root.IsTyping(),
            consumes: false));
        StraumrPaneLayout paneLayout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey)
                                       ?? new StraumrPaneLayout();
        _splits = new PaneSplits(paneLayout.Panels, paneLayout.Sections, paneLayout.Stack);
        _splits.Changed += QueuePaneLayoutSave;
        _list = new ResourceList([], ResourceScreenLayoutHelpers.Message(
                new TextBlock(() => _emptyMessage.Value)
                    .Style(() => _loadError.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedText)
                    .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            TuiKeybindHelpers.CombinedLabel("Edit", "Request.Edit"));
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => RequestEdit();
        _filter = new ResourceFilter("filter requests", ApplyFilter, () => _list);
        _list.AddCommand(ActionCommand("New", () => OpenEditor(null, false), () => _workspace is not null));
        _list.AddCommand(ActionCommand("Edit", RequestEdit, () => SelectedItem is not null,
            TuiKeybindHelpers.SecondaryPresentation("ResourceList.Activate")));
        _list.AddCommand(ActionCommand("Copy", () => OpenEditor(SelectedItem, true),
            () => SelectedItem is { IsBroken: false }));
        _list.AddCommand(ActionCommand("Delete", ShowDeleteDialog, () => SelectedItem is not null));
        foreach (Command command in ControlCommands("Request.EditJson", "Edit JSON",
                     () =>
                     {
                         if (SelectedItem is { } item) { NotifyIfFailed(EditAsJson(item)); }
                     },
                     () => SelectedItem is not null))
        {
            _list.AddCommand(command);
        }

        _list.AddCommand(SendCommand());
        _authView = new ScrollableContent(new ComputedVisual(BuildAuthentication));
        _referencesView = new ScrollableContent(new ComputedVisual(BuildReferences));
        Visual responsePane = ResourceScreenLayoutHelpers.Pane(_responsePreview.Root);
        Rule responseRule = StraumrSurfaceHelpers.TitledDivider("Response", responsePane.Owns);
        responseRule.EndLabel = new TextBlock(() => _responseSummary.Value)
            .Style(() => _responseFailed.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedBrightText)
            .Trimming(TextTrimming.EndEllipsis);
        Visual authPane = ResourceScreenLayoutHelpers.Pane(_authView);
        Visual referencesPane = ResourceScreenLayoutHelpers.Pane(_referencesView);
        Visual authColumn = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(authPane, 0, 0)
            .Cell(StraumrSurfaceHelpers.TitledDivider("Variables & Secrets", referencesPane.Owns), 1, 0)
            .Cell(referencesPane, 2, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        _sections = ResourceScreenLayoutHelpers.StackedSections(_splits,
            ResourceScreenLayoutHelpers.TwoPaneSections(_splits, "Authentication", authColumn,
                "Request", ResourceScreenLayoutHelpers.Pane(_requestPreview.Root), authPane.Owns),
            responseRule, responsePane);
        _sections.AddCommand(SendCommand(true));
        Visual listView = ResourceScreenLayoutHelpers.Scrollable(_list);
        Root = ResourceScreenLayoutHelpers.Create("Requests",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter,
            _splits,
            () => _loading.Value ? ResourceScreenLayoutHelpers.Message(new HStack(new Spinner(),
                new TextBlock("Loading requests…").Style(StraumrStyleService.MutedText)).Spacing(1)) : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayoutHelpers.EmptySections() : _sections);
        PromptCommands =
        [
            new TuiCommandModel("select", SelectRequestAsync) { Aliases = ["r"], ArgumentValues = RequestNames },
            new TuiCommandModel("create", CreateRequestAsync) { Aliases = ["new"] },
            new TuiCommandModel("edit", EditRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommandModel("copy", CopyRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommandModel("delete", DeleteRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommandModel("send", SendRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommandModel("view", ViewResponseAsync) { Aliases = ["response"], ArgumentValues = RequestNames },
            new TuiCommandModel("json", EditJsonAsync) { ArgumentValues = RequestNames },
            new TuiCommandModel("refresh", RefreshAsync)
        ];
    }
    private RequestScreenItemModel? SelectedItem => (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

    private ResponseBodyOptionsModel BodyOptions =>
        new(_settings.ResponseBodyFormat, _settings.ResponseHighlight, _settings.ResponseHighlightLimit);

    public TuiScreen Kind => TuiScreen.Requests;
    public Visual Root { get; }
    public Visual FocusTarget => _list;
    public string? ActiveWorkspaceName { get; private set; }
    public IReadOnlyList<TuiCommandModel> PromptCommands { get; }
    public event Action<TuiCommandResultModel>? NotificationRequested;
    public event Action<TuiExternalActionModel>? ExternalActionRequested;
    public event Action? TransientScreenOpened;
    public event Action? TransientScreenClosed;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Guid? selected = SelectedItem?.Id;
        Guid? previousWorkspace = _workspace?.Id;
        _loading.Value = true;
        _loadError.Value = false;
        _displayedId = null;
        _authentication.Value = null;
        try
        {
            await _state.LoadAsync(cancellationToken);
            _workspace = _state.State.CurrentWorkspace;
            ActiveWorkspaceName = null;
            _items = [];
            _workspaceAuths = [];
            if (_workspace is null)
            {
                _emptyMessage.Value = "No active workspace. Use :workspace to choose one.";
                ApplyFilter(string.Empty);
                return;
            }

            StraumrWorkspace workspace = await _workspaces.GetAsync(_workspace.Id, false, cancellationToken);
            if (workspace.Id != _workspace.Id)
            {
                throw new StraumrException("The active workspace has a mismatched ID; repair it in Workspaces.", StraumrError.CorruptEntry);
            }

            ActiveWorkspaceName = workspace.Name;
            foreach (Guid id in workspace.Requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(Path.GetDirectoryName(_workspace.Path)!, $"{id}.jsonc");
                if (!File.Exists(path))
                {
                    continue;
                }

                _items.Add(await LoadItemAsync(id, path, cancellationToken));
            }
            _items = _items.OrderByDescending(item => item.Request?.LastAccessed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _workspaceAuths = await LoadAuthsAsync(cancellationToken);
            if (previousWorkspace != _workspace.Id)
            {
                selected = null;
                _filter.Clear();
            }
            ApplyFilter(_filter.Text, selected);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            _items = [];
            _loadError.Value = true;
            _emptyMessage.Value = $"Cannot load requests: {exception.Message}\nUse :refresh to retry, or :workspace.";
            ApplyFilter(_filter.Text);
        }
        finally
        {
            _loading.Value = false;
        }
    }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        _responseView?.Update();
        _editorView?.Update();
        if (_editorNotice is { } notice)
        {
            _editorNotice = null;
            _editorView?.Report(notice, true);
        }

        await DeletePendingAsync(cancellationToken);

        if (_pendingSave is { } save)
        {
            _pendingSave = null;
            TuiCommandResultModel result = await save(cancellationToken);
            if (result.IsError)
            {
                _editorView?.Failed(result.Message!);
            }
            else if (_editorView is { } editor)
            {
                editor.Saved();
                editor.Report(result.Message!, false);
            }
            else
            {
                NotificationRequested?.Invoke(result);
            }
        }
        if (_savePaneLayout)
        {
            _savePaneLayout = false;
            try
            {
                await _state.SaveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                NotificationRequested?.Invoke(TuiCommandResultModel.Failed(
                    $"cannot save pane layout: {exception.Message}"));
            }
        }
        if (_pendingSend is { } id)
        {
            _pendingSend = null;
            await SendAsync(id, cancellationToken);
        }
        RequestScreenItemModel? item = SelectedItem;
        if (item?.Id == _displayedId)
        {
            return;
        }

        _displayedId = item?.Id;
        _authView.ScrollOffset = 0;
        _authentication.Value = null;
        if (item is null)
        {
            return;
        }

        if (item.Request is not { } request)
        {
            _requestBody.SetBody(null);
            _requestPreview.SetPageText(BodyPage,
                $"{item.Problem}\n\nPress {TuiKeybindHelpers.Hint("Request.Edit")} to repair this request.\n{item.Path}");
            _requestHeaders.SetMessage("Unavailable.");
            _requestPreview.SetPageText(ParamsPage, "Unavailable.");
            _responseBody.SetBody(null);
            _responseSummary.Value = "Unavailable";
            _responsePreview.SetPageText(BodyPage, "Repair the request before sending it.");
            _responseHeaders.SetMessage("Unavailable.");
            _sentHeaders.SetMessage("Unavailable.");
            _responsePreview.SetPageText(NetworkPage, "Unavailable.");
            return;
        }
        SetRequestPreview(request);
        ShowResponse(item.Id, request.Headers);
        _authentication.Value = RequestAuthenticationModel.Loading;
        RequestAuthenticationModel authentication = await RequestAuthenticationModel.LoadAsync(
            request, _workspace!, _auths, _secrets, _variables, cancellationToken);
        if (SelectedItem?.Id == item.Id)
        {
            _authentication.Value = authentication;
        }
    }

    private async Task<RequestScreenItemModel> LoadItemAsync(Guid id, string path, CancellationToken cancellationToken)
    {
        try
        {
            StraumrRequest request = await _requests.GetAsync(_workspace!, id, false, cancellationToken);
            string? problem = Validate(request, id);
            if (problem is null && request.LastResponse is { } stored && _workspace is { } workspace)
            {
                _responses.TryAdd((workspace.Id, id), stored.ToResponse());
            }

            return new RequestScreenItemModel(id, path, problem is null ? request : null, problem);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new RequestScreenItemModel(id, path, null, exception.Message);
        }
    }

    private async Task<IReadOnlyList<StraumrAuth>> LoadAuthsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await _auths.ListAsync(_workspace!, cancellationToken))
                .OrderBy(auth => auth.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return [];
        }
    }

    private Visual BuildHead()
    {
        if (SelectedItem is not { } item)
        {
            return new TextBlock("No request selected.").Style(StraumrStyleService.MutedText).Trimming(TextTrimming.EndEllipsis);
        }

        Visual summary = item.Request is { } request
            ? new HStack(new TextBlock(request.Method.Method).Style(HttpMethodFormatting.Style(request.Method))
                    .MinWidth(request.Method.Method.Length),
                new TextBlock(SecretFormatting.Display(RequestEditorStateModel.FromRequest(request).GetDisplayUri())).Style(StraumrStyleService.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch)).Spacing(2)
            : new TextBlock($"{SecretFormatting.Display(item.Name)} · cannot be read").Style(StraumrStyleService.RedText).Trimming(TextTrimming.EndEllipsis);
        return StraumrSurfaceHelpers.Bar(summary, new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyleService.TokenChip));
    }

    private Visual BuildAuthentication()
    {
        if (_authentication.Value is not { } auth)
        {
            return new TextBlock("Unavailable.").Style(StraumrStyleService.MutedText);
        }

        Visual fields = FieldListHelpers.Create(
            ("Source", FieldListHelpers.Wrapped(auth.Source)),
            ("Type", FieldListHelpers.Text(auth.Type)),
            ("Injects", FieldListHelpers.Wrapped(auth.Injects)),
            ("Status", FieldListHelpers.Styled(auth.Status.Text, auth.Status.Style)));
        VStack content = new VStack(fields).Spacing(1).HorizontalAlignment(Align.Stretch);
        if (auth.Problem is { } problem)
        {
            content.Add(FieldListHelpers.Problem(problem));
        }

        return content;
    }

    private Visual BuildReferences()
    {
        if (_authentication.Value is not { } auth)
        {
            return new TextBlock("Unavailable.").Style(StraumrStyleService.MutedText);
        }

        return ReferenceListHelpers.Create(auth.References);
    }

    private void SetRequestPreview(StraumrRequest request)
    {
        _requestBody.SetBody(request.BodyType == BodyType.None ? null : request.Bodies.GetValueOrDefault(request.BodyType),
            HeaderFormatting.ContentType(request.Headers) ?? RequestEditingHelpers.ContentType(request.BodyType));
        _requestHeaders.SetHeaders(HeaderFormatting.Rows(request.Headers));
        _requestPreview.SetPageText(ParamsPage, ContentFormatting.Fields(request.Params, "No parameters."));
    }

    private void ShowResponse(Guid id, IReadOnlyDictionary<string, string> configured)
    {
        _responseFailed.Value = false;
        if (_workspace is null || !_responses.TryGetValue((_workspace.Id, id), out StraumrResponse? response))
        {
            _responseBody.SetBody(null);
            _responseSummary.Value = "Not sent";
            _responsePreview.SetPageText(BodyPage,
                $"No saved response. Press {TuiKeybindHelpers.Hint("Request.Send")} to send the request.");
            _responseHeaders.SetMessage("No saved response.");
            _sentHeaders.SetMessage("No saved response.");
            _responsePreview.SetPageText(NetworkPage, "No saved response.");
            return;
        }
        _responseFailed.Value = response.Exception is not null || (int?)response.StatusCode >= 400;
        string status = response.StatusCode is { } code ? $"{(int)code} {response.ReasonPhrase}" : "Send failed";
        long bytes = response.Bytes;
        _responseSummary.Value = $"{status} · {response.Duration.TotalMilliseconds:0} ms · {ContentFormatting.Size(bytes)}";
        string details = $"{status}\nDuration: {response.Duration.TotalMilliseconds:0.##} ms\nSize: {ContentFormatting.Size(bytes)}\nHTTP: {response.HttpVersion}";
        if (response.Sent is { } sent)
        {
            details += $"\nSent: {TimestampFormatting.Relative(sent)}";
        }

        if (response.Warnings.Count > 0)
        {
            details += "\n\nWarnings\n" + string.Join('\n', response.Warnings);
        }

        if (response.Exception is { } exception)
        {
            details += "\n\n" + exception.Message;
        }

        _responsePreview.SetPageText(NetworkPage, details);
        _responseHeaders.SetHeaders(HeaderFormatting.Rows(response.ResponseHeaders));
        _sentHeaders.SetHeaders(HeaderFormatting.Sent(response.RequestHeaders, configured), HeaderFormatting.NotRecorded);
        _responseBody.SetBody(response.Content, HeaderFormatting.ContentType(response.ResponseHeaders));
        if (response.Exception is not null)
        {
            _responsePreview.SetPageText(BodyPage, response.Exception.Message);
        }
        else if (response.BodyOmitted)
        {
            _responsePreview.SetPageText(BodyPage, ContentFormatting.Unsaved(bytes));
        }
    }

    private void ApplyFilter(string query) => ApplyFilter(query, SelectedItem?.Id);

    private void ApplyFilter(string query, Guid? selected)
    {
        _query.Value = query.Trim();
        _count.Value = _items.Count;
        _visible.Value = _items.Where(item => _query.Value.Length == 0 || item.Name.Contains(_query.Value, StringComparison.OrdinalIgnoreCase) ||
                                              item.Request is { } request && (request.Uri.Contains(_query.Value, StringComparison.OrdinalIgnoreCase) ||
                                                                              request.Method.Method.Contains(_query.Value, StringComparison.OrdinalIgnoreCase))).ToList();
        _matchCount.Value = _visible.Value.Count;
        _list.SetRows(_visible.Value.Select(item => new ResourceRowModel(SecretFormatting.Display(item.Name), IsBroken: item.IsBroken,
            LeadingToken: item.Request is { } request ? new ResourceTokenModel(request.Method.Method,
                HttpMethodFormatting.Style(request.Method), request.Method == HttpMethod.Delete ? StraumrStyleService.RedBrightText : null) : null)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value && _workspace is not null)
        {
            _emptyMessage.Value = _query.Value.Length > 0 ? "No requests match this filter." : "No requests in this workspace.";
        }
    }

    private IEnumerable<string> RequestNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResultModel> SelectRequestAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
        {
            return Task.FromResult(TuiCommandResultModel.NoWorkspace);
        }

        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return Task.FromResult(TuiCommandResultModel.Failed(error!));
        }

        if (name.Length == 0)
        {
            return Task.FromResult(TuiCommandResultModel.Failed("usage: select <name>"));
        }

        return Task.FromResult(SelectRequest(name));
    }

    private TuiCommandResultModel OnSelected(string argument, Func<RequestScreenItemModel, TuiCommandResultModel> action)
    {
        if (_workspace is null)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return TuiCommandResultModel.Failed(error!);
        }

        if (name.Length > 0)
        {
            TuiCommandResultModel selection = SelectRequest(name);
            if (selection.IsError)
            {
                return selection;
            }
        }

        return SelectedItem is { } item ? action(item) : TuiCommandResultModel.Failed("no request selected");
    }

    private Task<TuiCommandResultModel> CreateRequestAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
        {
            return Task.FromResult(TuiCommandResultModel.NoWorkspace);
        }

        return Task.FromResult(argument.Length > 0
            ? TuiCommandResultModel.Failed("usage: create")
            : OpenEditorFor(null, false));
    }

    private Task<TuiCommandResultModel> EditRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? EditAsJson(item)
            : OpenEditorFor(item, false)));

    private Task<TuiCommandResultModel> CopyRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? TuiCommandResultModel.Failed($"cannot copy {item.Name}: the request cannot be read")
            : OpenEditorFor(item, true)));

    private Task<TuiCommandResultModel> DeleteRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ =>
        {
            ShowDeleteDialog();
            return TuiCommandResultModel.None;
        }));

    private Task<TuiCommandResultModel> ViewResponseAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item =>
        {
            if (_workspace is not { } workspace || !_responses.ContainsKey((workspace.Id, item.Id)))
            {
                return TuiCommandResultModel.Failed($"{item.Name} has no response yet; send it first");
            }

            OpenResponse();
            return TuiCommandResultModel.None;
        }));

    private TuiCommandResultModel OpenEditorFor(RequestScreenItemModel? source, bool copy)
    {
        if (_editorView is not null)
        {
            return TuiCommandResultModel.Failed("the request editor is already open");
        }

        OpenEditor(source, copy);
        return TuiCommandResultModel.None;
    }

    private TuiCommandResultModel SelectRequest(string name)
    {
        List<RequestScreenItemModel> matches = _items.FindAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
        {
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }

        if (matches.Count != 1)
        {
            return TuiCommandResultModel.Failed(matches.Count == 0 ? $"no request matches {name}" :
                $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        }

        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResultModel.None;
    }

    private Task<TuiCommandResultModel> EditJsonAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, EditAsJson));

    private async Task<TuiCommandResultModel> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return TuiCommandResultModel.Failed("usage: refresh");
        }

        if (_workspace is null)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        await LoadAsync(cancellationToken);
        return _loadError.Value ? TuiCommandResultModel.Failed(_emptyMessage.Value) :
            TuiCommandResultModel.Ok($"reloaded {CountFormatting.Label(_items.Count, "request")}");
    }

    private Task<TuiCommandResultModel> SendRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item =>
        {
            if (item.Request is not { } request)
            {
                return TuiCommandResultModel.Failed($"cannot send {item.Name}: the request cannot be read");
            }

            if (_sendCancellation is not null)
            {
                return TuiCommandResultModel.Failed("a request is already being sent");
            }

            _responseView = BuildResponseView(item.Id, request, () => _list.App?.Focus(_list));
            QueueSend(item.Id);
            ShowResponseView();
            return TuiCommandResultModel.None;
        }));

    private void QueueSend()
    {
        if (SelectedItem is not { IsBroken: false } item || _sendCancellation is not null)
        {
            return;
        }

        _responseView = BuildResponseView(item.Id, item.Request!, () => _list.App?.Focus(_list));
        QueueSend(item.Id);
        ShowResponseView();
    }

    private void OpenResponse()
    {
        if (_workspace is not { } workspace || SelectedItem is not { Request: { } request } item ||
            !_responses.TryGetValue((workspace.Id, item.Id), out StraumrResponse? response))
        {
            return;
        }

        _responseView = BuildResponseView(item.Id, request,
            () => _responsePreview.FocusTarget.App?.Focus(_responsePreview.FocusTarget));
        _responseView.Complete(response, true);
        ShowResponseView();
    }

    private void ShowResponseView()
    {
        _responseView!.Show();
        TransientScreenOpened?.Invoke();
    }

    private RequestResponseView BuildResponseView(Guid id, StraumrRequest request, Action restoreFocus) =>
        new(request, ActiveWorkspaceName, () => BodyOptions, () => _sendCancellation?.Cancel(), () =>
        {
            if (_sendCancellation is not null)
            {
                return;
            }

            QueueSend(id);
            _responseView?.Restart();
        }, () =>
        {
            _responseView = null;
            restoreFocus();
            TransientScreenClosed?.Invoke();
        });

    private void QueueSend(Guid id)
    {
        _sendCancellation = new CancellationTokenSource();
        _pendingSend = id;
    }

    private async Task SendAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace || _sendCancellation is null)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sendCancellation.Token);
        try
        {
            StraumrRequest request = await _requests.GetAsync(workspace, id, false, linked.Token);
            if (Validate(request, id) is { } problem)
            {
                throw new StraumrException(problem, StraumrError.CorruptEntry);
            }

            StraumrResponse response = await _requests.SendAsync(workspace, request, cancellationToken: linked.Token);
            _responses[(workspace.Id, id)] = response;
            _responseView?.Complete(response);
            _displayedId = null;
            await StoreResponseAsync(workspace, id, response, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && _sendCancellation.IsCancellationRequested)
        {
            _responseView?.Fail("Request cancelled");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _responseView?.Fail("Request timed out");
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is HttpRequestException or UriFormatException or InvalidOperationException)
        {
            _responseView?.Fail("Send failed", exception.Message);
        }
        finally
        {
            _sendCancellation.Dispose();
            _sendCancellation = null;
        }
    }

    private async Task StoreResponseAsync(StraumrWorkspaceEntry workspace, Guid id, StraumrResponse response,
        CancellationToken cancellationToken)
    {
        int limit = _settings.ResponseStoreLimit;
        try
        {
            await _requests.StoreResponseAsync(workspace, id,
                limit <= 0 ? null : response.ToStored(limit), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            NotificationRequested?.Invoke(TuiCommandResultModel.Failed(
                $"cannot save the response: {exception.Message}"));
        }
    }

    private void OpenEditor(RequestScreenItemModel? source, bool copy)
    {
        if (_workspace is null || _editorView is not null)
        {
            return;
        }

        bool isNew = source is null || copy;
        RequestEditorStateModel state = source?.Request is { } request
            ? RequestEditorStateModel.FromRequest(request)
            : RequestEditorStateModel.CreateNew();
        if (isNew && source is not null)
        {
            state.Name = string.Empty;
        }

        _editingId = isNew ? null : source!.Id;
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new RequestEditor(state, _workspaceAuths, ActiveWorkspaceName, isNew,
            () => _pendingSave = token => SaveEditAsync(state, token),
            () =>
            {
                _editorView = null;
                _editingId = null;
                _list.App?.Focus(_list);
                TransientScreenClosed?.Invoke();
            },
            EditContentExternally,
            sourceName);
        _editorView = editor;
        editor.Show();
        TransientScreenOpened?.Invoke();
    }

    private void ShowDeleteDialog()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        new ConfirmDialog(
            "Delete request",
            $"Delete {SecretFormatting.Display(item.Name)}?",
            "The request will be permanently deleted.",
            "Delete",
            true,
            () => _pendingDeleteId = item.Id).Show();
    }

    private async Task DeletePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } id || _workspace is not { } workspace)
        {
            return;
        }

        _pendingDeleteId = null;
        if (_items.Find(candidate => candidate.Id == id) is not { } item)
        {
            return;
        }

        int index = _selectedIndex.Value;
        try
        {
            await _requests.DeleteAsync(workspace, id, cancellationToken);
            _responses.Remove((workspace.Id, id));
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, null);
            if (_visible.Value.Count > 0)
            {
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            }

            NotificationRequested?.Invoke(TuiCommandResultModel.Ok($"deleted request {item.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            NotificationRequested?.Invoke(TuiCommandResultModel.Failed($"delete failed: {exception.Message}"));
        }
    }

    private void EditContentExternally(ExternalContentEditModel edit)
    {
        if (_editorView is not { } view)
        {
            return;
        }

        if (!_editor.IsConfigured)
        {
            view.Report("no default editor is configured; set EDITOR to write a body", true);
            return;
        }

        view.Suspend();
        ExternalActionRequested?.Invoke(new TuiExternalActionModel(async token =>
        {
            try
            {
                edit.Apply(await _editor.EditAsync(edit.Document, edit.Extension, token));
            }
            catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
            {
                _editorNotice = $"edit failed: {exception.Message}";
            }

            return TuiCommandResultModel.None;
        }, _list));
    }

    private async Task<TuiCommandResultModel> SaveEditAsync(
        RequestEditorStateModel state, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.Failed("no active workspace");
        }

        try
        {
            StraumrRequest saved;
            bool created = _editingId is null;
            if (_editingId is { } id)
            {
                StraumrRequest existing = await _requests.GetAsync(workspace, id, false, cancellationToken);
                state.ApplyTo(existing);
                saved = await _requests.SaveAsync(workspace, existing, cancellationToken);
                _responses.Remove((workspace.Id, id));
            }
            else
            {
                saved = await _requests.CreateAsync(workspace, state.ToRequest(), cancellationToken);
                _editingId = saved.Id;
            }

            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, saved.Id);
            return TuiCommandResultModel.Ok(created
                ? $"created request {saved.Name}"
                : $"updated request {saved.Name}");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResultModel.Failed(exception.Message);
        }
    }

    private void RequestEdit()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        if (!item.IsBroken)
        {
            OpenEditor(item, false);
            return;
        }

        NotifyIfFailed(EditAsJson(item));
    }

    private TuiCommandResultModel EditAsJson(RequestScreenItemModel item)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        if (!_editor.IsConfigured)
        {
            return TuiCommandResultModel.Failed("edit failed: no default editor is configured");
        }

        ExternalActionRequested?.Invoke(new TuiExternalActionModel(token => EditAsync(workspace, item, token), _list));
        return TuiCommandResultModel.None;
    }

    private void Notify(string message, bool failed) => NotificationRequested?.Invoke(
        failed ? TuiCommandResultModel.Failed(message) : TuiCommandResultModel.Ok(message));

    private void NotifyIfFailed(TuiCommandResultModel result)
    {
        if (result.IsError)
        {
            NotificationRequested?.Invoke(result);
        }
    }

    private async Task<TuiCommandResultModel> EditAsync(StraumrWorkspaceEntry workspace, RequestScreenItemModel item, CancellationToken cancellationToken)
    {
        try
        {
            string edited = await _editor.EditJsonAsync(await File.ReadAllTextAsync(item.Path, cancellationToken), cancellationToken);
            StraumrRequest? request = null;
            try { request = JsonSerializer.Deserialize(edited, StraumrJsonContext.Default.StraumrRequest); }
            catch (JsonException) { }
            string? problem = Validate(request, item.Id);
            if (problem is not null)
            {
                await File.WriteAllTextAsync(item.Path, edited, cancellationToken);
            }
            else
            {
                _files.CarryCommentsFrom(item.Path, edited);
                await _requests.SaveAsync(workspace, request!, cancellationToken);
            }
            _responses.Remove((workspace.Id, item.Id));
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, item.Id);
            return problem is null ? TuiCommandResultModel.Ok($"updated request {request!.Name}") :
                TuiCommandResultModel.Failed($"saved the edit, but {problem}; press {TuiKeybindHelpers.Hint("Request.Edit")} to repair it");
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
        {
            return TuiCommandResultModel.Failed($"edit failed: {exception.Message}");
        }
    }

    private static string? Validate(StraumrRequest? request, Guid id) => request switch
    {
        null => "the file is not a valid request",
        not null when request.Id != id => "the request ID does not match its file",
        not null when string.IsNullOrWhiteSpace(request.Name) => "the request name is missing",
        not null when request.Name.Contains('"') => "the request name contains a double quote",
        not null when string.IsNullOrWhiteSpace(request.Uri) => "the request URL is missing",
        { Method: null } => "the HTTP method is missing",
        { Params: null } or { Headers: null } or { Bodies: null } => "a request collection is null",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    private static IEnumerable<Command> ControlCommands(
        string id, string label, Action execute, Func<bool> available)
    {
        yield return ControlCommand(id, label,
            CommandPresentation.CommandBar, execute, available);
        yield return ControlCommand($"{id}.Letter", label,
            CommandPresentation.None, execute, available);
    }

    private static Command ControlCommand(string id, string label,
        CommandPresentation presentation, Action execute, Func<bool> available) => new()
    {
        Id = id,
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Secondary,
        Presentation = presentation,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => execute()
    };

    private Command SendCommand(bool overPanes = false) =>
        ActionCommand("Send", QueueSend, () => SelectedItem is { IsBroken: false } && !(overPanes && Root.IsTyping()),
            consumes: !overPanes);

    private static Command ActionCommand(string label, Action execute, Func<bool> available,
        CommandPresentation presentation = CommandPresentation.CommandBar, bool consumes = true) => new()
    {
        Id = $"Request.{label}",
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get($"Request.{label}"),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = consumes,
        Execute = _ => TuiKeybindHelpers.Run($"Request.{label}", execute)
    };

    private void QueuePaneLayoutSave()
    {
        _state.State.PaneLayouts[PaneLayoutKey] = new StraumrPaneLayout
        {
            Panels = _splits.Panels.Share,
            Sections = _splits.Sections.Share,
            Stack = _splits.Stack.Share
        };
        _savePaneLayout = true;
    }
}
