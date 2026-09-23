using System.Diagnostics;
using System.Text.Json;
using Straumr.Console.Tui.Screens.Components.Auth;
using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;
using ScrollableContent = Straumr.Console.Tui.Screens.Components.Shared.ScrollableContent;

namespace Straumr.Console.Tui.Screens;

public sealed class AuthScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Auths);
    private readonly IStraumrAuthService _auths;
    private readonly ScrollableContent _configurationView;
    private readonly State<int> _count = new(0);
    private readonly ScrollableContent _credentialView;
    private readonly ExternalEditorService _editor;
    private readonly State<string> _emptyMessage = new("Loading auths…");

    private readonly Stopwatch _sendClock = new();
    private readonly State<bool> _sending = new(false);
    private readonly IStraumrFileService _files;
    private readonly ResourceFilter _filter;
    private readonly ResourceList _list;
    private readonly State<bool> _loadError = new(false);
    private readonly State<bool> _loading = new(true);
    private readonly State<int> _matchCount = new(0);
    private readonly State<string> _query = new(string.Empty);

    private readonly State<IReadOnlyList<ReferenceModel>?> _references = new(null);
    private readonly IStraumrRequestService _requestService;
    private readonly IStraumrSecretService _secrets;
    private readonly ScrollableContent _referencesView;
    private readonly IStraumrVariableService _variables;
    private readonly Visual _sections;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly PaneSplits _splits;

    private readonly IStraumrStateService _state;

    private readonly State<IReadOnlyList<StraumrRequest>> _usedBy = new([]);
    private readonly ScrollableContent _usedByView;
    private readonly State<List<AuthScreenItemModel>> _visible = new([]);
    private readonly IStraumrWorkspaceService _workspaces;
    private Guid? _displayedId;

    private Guid? _editingId;

    private string? _editorNotice;

    private AuthEditor? _editorView;
    private CancellationTokenSource? _sendCancellation;
    private List<AuthScreenItemModel> _items = [];
    private Guid? _pendingDeleteId;
    private Guid? _pendingSendId;
    private Guid? _sentAuthId;
    private readonly State<string?> _sentValue = new(null);

    private Func<CancellationToken, Task<TuiCommandResultModel>>? _pendingSave;

    private bool _savePaneLayout;

    private StraumrWorkspaceEntry? _workspace;

    private IReadOnlyList<StraumrRequest> _workspaceRequests = [];

    public AuthScreen(
        IStraumrStateService state,
        IStraumrWorkspaceService workspaces,
        IStraumrAuthService auths,
        IStraumrRequestService requests,
        IStraumrSecretService secrets,
        IStraumrVariableService variables,
        IStraumrFileService files,
        ExternalEditorService editor)
    {
        (_state, _workspaces, _auths, _requestService, _secrets, _variables, _files, _editor) =
            (state, workspaces, auths, requests, secrets, variables, files, editor);

        StraumrPaneLayout paneLayout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey)
                                       ?? new StraumrPaneLayout { Stack = 65 };
        _splits = new PaneSplits(paneLayout.Panels, paneLayout.Sections, paneLayout.Stack);
        _splits.Changed += QueuePaneLayoutSave;

        _list = new ResourceList([], ResourceScreenLayoutHelpers.Message(
                new TextBlock(() => _emptyMessage.Value)
                    .Style(() => _loadError.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedText)
                    .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            TuiKeybindHelpers.CombinedLabel("Edit", "Auth.Edit"));
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => AuthEdit();
        _filter = new ResourceFilter("filter auths", ApplyFilter, () => _list);

        _list.AddCommand(ActionCommand("New", () => OpenEditor(null, false), () => _workspace is not null));
        _list.AddCommand(ActionCommand("Edit", AuthEdit, () => SelectedItem is not null,
            TuiKeybindHelpers.SecondaryPresentation("ResourceList.Activate")));
        _list.AddCommand(ActionCommand("Copy", () => OpenEditor(SelectedItem, true),
            () => SelectedItem is { IsBroken: false }));
        _list.AddCommand(ActionCommand("Delete", ShowDeleteDialog, () => SelectedItem is not null));
        foreach (Command command in ControlCommands("Auth.EditJson", "Edit JSON",
                     () =>
                     {
                         if (SelectedItem is { } item) { NotifyIfFailed(EditAsJson(item)); }
                     },
                     () => SelectedItem is not null))
        {
            _list.AddCommand(command);
        }

        _list.AddCommand(SendCommand());
        _list.AddCommand(CancelSendCommand());

        _configurationView = new ScrollableContent(new ComputedVisual(BuildConfiguration));
        _credentialView = new ScrollableContent(new ComputedVisual(BuildCredential));
        _referencesView = new ScrollableContent(new ComputedVisual(BuildReferences));
        _usedByView = new ScrollableContent(new ComputedVisual(BuildUsedBy));

        Visual configurationPane = ResourceScreenLayoutHelpers.Pane(_configurationView);
        Visual credentialPane = ResourceScreenLayoutHelpers.Pane(_credentialView);
        Visual referencesPane = ResourceScreenLayoutHelpers.Pane(_referencesView);
        Visual usedByPane = ResourceScreenLayoutHelpers.Pane(_usedByView);

        _sections = ResourceScreenLayoutHelpers.FourPaneSections(_splits,
            ("Configuration", configurationPane),
            ("Credential", credentialPane),
            ("Variables & Secrets", referencesPane),
            ("Used by", usedByPane));
        _sections.AddCommand(SendCommand());
        _sections.AddCommand(CancelSendCommand());

        Visual listView = ResourceScreenLayoutHelpers.Scrollable(_list);
        Root = ResourceScreenLayoutHelpers.Create("Auths",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter,
            _splits,
            () => _loading.Value
                ? ResourceScreenLayoutHelpers.Message(new HStack(new Spinner(),
                    new TextBlock("Loading auths…").Style(StraumrStyleService.MutedText)).Spacing(1))
                : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayoutHelpers.EmptySections() : _sections);

        PromptCommands =
        [
            new TuiCommandModel("select", SelectAuthAsync) { Aliases = ["a"], ArgumentValues = AuthNames },
            new TuiCommandModel("create", CreateAuthAsync) { Aliases = ["new"] },
            new TuiCommandModel("edit", EditAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommandModel("copy", CopyAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommandModel("delete", DeleteAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommandModel("send", SendAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommandModel("json", EditJsonAsync) { ArgumentValues = AuthNames },
            new TuiCommandModel("refresh", RefreshAsync)
        ];
    }

    private AuthScreenItemModel? SelectedItem =>
        (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

    public TuiScreen Kind => TuiScreen.Auths;

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
        _references.Value = null;
        _usedBy.Value = [];
        _sentAuthId = null;
        _sentValue.Value = null;
        try
        {
            await _state.LoadAsync(cancellationToken);
            _workspace = _state.State.CurrentWorkspace;
            ActiveWorkspaceName = null;
            _items = [];
            _workspaceRequests = [];
            if (_workspace is null)
            {
                _emptyMessage.Value = "No active workspace. Use :workspace to choose one.";
                ApplyFilter(string.Empty);
                return;
            }

            StraumrWorkspace workspace =
                await _workspaces.GetAsync(_workspace.Id, false, cancellationToken);
            if (workspace.Id != _workspace.Id)
            {
                throw new StraumrException(
                    "The active workspace has a mismatched ID; repair it in Workspaces.", StraumrError.CorruptEntry);
            }

            ActiveWorkspaceName = workspace.Name;
            foreach (Guid id in workspace.Auths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(Path.GetDirectoryName(_workspace.Path)!, $"{id}.jsonc");
                if (!File.Exists(path))
                {
                    continue;
                }

                _items.Add(await LoadItemAsync(id, path, cancellationToken));
            }

            _items = _items.OrderByDescending(item => item.Auth?.LastAccessed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _workspaceRequests = await LoadRequestsAsync(cancellationToken);
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
            _emptyMessage.Value = $"Cannot load auths: {exception.Message}\nUse :refresh to retry, or :workspace.";
            ApplyFilter(_filter.Text);
        }
        finally
        {
            _loading.Value = false;
        }
    }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
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
                if (editor.Saved())
                {
                    NotificationRequested?.Invoke(result);
                }
                else
                {
                    editor.Report(result.Message!, false);
                }
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
                NotificationRequested?.Invoke(
                    TuiCommandResultModel.Failed($"cannot save pane layout: {exception.Message}"));
            }
        }

        if (_pendingSendId is { } sendId)
        {
            _pendingSendId = null;
            await SendAsync(sendId, cancellationToken);
        }

        AuthScreenItemModel? item = SelectedItem;
        if (item?.Id == _displayedId)
        {
            return;
        }

        _displayedId = item?.Id;
        _configurationView.ScrollOffset = 0;
        _credentialView.ScrollOffset = 0;
        _usedByView.ScrollOffset = 0;
        _references.Value = null;
        _usedBy.Value = item is null
            ? []
            : _workspaceRequests.Where(request => request.AuthId == item.Id)
                .OrderBy(request => request.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (item?.Auth is not { } auth)
        {
            _references.Value = [];
            return;
        }

        IReadOnlyList<ReferenceModel> references = await ReferenceHelpers.ResolveAsync(
            _secrets,
            _variables,
            _workspace!,
            null,
            auth,
            IsRecoverable,
            cancellationToken);
        if (SelectedItem?.Id == item.Id)
        {
            _references.Value = references;
        }
    }

    private async Task<AuthScreenItemModel> LoadItemAsync(Guid id, string path, CancellationToken cancellationToken)
    {
        try
        {
            StraumrAuth auth = await _auths.GetAsync(_workspace!, id, false, cancellationToken);
            string? problem = Validate(auth, id);
            return new AuthScreenItemModel(id, path, problem is null ? auth : null, problem);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new AuthScreenItemModel(id, path, null, exception.Message);
        }
    }

    private async Task<IReadOnlyList<StraumrRequest>> LoadRequestsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _requestService.ListAsync(_workspace!, cancellationToken);
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
            return new TextBlock("No auth selected.").Style(StraumrStyleService.MutedText)
                .Trimming(TextTrimming.EndEllipsis);
        }

        Visual summary = item.Auth is { } auth
            ? new HStack(
                    new TextBlock(SecretFormatting.Display(auth.Name)).Style(StraumrStyleService.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis),
                    new TextBlock(AuthFormatting.TypeName(auth.Config)).Style(StraumrStyleService.AccentText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .HorizontalAlignment(Align.Stretch))
                .Spacing(2)
                .HorizontalAlignment(Align.Stretch)
            : new TextBlock($"{item.Name} · cannot be read").Style(StraumrStyleService.RedText)
                .Trimming(TextTrimming.EndEllipsis);

        Visual badge = new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyleService.TokenChip);
        return StraumrSurfaceHelpers.Bar(summary, new ComputedVisual(() => _sending.Value
            ? InFlightPulseHelpers.Create("SENDING", _sendClock)
            : badge));
    }

    private Visual BuildConfiguration()
    {
        if (SelectedItem is not { } item)
        {
            return Unavailable();
        }

        if (item.Auth is not { } auth)
        {
            return new VStack(
                    FieldListHelpers.Create(
                        ("Problem", FieldListHelpers.Problem(item.Problem ?? "the file is not a valid auth")),
                        ("Path", FieldListHelpers.Wrapped(PathFormatting.Display(item.Path)))),
                    new TextBlock($"Press {TuiKeybindHelpers.Hint("Auth.Edit")} to open the file in your editor and repair it.")
                        .Style(StraumrStyleService.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch))
                .Spacing(1)
                .HorizontalAlignment(Align.Stretch);
        }

        List<(string Label, Visual Value)> rows = new();
        switch (auth.Config)
        {
            case BearerAuthConfig bearer:
                rows.Add(("Prefix", Value(bearer.Prefix)));
                break;
            case BasicAuthConfig basic:
                rows.Add(("Username", Value(basic.Username)));
                break;
            case OAuth2Config oauth:
                rows.Add(("Grant", FieldListHelpers.Text(AuthEditingHelpers.GrantDisplayName(oauth.GrantType))));
                rows.Add(("Token URL", Value(oauth.TokenUrl)));
                rows.Add(("Client ID", Value(oauth.ClientId)));
                rows.Add(("Client secret", Credential(oauth.ClientSecret)));
                rows.Add(("Scope", Value(oauth.Scope)));
                if (oauth.GrantType == OAuth2GrantType.AuthorizationCode)
                {
                    rows.Add(("Authorization URL", Value(oauth.AuthorizationUrl)));
                    rows.Add(("Redirect URI", Value(oauth.RedirectUri)));
                    rows.Add(("PKCE", oauth.UsePkce
                        ? FieldListHelpers.Styled(oauth.CodeChallengeMethod, StraumrStyleService.AmberText)
                        : FieldListHelpers.Styled("off", StraumrStyleService.MutedText)));
                }

                if (oauth.GrantType == OAuth2GrantType.ResourceOwnerPassword)
                {
                    rows.Add(("Username", Value(oauth.Username)));
                    rows.Add(("Password", Credential(oauth.Password)));
                }

                break;
            case CustomAuthConfig custom:
                rows.Add(("Request", Value($"{custom.Method} {custom.Url}".Trim())));
                rows.Add(("Headers", FieldListHelpers.Count(custom.Headers.Count)));
                rows.Add(("Params", FieldListHelpers.Count(custom.Params.Count)));
                rows.Add(("Body", FieldListHelpers.Text(RequestEditingHelpers.BodyTypeDisplayName(custom.BodyType))));
                rows.Add(("Source", FieldListHelpers.Text(AuthEditingHelpers.ExtractionSourceDisplayName(custom.Source))));
                rows.Add(("Expression", Value(custom.ExtractionExpression)));
                break;
        }

        return FieldListHelpers.Create(rows.ToArray());
    }

    private Visual BuildCredential()
    {
        if (SelectedItem is not { } item)
        {
            return Unavailable();
        }

        if (item.Auth is not { } auth)
        {
            return new TextBlock("Unavailable until the file is valid.")
                .Style(StraumrStyleService.MutedText).Wrap(true);
        }

        AuthStatusModel status = AuthFormatting.Status(auth);
        List<(string Label, Visual Value)> rows = new()
        {
            ("Status", FieldListHelpers.Styled(status.Text, status.Style))
        };

        switch (auth.Config)
        {
            case BearerAuthConfig bearer when IsReference(bearer.Token):
                rows.Add(("Token", FieldListHelpers.Wrapped(SecretFormatting.Display(bearer.Token))));
                break;
            case BasicAuthConfig basic when IsReference(basic.Password):
                rows.Add(("Password", FieldListHelpers.Wrapped(SecretFormatting.Display(basic.Password))));
                break;
            case OAuth2Config { Token: { } token }:
                rows.Add(("Token type", FieldListHelpers.Text(token.TokenType)));
                rows.Add(("Expires", token.ExpiresAt is { } expiry
                    ? FieldListHelpers.Styled(TimestampFormatting.Absolute(expiry),
                        token.IsExpired ? StraumrStyleService.RedText : StraumrStyleService.AmberText)
                    : FieldListHelpers.Styled("no expiry", StraumrStyleService.MutedText)));
                rows.Add(("Refresh token", token.RefreshToken is null
                    ? FieldListHelpers.Styled("none", StraumrStyleService.MutedText)
                    : FieldListHelpers.Styled("available", StraumrStyleService.AmberText)));
                break;
            case CustomAuthConfig custom:
                rows.Add(("Template", FieldListHelpers.Wrapped(custom.ApplyHeaderTemplate)));
                break;
        }

        if (_sentAuthId == item.Id && _sentValue.Value is { } value)
        {
            rows.Add(("Last send", FieldListHelpers.Styled("value extracted", StraumrStyleService.AmberText)));
            rows.Add(("Extracted", FieldListHelpers.Wrapped(value)));
        }

        rows.Add(("Injects", FieldListHelpers.Text(AuthFormatting.Injects(auth.Config))));
        if (AuthEditingHelpers.SupportsFetch(auth.Config))
        {
            rows.Add(("Auto-renew", auth.AutoRenewAuth
                ? FieldListHelpers.Styled("on", StraumrStyleService.AmberText)
                : FieldListHelpers.Styled("off", StraumrStyleService.MutedText)));
        }

        return FieldListHelpers.Create(rows.ToArray());
    }

    private Visual BuildReferences()
    {
        if (SelectedItem is null)
        {
            return Unavailable();
        }

        if (_references.Value is not { } references)
        {
            return new TextBlock("Loading…").Style(StraumrStyleService.MutedText);
        }

        return ReferenceListHelpers.Create(references);
    }

    private Visual BuildUsedBy()
    {
        if (SelectedItem is null)
        {
            return Unavailable();
        }

        IReadOnlyList<StraumrRequest> requests = _usedBy.Value;
        if (requests.Count == 0)
        {
            return new TextBlock("No requests use this auth.").Style(StraumrStyleService.MutedText).Wrap(true);
        }

        return new VStack(requests
                .Select(request => (Visual)new HStack(
                        new TextBlock(request.Method.Method)
                            .Style(HttpMethodFormatting.Style(request.Method))
                            .MinWidth(request.Method.Method.Length),
                        new TextBlock(SecretFormatting.Display(request.Name))
                            .Style(StraumrStyleService.PrimaryText)
                            .Trimming(TextTrimming.EndEllipsis)
                            .HorizontalAlignment(Align.Stretch))
                    .Spacing(1)
                    .HorizontalAlignment(Align.Stretch))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }

    private static Visual Unavailable() =>
        new TextBlock("Unavailable.").Style(StraumrStyleService.MutedText);

    private static Visual Value(string value) =>
        value.Length == 0
            ? FieldListHelpers.Styled("not set", StraumrStyleService.MutedText)
            : FieldListHelpers.Wrapped(SecretFormatting.Display(value));

    private static Visual Credential(string value) =>
        IsReference(value) ? FieldListHelpers.Wrapped(SecretFormatting.Display(value)) :
        value.Length == 0 ? FieldListHelpers.Styled("not set", StraumrStyleService.MutedText) :
        FieldListHelpers.Styled("set", StraumrStyleService.AmberText);

    private static bool IsReference(string value) => VariableHelpers.ReferencePattern.IsMatch(value);

    private void ApplyFilter(string query) => ApplyFilter(query, SelectedItem?.Id);

    private void ApplyFilter(string query, Guid? selected)
    {
        _query.Value = query.Trim();
        _count.Value = _items.Count;
        _visible.Value = _items.Where(item => _query.Value.Length == 0 ||
                                              item.Name.Contains(_query.Value, StringComparison.OrdinalIgnoreCase) ||
                                              item.Auth is { } auth &&
                                              AuthFormatting.Meta(auth.Config).Contains(_query.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        _matchCount.Value = _visible.Value.Count;
        _list.SetRows(_visible.Value.Select(item => new ResourceRowModel(
            SecretFormatting.Display(item.Name),
            item.Auth is { } auth ? AuthFormatting.Meta(auth.Config) : "cannot be read",
            item.Auth is { } held && AuthFormatting.HasCredential(held.Config),
            IsBroken: item.IsBroken)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value && _workspace is not null)
        {
            _emptyMessage.Value = _query.Value.Length > 0
                ? "No auths match this filter."
                : $"No auths in this workspace. Press {TuiKeybindHelpers.Hint("Auth.New")} to create one.";
        }
    }

    private IEnumerable<string> AuthNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResultModel> SelectAuthAsync(string argument, CancellationToken cancellationToken)
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

        return Task.FromResult(SelectAuth(name));
    }

    private TuiCommandResultModel OnSelected(string argument, Func<AuthScreenItemModel, TuiCommandResultModel> action)
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
            TuiCommandResultModel selection = SelectAuth(name);
            if (selection.IsError)
            {
                return selection;
            }
        }

        return SelectedItem is { } item ? action(item) : TuiCommandResultModel.Failed("no auth selected");
    }

    private Task<TuiCommandResultModel> CreateAuthAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
        {
            return Task.FromResult(TuiCommandResultModel.NoWorkspace);
        }

        return Task.FromResult(argument.Length > 0
            ? TuiCommandResultModel.Failed("usage: create")
            : OpenEditorFor(null, false));
    }

    private Task<TuiCommandResultModel> EditAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? EditAsJson(item)
            : OpenEditorFor(item, false)));

    private Task<TuiCommandResultModel> CopyAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? TuiCommandResultModel.Failed($"cannot copy {item.Name}: the auth cannot be read")
            : OpenEditorFor(item, true)));

    private Task<TuiCommandResultModel> DeleteAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ =>
        {
            ShowDeleteDialog();
            return TuiCommandResultModel.None;
        }));

    private TuiCommandResultModel OpenEditorFor(AuthScreenItemModel? source, bool copy)
    {
        if (_editorView is not null)
        {
            return TuiCommandResultModel.Failed("the auth editor is already open");
        }

        OpenEditor(source, copy);
        return TuiCommandResultModel.None;
    }

    private TuiCommandResultModel SelectAuth(string name)
    {
        List<AuthScreenItemModel> matches = _items.FindAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
        {
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }

        if (matches.Count != 1)
        {
            return TuiCommandResultModel.Failed(matches.Count == 0
                ? $"no auth matches {name}"
                : $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        }

        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResultModel.None;
    }

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
        return _loadError.Value
            ? TuiCommandResultModel.Failed(_emptyMessage.Value)
            : TuiCommandResultModel.Ok($"reloaded {CountFormatting.Label(_items.Count, "auth")}");
    }

    private Task<TuiCommandResultModel> SendAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ => QueueSend()));

    private Task<TuiCommandResultModel> EditJsonAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, EditAsJson));

    private TuiCommandResultModel QueueSend()
    {
        if (_workspace is null)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        if (SelectedItem is not { Auth: { } auth } item)
        {
            return TuiCommandResultModel.Failed("no auth selected");
        }

        if (!AuthEditingHelpers.SupportsFetch(auth.Config))
        {
            return TuiCommandResultModel.Failed($"{auth.Name} holds its credential; there is no auth request to send");
        }

        if (_sendCancellation is not null)
        {
            return TuiCommandResultModel.Failed("an auth is already being sent");
        }

        _sentAuthId = null;
        _sentValue.Value = null;
        _sendCancellation = new CancellationTokenSource();
        _sendClock.Restart();
        _sending.Value = true;
        _pendingSendId = item.Id;
        return TuiCommandResultModel.None;
    }

    private async Task SendAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace || _sendCancellation is null)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _sendCancellation.Token);
        try
        {
            StraumrAuth auth = await _auths.GetAsync(workspace, id, false, linked.Token);
            string value = await _requestService.SendAuthAsync(workspace, auth, linked.Token);
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, id);
            _sentAuthId = id;
            _sentValue.Value = value;
            NotificationRequested?.Invoke(TuiCommandResultModel.Ok($"auth send succeeded for {auth.Name}; value extracted"));
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && _sendCancellation.IsCancellationRequested)
        {
            NotificationRequested?.Invoke(TuiCommandResultModel.Failed("auth send cancelled"));
        }
        catch (Exception exception) when (IsRecoverable(exception) ||
                                          exception is HttpRequestException or UriFormatException
                                              or InvalidOperationException)
        {
            NotificationRequested?.Invoke(TuiCommandResultModel.Failed($"auth send failed: {exception.Message}"));
        }
        finally
        {
            _sendClock.Stop();
            _sending.Value = false;
            _sendCancellation.Dispose();
            _sendCancellation = null;
        }
    }

    private void OpenEditor(AuthScreenItemModel? source, bool copy)
    {
        if (_workspace is null || _editorView is not null)
        {
            return;
        }

        bool isNew = source is null || copy;
        StraumrAuth state = source?.Auth is { } auth
            ? isNew ? auth.CopyAs(string.Empty) : Working(auth)
            : new StraumrAuth
            {
                Name = string.Empty,
                Config = AuthEditingHelpers.CreateConfig(AuthType.Bearer)
            };

        _editingId = isNew ? null : source!.Id;
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new AuthEditor(state, ActiveWorkspaceName, isNew,
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

    private static StraumrAuth Working(StraumrAuth auth)
    {
        StraumrAuth copy = auth.CopyAs(auth.Name);
        copy.Id = auth.Id;
        copy.Modified = auth.Modified;
        copy.LastAccessed = auth.LastAccessed;
        return copy;
    }

    private async Task<TuiCommandResultModel> SaveEditAsync(StraumrAuth state, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.Failed("no active workspace");
        }

        try
        {
            StraumrAuth saved;
            bool created = _editingId is null;
            if (_editingId is { } id)
            {
                StraumrAuth existing = await _auths.GetAsync(workspace, id, false, cancellationToken);
                existing.Name = state.Name;
                existing.Config = state.Config;
                existing.AutoRenewAuth = state.AutoRenewAuth;
                saved = await _auths.SaveAsync(workspace, existing, cancellationToken);
            }
            else
            {
                saved = await _auths.CreateAsync(workspace, state, cancellationToken);
                _editingId = saved.Id;
            }

            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, saved.Id);
            return TuiCommandResultModel.Ok(created ? $"created auth {saved.Name}" : $"updated auth {saved.Name}");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResultModel.Failed(exception.Message);
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

    private void AuthEdit()
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

    private TuiCommandResultModel EditAsJson(AuthScreenItemModel item)
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

    private async Task<TuiCommandResultModel> EditAsync(
        StraumrWorkspaceEntry workspace, AuthScreenItemModel item, CancellationToken cancellationToken)
    {
        try
        {
            string edited = await _editor.EditJsonAsync(
                await File.ReadAllTextAsync(item.Path, cancellationToken), cancellationToken);
            StraumrAuth? auth = null;
            try { auth = JsonSerializer.Deserialize(edited, StraumrJsonContext.Default.StraumrAuth); }
            catch (JsonException) { }
            string? problem = Validate(auth, item.Id);
            if (problem is not null)
            {
                await File.WriteAllTextAsync(item.Path, edited, cancellationToken);
            }
            else
            {
                _files.CarryCommentsFrom(item.Path, edited);
                await _auths.SaveAsync(workspace, auth!, cancellationToken);
            }
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, item.Id);
            return problem is null
                ? TuiCommandResultModel.Ok($"updated auth {auth!.Name}")
                : TuiCommandResultModel.Failed($"saved the edit, but {problem}; press {TuiKeybindHelpers.Hint("Auth.Edit")} to repair it");
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
        {
            return TuiCommandResultModel.Failed($"edit failed: {exception.Message}");
        }
    }

    private void ShowDeleteDialog()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        int dependents = _workspaceRequests.Count(request => request.AuthId == item.Id);
        new ConfirmDialog(
            "Delete auth",
            $"Delete {SecretFormatting.Display(item.Name)}?",
            dependents == 0
                ? "The auth will be permanently deleted."
                : $"The auth will be permanently deleted. {CountFormatting.Label(dependents, "request")} " +
                  "still point at it and will fail to send until they are given another. " +
                  "Deleting an auth does not change them, as deleting one from the CLI does not.",
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
            await _auths.DeleteAsync(workspace, id, cancellationToken);
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, null);
            if (_visible.Value.Count > 0)
            {
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            }

            NotificationRequested?.Invoke(TuiCommandResultModel.Ok($"deleted auth {item.Name}"));
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

    private static string? Validate(StraumrAuth? auth, Guid id) => auth switch
    {
        null => "the file is not a valid auth",
        not null when auth.Id != id => "the auth ID does not match its file",
        not null when string.IsNullOrWhiteSpace(auth.Name) => "the auth name is missing",
        not null when auth.Name.Contains('"') => "the auth name contains a double quote",
        { Config: null } => "the auth has no configuration",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    private Command SendCommand() =>
        ActionCommand("Send", () => NotifyIfFailed(QueueSend()),
            () => !_sending.Value && SelectedItem is { Auth: { } auth } &&
                  AuthEditingHelpers.SupportsFetch(auth.Config));

    private Command CancelSendCommand() =>
        ActionCommandKey("Cancel", () => _sendCancellation?.Cancel(),
            () => _sending.Value);

    private void NotifyIfFailed(TuiCommandResultModel result)
    {
        if (result.IsError)
        {
            NotificationRequested?.Invoke(result);
        }
    }

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

    private static Command ActionCommand(string label, Action execute, Func<bool> available,
        CommandPresentation presentation = CommandPresentation.CommandBar) =>
        ActionCommandKey(label, execute, available, presentation);

    private static Command ActionCommandKey(string label, Action execute,
        Func<bool> available, CommandPresentation presentation = CommandPresentation.CommandBar) => new()
    {
        Id = $"Auth.{label}",
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get($"Auth.{label}"),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => TuiKeybindHelpers.Run($"Auth.{label}", execute)
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
