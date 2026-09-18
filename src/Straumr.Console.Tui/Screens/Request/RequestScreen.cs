using System.Text;
using System.Text.Json;
using Straumr.Console.Shared.Models;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Shared.Editor;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Request;

public sealed class RequestScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Requests);

    private readonly IStraumrStateService _state;
    private readonly IStraumrWorkspaceService _workspaces;
    private readonly IStraumrRequestService _requests;
    private readonly IStraumrAuthService _auths;
    private readonly IStraumrSecretService _secrets;
    private readonly IStraumrFileService _files;
    private readonly IStraumrSettingsService _settings;
    private readonly ExternalEditor _editor;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<int> _count = new(0);
    private readonly State<int> _matchCount = new(0);
    private readonly State<string> _query = new(string.Empty);
    private readonly State<string> _emptyMessage = new("Loading requests…");
    private readonly State<bool> _loading = new(true);
    private readonly State<bool> _loadError = new(false);
    private readonly State<RequestAuthentication?> _authentication = new(null);
    private readonly State<string> _responseSummary = new("Not sent");
    private readonly State<bool> _responseFailed = new(false);
    private readonly ResourceList _list;
    private readonly ResourceFilter _filter;
    private readonly PaneSplits _splits;
    private readonly PreviewPane _requestPreview = new("Body", "Headers", "Params");
    private readonly PreviewPane _responsePreview = new("Body", "Headers", "Network");
    private readonly ResponseBodyActions _responseBody;
    private readonly ScrollableContent _authView;
    private readonly ScrollableContent _secretsView;
    private readonly Visual _sections;
    private readonly Dictionary<(Guid Workspace, Guid Request), StraumrResponse> _responses = [];
    private List<RequestScreenItem> _items = [];
    private readonly State<List<RequestScreenItem>> _visible = new([]);
    private StraumrWorkspaceEntry? _workspace;
    private Guid? _displayedId;
    private Guid? _pendingSend;

    /// <summary>The request the reader has confirmed deleting, removed on the next update pass.</summary>
    private Guid? _pendingDeleteId;
    private CancellationTokenSource? _sendCancellation;
    private RequestResponseView? _responseView;
    private RequestEditor? _editorView;

    /// <summary>
    /// The request the open editor writes to, or <see langword="null"/> while it would create one.
    /// It is a field rather than a captured value because the editor stays open after a save: what
    /// a create wrote is what the next save has to change.
    /// </summary>
    private Guid? _editingId;

    /// <summary>
    /// What an external body edit has to say, held until the editor screen is back on the terminal
    /// to say it on. It is set off the update loop, while the app is not running at all.
    /// </summary>
    private string? _editorNotice;

    /// <summary>
    /// The save an open editor has asked for, run on the next update pass. Core calls belong there,
    /// where every other one in this screen runs, rather than inside the keystroke that asked.
    /// </summary>
    private Func<CancellationToken, Task<TuiCommandResult>>? _pendingSave;

    /// <summary>
    /// The workspace's auths, for the editor's Auth field. Loaded with the requests rather than when
    /// the editor opens, because opening a form is not a moment to spend on disk.
    /// </summary>
    private IReadOnlyList<StraumrAuth> _workspaceAuths = [];
    private bool _savePaneLayout;

    public RequestScreen(IStraumrStateService state, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, IStraumrSecretService secrets,
        IStraumrFileService files, IStraumrSettingsService settings, ExternalEditor editor)
    {
        (_state, _workspaces, _requests, _auths, _secrets, _files, _settings, _editor) =
            (state, workspaces, requests, auths, secrets, files, settings, editor);
        _responseBody = new ResponseBodyActions(_responsePreview, (message, failed) =>
            NotificationRequested?.Invoke(failed ? TuiCommandResult.Failed(message) : TuiCommandResult.Ok(message)),
            () => _settings.ResponseBodyFormat);
        _responsePreview.Root.AddCommand(ActionCommand("Fullscreen", 'v', OpenResponse,
            () => _workspace is { } workspace && SelectedItem is { IsBroken: false } item && _responses.ContainsKey((workspace.Id, item.Id))));
        StraumrPaneLayout paneLayout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey)
            ?? new StraumrPaneLayout();
        _splits = new PaneSplits(paneLayout.Panels, paneLayout.Sections, paneLayout.Stack);
        _splits.Changed += QueuePaneLayoutSave;
        _list = new ResourceList([], ResourceScreenLayout.Message(
            new TextBlock(() => _emptyMessage.Value)
                .Style(() => _loadError.Value ? StraumrStyles.RedText : StraumrStyles.MutedText)
                .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            // One hint for one action under two keys. The bar renders one keycap per hint, from
            // the gesture, so `e` rides in the label painted in the bar's own key colour — the
            // same way `Tab /t Next tab` carries the letter that also changes page.
            activateLabel: $"{StraumrStyles.KeyMarkup("e")} Edit");
        _list.BindSelectedIndex(_selectedIndex);
        // Enter opens the editor, exactly as `e` does. Activating a row means doing the thing the
        // row is for, and moving focus into a read-only preview is not that: `Tab` already reaches
        // the panes, and the preview is beside the list rather than behind it, so there was nothing
        // to open. Broken requests fall through to the JSON the same way, since it is one method.
        _list.ItemActivated += _ => RequestEdit();
        _filter = new ResourceFilter("filter requests", ApplyFilter, () => _list);
        _list.AddCommand(ActionCommand("New", 'c', () => OpenEditor(null, 'c'), () => _workspace is not null));
        // `e` is one key with two meanings because it is one intent. A request that parses is edited
        // in the form; one that does not cannot be loaded into fields at all, so the same key opens
        // the text that needs repairing, which is what the broken row already tells the reader to do.
        // Unpresented: `Enter`'s hint names this key, and the row has no space for the same action
        // twice.
        _list.AddCommand(ActionCommand("Edit", 'e', RequestEdit, () => SelectedItem is not null,
            CommandPresentation.None));
        _list.AddCommand(ActionCommand("Copy", 'y', () => OpenEditor(SelectedItem, 'y'),
            () => SelectedItem is { IsBroken: false }));
        // Offered for a broken request too, unlike Copy and Send: one that cannot be read is one a
        // reader is more likely to want rid of, not less.
        _list.AddCommand(ActionCommand("Delete", 'd', ShowDeleteDialog, () => SelectedItem is not null));
        foreach (Command command in ControlCommands("Request.EditJson", "Edit JSON", 'e',
                     () => { if (SelectedItem is { } item) NotifyIfFailed(EditAsJson(item)); },
                     () => SelectedItem is not null))
            _list.AddCommand(command);
        _list.AddCommand(SendCommand());
        _authView = new ScrollableContent(new ComputedVisual(BuildAuthentication));
        _secretsView = new ScrollableContent(new ComputedVisual(BuildSecrets));
        Visual responsePane = ResourceScreenLayout.Pane(_responsePreview.Root);
        var responseRule = StraumrSurfaces.TitledDivider("Response", responsePane.Owns);
        responseRule.EndLabel = new TextBlock(() => _responseSummary.Value)
            .Style(() => _responseFailed.Value ? StraumrStyles.RedText : StraumrStyles.MutedBrightText)
            .Trimming(TextTrimming.EndEllipsis);
        // Secrets is a region of its own rather than a paragraph under Authentication: what it
        // answers — whether this request can fill itself in when it is sent — is a different
        // question from how the request authenticates, and it is worth seeing without asking for
        // it. The column's two regions share its height; each scrolls when its own content is
        // longer than its share.
        Visual authPane = ResourceScreenLayout.Pane(_authView);
        Visual secretsPane = ResourceScreenLayout.Pane(_secretsView);
        Visual authColumn = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(authPane, 0, 0)
            .Cell(StraumrSurfaces.TitledDivider("Secrets", secretsPane.Owns), 1, 0)
            .Cell(secretsPane, 2, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        _sections = ResourceScreenLayout.StackedSections(_splits,
            ResourceScreenLayout.TwoPaneSections(_splits, "Authentication", authColumn,
                "Request", ResourceScreenLayout.Pane(_requestPreview.Root), authPane.Owns),
            responseRule, responsePane);
        // Sending is the screen's action, not the list's: a reader looking at the response of the
        // last send wants the next one from where they are, not after a trip back to the row. The
        // command goes on the container the detail regions share rather than on each of them, since
        // routing walks the focus chain, and not on `Root`, which the filter's text field is under.
        _sections.AddCommand(SendCommand());
        Visual listView = ResourceScreenLayout.Scrollable(_list);
        Root = ResourceScreenLayout.Create("Requests",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter,
            _splits,
            () => _loading.Value ? ResourceScreenLayout.Message(new HStack(new Spinner(),
                new TextBlock("Loading requests…").Style(StraumrStyles.MutedText)).Spacing(1)) : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayout.EmptySections() : _sections);
        // Every action the screen offers under a key is here under a name as well, because the
        // prompt is how the other screens reach this one: `:rq edit <request>` from Auths is the
        // same edit `e` is on the row. Each takes the request to act on by name, and acts on the
        // selection when given none, which is what the key does.
        PromptCommands =
        [
            new TuiCommand("select", SelectRequestAsync) { Aliases = ["r"], ArgumentValues = RequestNames },
            new TuiCommand("create", CreateRequestAsync) { Aliases = ["new"] },
            new TuiCommand("edit", EditRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommand("copy", CopyRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommand("delete", DeleteRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommand("send", SendRequestAsync) { ArgumentValues = RequestNames },
            new TuiCommand("view", ViewResponseAsync) { Aliases = ["response"], ArgumentValues = RequestNames },
            new TuiCommand("json", EditJsonAsync) { ArgumentValues = RequestNames },
            new TuiCommand("refresh", RefreshAsync)
        ];
    }

    public TuiScreen Kind => TuiScreen.Requests;
    public Visual Root { get; }
    public Visual FocusTarget => _list;
    public string? ActiveWorkspaceName { get; private set; }
    public IReadOnlyList<TuiCommand> PromptCommands { get; }
    public event Action<TuiCommandResult>? NotificationRequested;
    public event Action<TuiExternalAction>? ExternalActionRequested;
    public event Action? TransientScreenOpened;
    public event Action? TransientScreenClosed;
    private RequestScreenItem? SelectedItem => (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

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

            StraumrWorkspace workspace = await _workspaces.GetAsync(_workspace.Id, updateLastAccessed: false, cancellationToken);
            if (workspace.Id != _workspace.Id)
                throw new StraumrException("The active workspace has a mismatched ID; repair it in Workspaces.", StraumrError.CorruptEntry);
            ActiveWorkspaceName = workspace.Name;
            foreach (Guid id in workspace.Requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(Path.GetDirectoryName(_workspace.Path)!, $"{id}.jsonc");
                if (!File.Exists(path))
                    continue;
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

    private async Task<RequestScreenItem> LoadItemAsync(Guid id, string path, CancellationToken cancellationToken)
    {
        try
        {
            StraumrRequest request = await _requests.GetAsync(_workspace!, id, updateLastAccessed: false, cancellationToken);
            string? problem = Validate(request, id);
            return new RequestScreenItem(id, path, problem is null ? request : null, problem);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new RequestScreenItem(id, path, null, exception.Message);
        }
    }

    /// <remarks>
    /// An unreadable auth file scopes its failure to the Auth field rather than to the screen: the
    /// requests themselves are readable, and a workspace with one broken auth is still one whose
    /// requests can be edited. The field then offers what could be read.
    /// </remarks>
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

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        _responseView?.Update();
        _editorView?.Update();
        // After the editor screen is back, not before: a message put on a view that is still
        // suspended would be said to a screen nobody is looking at, and expire unread.
        if (_editorNotice is { } notice)
        {
            _editorNotice = null;
            _editorView?.Report(notice, error: true);
        }

        await DeletePendingAsync(cancellationToken);

        if (_pendingSave is { } save)
        {
            _pendingSave = null;
            TuiCommandResult result = await save(cancellationToken);
            if (result.IsError)
            {
                _editorView?.Failed(result.Message!);
            }
            else if (_editorView is { } editor)
            {
                // Said on the editor's own footer, not the shell's: saving no longer closes the
                // view, so a message behind it would expire without being read.
                editor.Saved();
                editor.Report(result.Message!, error: false);
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
                NotificationRequested?.Invoke(TuiCommandResult.Failed(
                    $"cannot save pane layout: {exception.Message}"));
            }
        }
        if (_pendingSend is { } id)
        {
            _pendingSend = null;
            await SendAsync(id, cancellationToken);
        }
        RequestScreenItem? item = SelectedItem;
        if (item?.Id == _displayedId)
            return;
        _displayedId = item?.Id;
        _authView.ScrollOffset = 0;
        _authentication.Value = null;
        if (item is null)
            return;
        if (item.Request is not { } request)
        {
            _responseBody.SetBody(null);
            _requestPreview.SetText($"{item.Problem}\n\nPress e to repair this request.\n{item.Path}", "Unavailable.", "Unavailable.");
            _responseSummary.Value = "Unavailable";
            _responsePreview.SetText("Repair the request before sending it.", "Unavailable.", "Unavailable.");
            return;
        }
        SetRequestPreview(request);
        ShowResponse(item.Id);
        _authentication.Value = RequestAuthentication.Loading;
        RequestAuthentication authentication = await RequestAuthentication.LoadAsync(
            request, _workspace!, _auths, _secrets, cancellationToken);
        if (SelectedItem?.Id == item.Id)
            _authentication.Value = authentication;
    }

    private Visual BuildHead()
    {
        if (SelectedItem is not { } item)
            return new TextBlock("No request selected.").Style(StraumrStyles.MutedText).Trimming(TextTrimming.EndEllipsis);
        Visual summary = item.Request is { } request
            ? new HStack(new TextBlock(request.Method.Method).Style(HttpMethodFormatting.Style(request.Method))
                    .MinWidth(request.Method.Method.Length),
                new TextBlock(SecretFormatting.Display(RequestEditorState.FromRequest(request).GetDisplayUri())).Style(StraumrStyles.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch)).Spacing(2)
            : new TextBlock($"{SecretFormatting.Display(item.Name)} · cannot be read").Style(StraumrStyles.RedText).Trimming(TextTrimming.EndEllipsis);
        return StraumrSurfaces.Bar(summary, new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyles.TokenChip));
    }

    private Visual BuildAuthentication()
    {
        if (_authentication.Value is not { } auth)
            return new TextBlock("Unavailable.").Style(StraumrStyles.MutedText);
        var fields = FieldList.Create(
            ("Source", FieldList.Wrapped(auth.Source)),
            ("Type", FieldList.Text(auth.Type)),
            ("Injects", FieldList.Wrapped(auth.Injects)),
            ("Status", FieldList.Styled(auth.Status.Text, auth.Status.Style)));
        // No line saying the credential is hidden. That a request's token is not printed on screen
        // is what anyone would assume, so the line answered a question nobody asked while taking a
        // row of a pane that has better uses for it.
        var content = new VStack(fields).Spacing(1).HorizontalAlignment(Align.Stretch);
        if (auth.Problem is { } problem)
            content.Add(FieldList.Problem(problem));
        return content;
    }

    /// <summary>
    /// Every secret this request depends on, its own and its auth's, and whether the store can
    /// supply it. One that cannot reads red: it is the reason a send will fail, before it does.
    /// </summary>
    private Visual BuildSecrets()
    {
        if (_authentication.Value is not { } auth)
            return new TextBlock("Unavailable.").Style(StraumrStyles.MutedText);
        return SecretList.Create(auth.References);
    }

    private void SetRequestPreview(StraumrRequest request)
    {
        string body = request.BodyType == BodyType.None ? "No body." :
            ContentFormatting.Preview(request.Bodies.GetValueOrDefault(request.BodyType), request.BodyType == BodyType.Json);
        _requestPreview.SetText(body,
            ContentFormatting.Fields(request.Headers.Select(header => new KeyValuePair<string, string>(header.Key,
                header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? "[hidden]" : header.Value)), "No headers."),
            ContentFormatting.Fields(request.Params, "No parameters."));
    }

    private void ShowResponse(Guid id)
    {
        _responseFailed.Value = false;
        if (_workspace is null || !_responses.TryGetValue((_workspace.Id, id), out StraumrResponse? response))
        {
            _responseBody.SetBody(null);
            _responseSummary.Value = "Not sent";
            // "No saved response" rather than "no response yet": the pane is showing what this
            // request has stored, and `s` is now offered from the pane itself, so the old line's
            // instruction to go back to the row was also wrong.
            _responsePreview.SetText("No saved response. Press s to send the request.",
                "No saved response.", "No saved response.");
            return;
        }
        _responseFailed.Value = response.Exception is not null || (int?)response.StatusCode >= 400;
        string status = response.StatusCode is { } code ? $"{(int)code} {response.ReasonPhrase}" : "Send failed";
        long bytes = response.RawContent?.LongLength ?? Encoding.UTF8.GetByteCount(response.Content ?? string.Empty);
        _responseSummary.Value = $"{status} · {response.Duration.TotalMilliseconds:0} ms · {ContentFormatting.Size(bytes)}";
        string details = $"{status}\nDuration: {response.Duration.TotalMilliseconds:0.##} ms\nSize: {ContentFormatting.Size(bytes)}\nHTTP: {response.HttpVersion}";
        if (response.Warnings.Count > 0)
            details += "\n\nWarnings\n" + string.Join('\n', response.Warnings);
        if (response.Exception is { } exception)
            details += "\n\n" + exception.Message;
        _responsePreview.SetText(response.Exception?.Message ?? "No body.",
            ContentFormatting.Headers(response.ResponseHeaders), details);
        _responseBody.SetBody(response.Content);
        if (response.Exception is not null)
            _responsePreview.SetPageText(0, response.Exception.Message);
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
        _list.SetRows(_visible.Value.Select(item => new ResourceRow(SecretFormatting.Display(item.Name), IsBroken: item.IsBroken,
            LeadingToken: item.Request is { } request ? new ResourceToken(request.Method.Method,
                HttpMethodFormatting.Style(request.Method), request.Method == HttpMethod.Delete ? StraumrStyles.RedBrightText : null) : null)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value && _workspace is not null)
            _emptyMessage.Value = _query.Value.Length > 0 ? "No requests match this filter." : "No requests in this workspace.";
    }

    private IEnumerable<string> RequestNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResult> SelectRequestAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
            return Task.FromResult(TuiCommandResult.NoWorkspace);
        if (!TuiCommandArguments.TryParseSingle(argument, out string name, out string? error))
            return Task.FromResult(TuiCommandResult.Failed(error!));
        if (name.Length == 0)
            return Task.FromResult(TuiCommandResult.Failed("usage: select <name>"));
        return Task.FromResult(SelectRequest(name));
    }

    /// <summary>
    /// Runs a command against the request it names, or against the selection when it names none,
    /// having first said why it cannot run at all. Every command that acts on one request goes
    /// through here, so they take their argument, report a missing workspace and report a name that
    /// matches nothing or too much in the same words.
    /// </summary>
    private TuiCommandResult OnSelected(string argument, Func<RequestScreenItem, TuiCommandResult> action)
    {
        if (_workspace is null)
            return TuiCommandResult.NoWorkspace;
        if (!TuiCommandArguments.TryParseSingle(argument, out string name, out string? error))
            return TuiCommandResult.Failed(error!);
        if (name.Length > 0)
        {
            TuiCommandResult selection = SelectRequest(name);
            if (selection.IsError)
                return selection;
        }

        return SelectedItem is { } item ? action(item) : TuiCommandResult.Failed("no request selected");
    }

    private Task<TuiCommandResult> CreateRequestAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
            return Task.FromResult(TuiCommandResult.NoWorkspace);
        return Task.FromResult(argument.Length > 0
            ? TuiCommandResult.Failed("usage: create")
            : OpenEditorFor(null, 'c'));
    }

    private Task<TuiCommandResult> EditRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? EditAsJson(item)
            : OpenEditorFor(item, 'e')));

    private Task<TuiCommandResult> CopyRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? TuiCommandResult.Failed($"cannot copy {item.Name}: the request cannot be read")
            : OpenEditorFor(item, 'y')));

    private Task<TuiCommandResult> DeleteRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ =>
        {
            ShowDeleteDialog();
            return TuiCommandResult.None;
        }));

    /// <summary>
    /// Opens the response already held for a request, full screen — what <c>v</c> does on the
    /// response pane. A request that has not been sent in this session has none to open; the
    /// command says so rather than sending one, because sending is <c>:send</c>'s to do.
    /// </summary>
    private Task<TuiCommandResult> ViewResponseAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item =>
        {
            if (_workspace is not { } workspace || !_responses.ContainsKey((workspace.Id, item.Id)))
                return TuiCommandResult.Failed($"{item.Name} has no response yet; send it first");
            OpenResponse();
            return TuiCommandResult.None;
        }));

    /// <remarks>
    /// The guard the keystroke can leave unsaid: a key pressed while the editor is up never reaches
    /// the screen behind it, but a command dispatched from another screen arrives without that.
    /// </remarks>
    private TuiCommandResult OpenEditorFor(RequestScreenItem? source, char openingGesture)
    {
        if (_editorView is not null)
            return TuiCommandResult.Failed("the request editor is already open");
        OpenEditor(source, openingGesture);
        return TuiCommandResult.None;
    }

    private TuiCommandResult SelectRequest(string name)
    {
        List<RequestScreenItem> matches = _items.FindAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count != 1)
            return TuiCommandResult.Failed(matches.Count == 0 ? $"no request matches {name}" :
                $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResult.None;
    }

    /// <summary>
    /// Opens a request's file in the configured editor. The form is how a request is normally
    /// changed; this is the way to the JSON itself, for a field the form does not offer or an edit
    /// easier to make as text. Named for what it gives you rather than for the program it runs, since
    /// which program that is comes from the environment.
    /// </summary>
    private Task<TuiCommandResult> EditJsonAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, EditAsJson));

    private async Task<TuiCommandResult> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
            return TuiCommandResult.Failed("usage: refresh");
        if (_workspace is null)
            return TuiCommandResult.NoWorkspace;
        await LoadAsync(cancellationToken);
        return _loadError.Value ? TuiCommandResult.Failed(_emptyMessage.Value) :
            TuiCommandResult.Ok($"reloaded {CountFormatting.Label(_items.Count, "request")}");
    }

    private Task<TuiCommandResult> SendRequestAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item =>
        {
            if (item.Request is not { } request)
                return TuiCommandResult.Failed($"cannot send {item.Name}: the request cannot be read");
            if (_sendCancellation is not null)
                return TuiCommandResult.Failed("a request is already being sent");

            _responseView = BuildResponseView(item.Id, request, () => _list.App?.Focus(_list));
            QueueSend(item.Id);
            ShowResponseView();
            return TuiCommandResult.None;
        }));

    private void QueueSend()
    {
        if (SelectedItem is not { IsBroken: false } item || _sendCancellation is not null)
            return;
        _responseView = BuildResponseView(item.Id, item.Request!, () => _list.App?.Focus(_list));
        QueueSend(item.Id);
        ShowResponseView();
    }

    private void OpenResponse()
    {
        if (_workspace is not { } workspace || SelectedItem is not { Request: { } request } item ||
            !_responses.TryGetValue((workspace.Id, item.Id), out StraumrResponse? response))
            return;
        _responseView = BuildResponseView(item.Id, request,
            () => _responsePreview.FocusTarget.App?.Focus(_responsePreview.FocusTarget));
        _responseView.Complete(response, cached: true);
        ShowResponseView();
    }

    /// <summary>
    /// Puts the response on the terminal and tells the shell it is there, so a command that came
    /// from another screen is returned to that screen when the view closes.
    /// </summary>
    private void ShowResponseView()
    {
        _responseView!.Show();
        TransientScreenOpened?.Invoke();
    }

    /// <remarks>
    /// Sending again is the same send, so it goes through the same queue rather than around it: the
    /// view returns to its in-flight state and <see cref="UpdateAsync"/> runs the request where every
    /// other Core call runs. It stands down while one is already in flight, as `s` on the list does.
    /// </remarks>
    private RequestResponseView BuildResponseView(Guid id, StraumrRequest request, Action restoreFocus) =>
        new(request, ActiveWorkspaceName, () => _settings.ResponseBodyFormat, () => _sendCancellation?.Cancel(), () =>
        {
            if (_sendCancellation is not null)
                return;
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
            return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sendCancellation.Token);
        try
        {
            StraumrRequest request = await _requests.GetAsync(workspace, id, updateLastAccessed: false, linked.Token);
            if (Validate(request, id) is { } problem)
                throw new StraumrException(problem, StraumrError.CorruptEntry);
            StraumrResponse response = await _requests.SendAsync(workspace, request, cancellationToken: linked.Token);
            _responses[(workspace.Id, id)] = response;
            _responseView?.Complete(response);
            _displayedId = null;
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

    /// <summary>
    /// Opens the request editor: on nothing for a new request, on the selection for a change or a
    /// copy. A copy opens with its source's name cleared rather than pre-filled with a variation of
    /// it, because the one thing a copy must be given is a name of its own.
    /// </summary>
    private void OpenEditor(RequestScreenItem? source, char openingGesture)
    {
        if (_workspace is null || _editorView is not null)
            return;

        bool isNew = source is null || openingGesture == 'y';
        RequestEditorState state = source?.Request is { } request
            ? RequestEditorState.FromRequest(request)
            : RequestEditorState.CreateNew();
        if (isNew && source is not null)
            state.Name = string.Empty;

        _editingId = isNew ? null : source!.Id;
        // A copy is the one opening that clears the name it was given, so it is the one that has to
        // keep saying which name that was.
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new RequestEditor(state, _workspaceAuths, ActiveWorkspaceName, isNew, openingGesture,
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
            return;

        new ConfirmDialog(
            "Delete request",
            $"Delete {SecretFormatting.Display(item.Name)}?",
            "The request will be permanently deleted.",
            "Delete",
            destructive: true,
            () => _pendingDeleteId = item.Id).Show();
    }

    /// <remarks>
    /// Run from the update pass, where every other Core call on this screen runs, rather than from
    /// inside the keystroke that confirmed it.
    /// </remarks>
    private async Task DeletePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } id || _workspace is not { } workspace)
            return;

        _pendingDeleteId = null;
        if (_items.Find(candidate => candidate.Id == id) is not { } item)
            return;

        int index = _selectedIndex.Value;
        try
        {
            await _requests.DeleteAsync(workspace, id, cancellationToken);
            _responses.Remove((workspace.Id, id));
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, null);
            // The row below the one deleted takes its place, as it does in every list in this app;
            // on the last row that is the row above.
            if (_visible.Value.Count > 0)
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            NotificationRequested?.Invoke(TuiCommandResult.Ok($"deleted request {item.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            NotificationRequested?.Invoke(TuiCommandResult.Failed($"delete failed: {exception.Message}"));
        }
    }

    /// <summary>
    /// Writes a body in the reader's own editor. The editor screen is put down first and picked up
    /// again afterwards, because the other program needs the terminal this one is drawing on.
    /// </summary>
    /// <remarks>
    /// The outcome is reported on the editor screen rather than through the shell: the shell's
    /// message line is behind the view the reader is looking at. The action itself therefore returns
    /// nothing to say, and a failure is held until the view is back to say it on.
    /// </remarks>
    private void EditContentExternally(ExternalContentEdit edit)
    {
        if (_editorView is not { } view)
            return;

        if (!_editor.IsConfigured)
        {
            view.Report("no default editor is configured; set EDITOR to write a body", error: true);
            return;
        }

        view.Suspend();
        ExternalActionRequested?.Invoke(new TuiExternalAction(async token =>
        {
            try
            {
                edit.Apply(await _editor.EditAsync(edit.Document, edit.Extension, token));
            }
            catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
            {
                _editorNotice = $"edit failed: {exception.Message}";
            }

            return TuiCommandResult.None;
        }, _list));
    }

    /// <remarks>
    /// Create and save are one path with one difference, which is whether Core is being given a
    /// request it has never seen. The editor state is the same either way, and an edit applies onto
    /// the request as loaded so fields the form does not show — its group, its access times — survive
    /// being edited by a form that never mentions them.
    /// </remarks>
    private async Task<TuiCommandResult> SaveEditAsync(
        RequestEditorState state, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
            return TuiCommandResult.Failed("no active workspace");

        try
        {
            StraumrRequest saved;
            bool created = _editingId is null;
            if (_editingId is { } id)
            {
                StraumrRequest existing = await _requests.GetAsync(workspace, id, updateLastAccessed: false, cancellationToken);
                state.ApplyTo(existing);
                saved = await _requests.SaveAsync(workspace, existing, cancellationToken);
                _responses.Remove((workspace.Id, id));
            }
            else
            {
                saved = await _requests.CreateAsync(workspace, state.ToRequest(), cancellationToken);
                // The editor is still open on it, so from here it is a request that exists.
                _editingId = saved.Id;
            }

            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, saved.Id);
            return TuiCommandResult.Ok(created
                ? $"created request {saved.Name}"
                : $"updated request {saved.Name}");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResult.Failed(exception.Message);
        }
    }

    /// <summary>
    /// A request that parses is edited in the form; one that does not cannot be loaded into fields
    /// at all, so the same key opens the text that needs repairing.
    /// </summary>
    private void RequestEdit()
    {
        if (SelectedItem is not { } item)
            return;
        if (!item.IsBroken)
        {
            OpenEditor(item, 'e');
            return;
        }

        NotifyIfFailed(EditAsJson(item));
    }

    /// <remarks>
    /// It answers rather than reports, because a command has to carry its own failure: a
    /// notification raised while a command runs is replaced by the result that command returns.
    /// The keys that call it report for themselves through <see cref="NotifyIfFailed"/>.
    /// </remarks>
    private TuiCommandResult EditAsJson(RequestScreenItem item)
    {
        if (_workspace is not { } workspace)
            return TuiCommandResult.NoWorkspace;
        if (!_editor.IsConfigured)
            return TuiCommandResult.Failed("edit failed: no default editor is configured");
        ExternalActionRequested?.Invoke(new TuiExternalAction(token => EditAsync(workspace, item, token), _list));
        return TuiCommandResult.None;
    }

    private void NotifyIfFailed(TuiCommandResult result)
    {
        if (result.IsError)
            NotificationRequested?.Invoke(result);
    }

    private async Task<TuiCommandResult> EditAsync(StraumrWorkspaceEntry workspace, RequestScreenItem item, CancellationToken cancellationToken)
    {
        try
        {
            string edited = await _editor.EditJsonAsync(await File.ReadAllTextAsync(item.Path, cancellationToken), cancellationToken);
            StraumrRequest? request = null;
            try { request = JsonSerializer.Deserialize(edited, StraumrJsonContext.Default.StraumrRequest); }
            catch (JsonException) { }
            string? problem = Validate(request, item.Id);
            if (problem is not null)
                await File.WriteAllTextAsync(item.Path, edited, cancellationToken);
            else
            {
                _files.CarryCommentsFrom(item.Path, edited);
                await _requests.SaveAsync(workspace, request!, cancellationToken);
            }
            _responses.Remove((workspace.Id, item.Id));
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, item.Id);
            return problem is null ? TuiCommandResult.Ok($"updated request {request!.Name}") :
                TuiCommandResult.Failed($"saved the edit, but {problem}; press e to repair it");
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
        {
            return TuiCommandResult.Failed($"edit failed: {exception.Message}");
        }
    }

    private static string? Validate(StraumrRequest? request, Guid id) => request switch
    {
        null => "the file is not a valid request",
        { } when request.Id != id => "the request ID does not match its file",
        { } when string.IsNullOrWhiteSpace(request.Name) => "the request name is missing",
        { } when request.Name.Contains('"') => "the request name contains a double quote",
        { } when string.IsNullOrWhiteSpace(request.Uri) => "the request URL is missing",
        { Method: null } => "the HTTP method is missing",
        { Params: null } or { Headers: null } or { Bodies: null } => "a request collection is null",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    /// <summary>
    /// A <c>Ctrl</c>-plus-letter action, for something that has to stay reachable without taking one
    /// of the plain letters the screen's own actions use. Secondary, so it yields on a crowded footer
    /// row to the actions most requests are about.
    /// </summary>
    /// <remarks>
    /// A terminal sends <c>Ctrl</c> and a letter as the single C0 byte the letter maps to, so the
    /// gesture has to carry that control character. The letter-and-modifier form is registered beside
    /// it, unpresented, for a host that reports the two separately.
    /// </remarks>
    private static IEnumerable<Command> ControlCommands(
        string id, string label, char letter, Action execute, Func<bool> available)
    {
        yield return ControlCommand(id, label, (char)(char.ToUpperInvariant(letter) & 0x1F),
            CommandPresentation.CommandBar, execute, available);
        yield return ControlCommand($"{id}.Letter", label, letter,
            CommandPresentation.None, execute, available);
    }

    private static Command ControlCommand(string id, string label, char gestureChar,
        CommandPresentation presentation, Action execute, Func<bool> available) => new()
    {
        Id = id, LabelMarkup = label, Gesture = new KeyGesture(gestureChar, TerminalModifiers.Ctrl),
        Importance = CommandImportance.Secondary, Presentation = presentation,
        IsVisible = _ => available(), CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false, Execute = _ => execute()
    };

    /// <summary>
    /// A fresh <c>s</c> for each visual that offers it. One <see cref="Command"/> instance belongs to
    /// the visual it is added to, so the list and the detail regions each get their own.
    /// </summary>
    private Command SendCommand() =>
        ActionCommand("Send fullscreen", 's', QueueSend, () => SelectedItem is { IsBroken: false });

    private static Command ActionCommand(string label, char key, Action execute, Func<bool> available,
        CommandPresentation presentation = CommandPresentation.CommandBar) => new()
    {
        Id = $"Request.{label}", LabelMarkup = label, Gesture = new KeyGesture(key),
        Importance = CommandImportance.Primary, Presentation = presentation,
        IsVisible = _ => available(), CanExecute = _ => available(), Execute = _ => execute()
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
