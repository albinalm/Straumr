using System.Text;
using System.Text.Json;
using Straumr.Console.Shared.Models;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Visuals.Shared;
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

    private readonly IStraumrOptionsService _options;
    private readonly IStraumrWorkspaceService _workspaces;
    private readonly IStraumrRequestService _requests;
    private readonly IStraumrAuthService _auths;
    private readonly IStraumrSecretService _secrets;
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
    private readonly Visual _sections;
    private readonly Dictionary<(Guid Workspace, Guid Request), StraumrResponse> _responses = [];
    private List<RequestScreenItem> _items = [];
    private readonly State<List<RequestScreenItem>> _visible = new([]);
    private StraumrWorkspaceEntry? _workspace;
    private Guid? _displayedId;
    private Guid? _pendingSend;
    private CancellationTokenSource? _sendCancellation;
    private RequestResponseView? _responseView;
    private bool _savePaneLayout;

    public RequestScreen(IStraumrOptionsService options, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, IStraumrSecretService secrets,
        ExternalEditor editor)
    {
        (_options, _workspaces, _requests, _auths, _secrets, _editor) =
            (options, workspaces, requests, auths, secrets, editor);
        _responseBody = new ResponseBodyActions(_responsePreview, (message, failed) =>
            NotificationRequested?.Invoke(failed ? TuiCommandResult.Failed(message) : TuiCommandResult.Ok(message)));
        _responsePreview.Root.AddCommand(ActionCommand("Fullscreen", 'v', OpenResponse,
            () => _workspace is { } workspace && SelectedItem is { IsBroken: false } item && _responses.ContainsKey((workspace.Id, item.Id))));
        StraumrPaneLayout paneLayout = options.Options.PaneLayouts.GetValueOrDefault(PaneLayoutKey)
            ?? new StraumrPaneLayout();
        _splits = new PaneSplits(paneLayout.Panels, paneLayout.Sections, paneLayout.Stack);
        _splits.Changed += QueuePaneLayoutSave;
        _list = new ResourceList([], ResourceScreenLayout.Message(
            new TextBlock(() => _emptyMessage.Value)
                .Style(() => _loadError.Value ? StraumrStyles.RedText : StraumrStyles.MutedText)
                .Wrap(true).Trimming(TextTrimming.EndEllipsis)), activateLabel: "Inspect") { AutoFocus = true };
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => _requestPreview.FocusTarget.App?.Focus(_requestPreview.FocusTarget);
        _filter = new ResourceFilter("filter requests", ApplyFilter, () => _list);
        _list.AddCommand(ActionCommand("Edit", 'e', RequestEdit, () => SelectedItem is not null));
        _list.AddCommand(ActionCommand("Send fullscreen", 's', QueueSend, () => SelectedItem is { IsBroken: false }));
        _authView = new ScrollableContent(new ComputedVisual(BuildAuthentication));
        Visual responsePane = ResourceScreenLayout.Pane(_responsePreview.Root);
        var responseRule = StraumrSurfaces.TitledDivider("Response", responsePane.Owns);
        responseRule.EndLabel = new TextBlock(() => _responseSummary.Value)
            .Style(() => _responseFailed.Value ? StraumrStyles.RedText : StraumrStyles.MutedBrightText)
            .Trimming(TextTrimming.EndEllipsis);
        _sections = ResourceScreenLayout.StackedSections(_splits,
            ResourceScreenLayout.TwoPaneSections(_splits, "Authentication", ResourceScreenLayout.Pane(_authView),
                "Request", ResourceScreenLayout.Pane(_requestPreview.Root)),
            responseRule, responsePane);
        Visual listView = ResourceScreenLayout.Scrollable(_list);
        Root = ResourceScreenLayout.Create("Requests",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter,
            _splits,
            () => _loading.Value ? ResourceScreenLayout.Message(new HStack(new Spinner(),
                new TextBlock("Loading requests…").Style(StraumrStyles.MutedText)).Spacing(1)) : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayout.EmptySections() : _sections);
        PromptCommands =
        [
            new TuiCommand("request", SelectRequestAsync) { Aliases = ["r"], ArgumentValues = () => _items.Select(item => item.Name) },
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
            await _options.LoadAsync(cancellationToken);
            _workspace = _options.Options.CurrentWorkspace;
            ActiveWorkspaceName = null;
            _items = [];
            if (_workspace is null)
            {
                _emptyMessage.Value = "No active workspace. Use :workspaces to choose one.";
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
                string path = Path.Combine(Path.GetDirectoryName(_workspace.Path)!, $"{id}.json");
                if (!File.Exists(path))
                    continue;
                _items.Add(await LoadItemAsync(id, path, cancellationToken));
            }
            _items = _items.OrderByDescending(item => item.Request?.LastAccessed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
            _emptyMessage.Value = $"Cannot load requests: {exception.Message}\nUse :refresh to retry, or :workspaces.";
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

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        _responseView?.Update();
        if (_savePaneLayout)
        {
            _savePaneLayout = false;
            try
            {
                await _options.SaveAsync(cancellationToken);
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
                new TextBlock(RequestEditorState.FromRequest(request).GetDisplayUri()).Style(StraumrStyles.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch)).Spacing(2)
            : new TextBlock($"{item.Name} · cannot be read").Style(StraumrStyles.RedText).Trimming(TextTrimming.EndEllipsis);
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
            ("Status", FieldList.Wrapped(auth.Status)));
        var content = new VStack(fields).Spacing(1).HorizontalAlignment(Align.Stretch);
        if (auth.Type != "None" && auth.Type.Length > 0)
            content.Add(new TextBlock("Credential material hidden").Style(StraumrStyles.MutedText).Wrap(true));
        if (auth.Problem is { } problem)
            content.Add(FieldList.Problem(problem));
        content.Add(new TextBlock("Secret references").Style(StraumrStyles.MutedBrightText));
        content.Add(FieldList.Wrapped(auth.References));
        return content;
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
            _responsePreview.SetText("No response yet. Press s on the request to send it.", "No response headers.", "No response yet.");
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
        _list.SetRows(_visible.Value.Select(item => new ResourceRow(item.Name, IsBroken: item.IsBroken,
            LeadingToken: item.Request is { } request ? new ResourceToken(request.Method.Method,
                HttpMethodFormatting.Style(request.Method), request.Method == HttpMethod.Delete ? StraumrStyles.RedBrightText : null) : null)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value && _workspace is not null)
            _emptyMessage.Value = _query.Value.Length > 0 ? "No requests match this filter." : "No requests in this workspace.";
    }

    private Task<TuiCommandResult> SelectRequestAsync(string name, CancellationToken cancellationToken)
    {
        if (name.Length == 0)
            return Task.FromResult(TuiCommandResult.Failed("usage: request <name>"));
        List<RequestScreenItem> matches = _items.FindAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count != 1)
            return Task.FromResult(TuiCommandResult.Failed(matches.Count == 0 ? $"no request matches {name}" :
                $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}"));
        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return Task.FromResult(TuiCommandResult.None);
    }

    private async Task<TuiCommandResult> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
            return TuiCommandResult.Failed("usage: refresh");
        await LoadAsync(cancellationToken);
        return _loadError.Value ? TuiCommandResult.Failed(_emptyMessage.Value) :
            TuiCommandResult.Ok($"reloaded {CountFormatting.Label(_items.Count, "request")}");
    }

    private void QueueSend()
    {
        if (SelectedItem is not { IsBroken: false } item || _sendCancellation is not null)
            return;
        _responseView = BuildResponseView(item.Id, item.Request!, () => _list.App?.Focus(_list));
        QueueSend(item.Id);
        _responseView.Show();
    }

    private void OpenResponse()
    {
        if (_workspace is not { } workspace || SelectedItem is not { Request: { } request } item ||
            !_responses.TryGetValue((workspace.Id, item.Id), out StraumrResponse? response))
            return;
        _responseView = BuildResponseView(item.Id, request,
            () => _responsePreview.FocusTarget.App?.Focus(_responsePreview.FocusTarget));
        _responseView.Complete(response, cached: true);
        _responseView.Show();
    }

    /// <remarks>
    /// Sending again is the same send, so it goes through the same queue rather than around it: the
    /// view returns to its in-flight state and <see cref="UpdateAsync"/> runs the request where every
    /// other Core call runs. It stands down while one is already in flight, as `s` on the list does.
    /// </remarks>
    private RequestResponseView BuildResponseView(Guid id, StraumrRequest request, Action restoreFocus) =>
        new(request, ActiveWorkspaceName, () => _sendCancellation?.Cancel(), () =>
        {
            if (_sendCancellation is not null)
                return;
            QueueSend(id);
            _responseView?.Restart();
        }, () =>
        {
            _responseView = null;
            restoreFocus();
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

    private void RequestEdit()
    {
        if (SelectedItem is not { } item || _workspace is not { } workspace)
            return;
        if (!_editor.IsConfigured)
        {
            NotificationRequested?.Invoke(TuiCommandResult.Failed("edit failed: no default editor is configured"));
            return;
        }
        ExternalActionRequested?.Invoke(new TuiExternalAction(token => EditAsync(workspace, item, token), _list));
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
                await _requests.SaveAsync(workspace, request!, cancellationToken);
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
        { } when string.IsNullOrWhiteSpace(request.Uri) => "the request URL is missing",
        { Method: null } => "the HTTP method is missing",
        { Params: null } or { Headers: null } or { Bodies: null } => "a request collection is null",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    private static Command ActionCommand(string label, char key, Action execute, Func<bool> available) => new()
    {
        Id = $"Request.{label}", LabelMarkup = label, Gesture = new KeyGesture(key),
        Importance = CommandImportance.Primary, Presentation = CommandPresentation.CommandBar,
        IsVisible = _ => available(), CanExecute = _ => available(), Execute = _ => execute()
    };

    private void QueuePaneLayoutSave()
    {
        _options.Options.PaneLayouts[PaneLayoutKey] = new StraumrPaneLayout
        {
            Panels = _splits.Panels.Share,
            Sections = _splits.Sections.Share,
            Stack = _splits.Stack.Share
        };
        _savePaneLayout = true;
    }
}
