using System.Diagnostics;
using System.Text.Json;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Shared.Editor;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Helpers;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Auth;

/// <summary>
/// The Auths screen: the workspace's auths, what each one is configured as, what it currently holds,
/// and which requests depend on it.
/// </summary>
/// <remarks>
/// It is the Requests screen's counterpart. Requests names the auth it will send with; this screen
/// is about the auth itself, and says the same things about it in the same words — the status comes
/// from <see cref="AuthFormatting"/> and the secret references from <see cref="SecretReferences"/>,
/// both of which Requests already uses.
/// </remarks>
public sealed class AuthScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Auths);

    private readonly IStraumrStateService _state;
    private readonly IStraumrWorkspaceService _workspaces;
    private readonly IStraumrAuthService _auths;
    private readonly IStraumrRequestService _requestService;
    private readonly IStraumrSecretService _secrets;
    private readonly IStraumrFileService _files;
    private readonly ExternalEditor _editor;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<int> _count = new(0);
    private readonly State<int> _matchCount = new(0);
    private readonly State<string> _query = new(string.Empty);
    private readonly State<string> _emptyMessage = new("Loading auths…");
    private readonly State<bool> _loading = new(true);
    private readonly State<bool> _loadError = new(false);

    /// <summary>The selected auth's secret references, or <see langword="null"/> while they load.</summary>
    private readonly State<IReadOnlyList<SecretReference>?> _references = new(null);

    private readonly State<IReadOnlyList<StraumrRequest>> _usedBy = new([]);
    private readonly State<bool> _fetching = new(false);
    private readonly ResourceList _list;
    private readonly ResourceFilter _filter;
    private readonly PaneSplits _splits;
    private readonly ScrollableContent _configurationView;
    private readonly ScrollableContent _credentialView;
    private readonly ScrollableContent _secretsView;
    private readonly ScrollableContent _usedByView;
    private readonly Visual _sections;
    private List<AuthScreenItem> _items = [];
    private readonly State<List<AuthScreenItem>> _visible = new([]);

    /// <summary>
    /// The workspace's requests, for the Used by region. Loaded with the auths rather than when a row
    /// is selected, because moving the cursor is not a moment to spend on disk.
    /// </summary>
    private IReadOnlyList<StraumrRequest> _workspaceRequests = [];

    private StraumrWorkspaceEntry? _workspace;
    private Guid? _displayedId;
    private Guid? _pendingFetchId;
    private Guid? _pendingDeleteId;
    private CancellationTokenSource? _fetchCancellation;

    /// <summary>Times the fetch the bar is reporting. A field, so the pulse survives a rebuild of the bar.</summary>
    private readonly Stopwatch _fetchClock = new();

    private AuthEditor? _editorView;

    /// <summary>
    /// The auth the open editor writes to, or <see langword="null"/> while it would create one. It is
    /// a field rather than a captured value because the editor stays open after a save: what a create
    /// wrote is what the next save has to change.
    /// </summary>
    private Guid? _editingId;

    /// <summary>
    /// What an external body edit has to say, held until the editor screen is back on the terminal to
    /// say it on. It is set off the update loop, while the app is not running at all.
    /// </summary>
    private string? _editorNotice;

    /// <summary>
    /// The save an open editor has asked for, run on the next update pass. Core calls belong there,
    /// where every other one on this screen runs, rather than inside the keystroke that asked.
    /// </summary>
    private Func<CancellationToken, Task<TuiCommandResult>>? _pendingSave;

    private bool _savePaneLayout;

    public AuthScreen(
        IStraumrStateService state,
        IStraumrWorkspaceService workspaces,
        IStraumrAuthService auths,
        IStraumrRequestService requests,
        IStraumrSecretService secrets,
        IStraumrFileService files,
        ExternalEditor editor)
    {
        (_state, _workspaces, _auths, _requestService, _secrets, _files, _editor) =
            (state, workspaces, auths, requests, secrets, files, editor);

        // The upper regions open with a larger share than the even split the other screens use.
        // Configuration and Credential carry up to seven rows each; Secrets and Used by are usually
        // two, and an even split spent the room on the half that had least to say.
        StraumrPaneLayout paneLayout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey)
            ?? new StraumrPaneLayout { Stack = 65 };
        _splits = new PaneSplits(paneLayout.Panels, paneLayout.Sections, paneLayout.Stack);
        _splits.Changed += QueuePaneLayoutSave;

        _list = new ResourceList([], ResourceScreenLayout.Message(
                new TextBlock(() => _emptyMessage.Value)
                    .Style(() => _loadError.Value ? StraumrStyles.RedText : StraumrStyles.MutedText)
                    .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            // One hint for one action under two keys, as on Requests: the bar renders one keycap per
            // hint, so `e` rides in the label painted in the bar's own key colour.
            activateLabel: $"{StraumrStyles.KeyMarkup("/e")} Edit");
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => AuthEdit();
        _filter = new ResourceFilter("filter auths", ApplyFilter, () => _list);

        _list.AddCommand(ActionCommand("New", 'c', () => OpenEditor(null, 'c'), () => _workspace is not null));
        // One key with two meanings because it is one intent: an auth that parses is edited in the
        // form, and one that does not cannot be loaded into fields at all, so the same key opens the
        // text that needs repairing. Unpresented — `Enter`'s hint names it.
        _list.AddCommand(ActionCommand("Edit", 'e', AuthEdit, () => SelectedItem is not null,
            CommandPresentation.None));
        _list.AddCommand(ActionCommand("Copy", 'y', () => OpenEditor(SelectedItem, 'y'),
            () => SelectedItem is { IsBroken: false }));
        // Offered for a broken auth too, unlike Copy and Fetch: one that cannot be read is one a
        // reader is more likely to want rid of, not less.
        _list.AddCommand(ActionCommand("Delete", 'd', ShowDeleteDialog, () => SelectedItem is not null));
        foreach (Command command in ControlCommands("Auth.EditJson", "Edit JSON", 'e',
                     () => { if (SelectedItem is { } item) NotifyIfFailed(EditAsJson(item)); },
                     () => SelectedItem is not null))
            _list.AddCommand(command);
        _list.AddCommand(FetchCommand());
        _list.AddCommand(CancelFetchCommand());

        _configurationView = new ScrollableContent(new ComputedVisual(BuildConfiguration));
        _credentialView = new ScrollableContent(new ComputedVisual(BuildCredential));
        _secretsView = new ScrollableContent(new ComputedVisual(BuildSecrets));
        _usedByView = new ScrollableContent(new ComputedVisual(BuildUsedBy));

        Visual configurationPane = ResourceScreenLayout.Pane(_configurationView);
        Visual credentialPane = ResourceScreenLayout.Pane(_credentialView);
        Visual secretsPane = ResourceScreenLayout.Pane(_secretsView);
        Visual usedByPane = ResourceScreenLayout.Pane(_usedByView);

        // Four regions rather than two, because an auth answers four separate questions: how it is
        // set up, what it is holding, which secrets it needs, and what breaks if it goes away. The
        // last is the one worth having in front of you before pressing `d`.
        _sections = ResourceScreenLayout.FourPaneSections(_splits,
            ("Configuration", configurationPane),
            ("Credential", credentialPane),
            ("Secrets", secretsPane),
            ("Used by", usedByPane));
        // Fetching is the screen's action, not the list's: a reader looking at a token's state wants
        // to renew it from where they are. The command goes on the container the detail regions
        // share, as `s` does on Requests, and not on `Root`, which the filter's text field is under.
        _sections.AddCommand(FetchCommand());
        _sections.AddCommand(CancelFetchCommand());

        Visual listView = ResourceScreenLayout.Scrollable(_list);
        Root = ResourceScreenLayout.Create("Auths",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter,
            _splits,
            () => _loading.Value
                ? ResourceScreenLayout.Message(new HStack(new Spinner(),
                    new TextBlock("Loading auths…").Style(StraumrStyles.MutedText)).Spacing(1))
                : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayout.EmptySections() : _sections);

        // Every action the screen offers under a key is here under a name as well, because the
        // prompt is how the other screens reach this one: `:au edit <auth>` from Requests is the
        // same edit `e` is on the row. Each takes the auth to act on by name, and acts on the
        // selection when given none, which is what the key does.
        PromptCommands =
        [
            new TuiCommand("select", SelectAuthAsync) { Aliases = ["a"], ArgumentValues = AuthNames },
            new TuiCommand("create", CreateAuthAsync) { Aliases = ["new"] },
            new TuiCommand("edit", EditAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommand("copy", CopyAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommand("delete", DeleteAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommand("fetch", FetchAuthAsync) { ArgumentValues = AuthNames },
            new TuiCommand("json", EditJsonAsync) { ArgumentValues = AuthNames },
            new TuiCommand("refresh", RefreshAsync)
        ];
    }

    public TuiScreen Kind => TuiScreen.Auths;

    public Visual Root { get; }

    public Visual FocusTarget => _list;

    public string? ActiveWorkspaceName { get; private set; }

    public IReadOnlyList<TuiCommand> PromptCommands { get; }

    public event Action<TuiCommandResult>? NotificationRequested;

    public event Action<TuiExternalAction>? ExternalActionRequested;

    public event Action? TransientScreenOpened;

    public event Action? TransientScreenClosed;

    private AuthScreenItem? SelectedItem =>
        (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Guid? selected = SelectedItem?.Id;
        Guid? previousWorkspace = _workspace?.Id;
        _loading.Value = true;
        _loadError.Value = false;
        _displayedId = null;
        _references.Value = null;
        _usedBy.Value = [];
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
                await _workspaces.GetAsync(_workspace.Id, updateLastAccessed: false, cancellationToken);
            if (workspace.Id != _workspace.Id)
                throw new StraumrException(
                    "The active workspace has a mismatched ID; repair it in Workspaces.", StraumrError.CorruptEntry);
            ActiveWorkspaceName = workspace.Name;
            foreach (Guid id in workspace.Auths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(Path.GetDirectoryName(_workspace.Path)!, $"{id}.jsonc");
                if (!File.Exists(path))
                    continue;
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

    /// <remarks>
    /// One read per entry rather than one <c>ListAsync</c>, so a file that does not parse scopes its
    /// failure to its own row instead of taking the screen to its error state.
    /// </remarks>
    private async Task<AuthScreenItem> LoadItemAsync(Guid id, string path, CancellationToken cancellationToken)
    {
        try
        {
            StraumrAuth auth = await _auths.GetAsync(_workspace!, id, updateLastAccessed: false, cancellationToken);
            string? problem = Validate(auth, id);
            return new AuthScreenItem(id, path, problem is null ? auth : null, problem);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return new AuthScreenItem(id, path, null, exception.Message);
        }
    }

    /// <remarks>
    /// Unreadable requests scope their failure to the Used by region, which then says what it could
    /// read. A workspace with one broken request is still one whose auths can be edited.
    /// </remarks>
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

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
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
                NotificationRequested?.Invoke(
                    TuiCommandResult.Failed($"cannot save pane layout: {exception.Message}"));
            }
        }

        if (_pendingFetchId is { } fetchId)
        {
            _pendingFetchId = null;
            await FetchAsync(fetchId, cancellationToken);
        }

        AuthScreenItem? item = SelectedItem;
        if (item?.Id == _displayedId)
            return;

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

        IReadOnlyList<SecretReference> references = await SecretReferences.ResolveAsync(
            _secrets,
            [JsonSerializer.Serialize(auth, StraumrJsonContext.Default.StraumrAuth)],
            IsRecoverable,
            cancellationToken);
        if (SelectedItem?.Id == item.Id)
            _references.Value = references;
    }

    /// <remarks>
    /// The bar's right half is the identifier badge, and the pulse while a fetch is in flight — the
    /// same swap the full-screen response makes, one screen down. The bar keeps its three rows either
    /// way, so a fetch landing changes what it says rather than how tall it is.
    /// </remarks>
    private Visual BuildHead()
    {
        if (SelectedItem is not { } item)
            return new TextBlock("No auth selected.").Style(StraumrStyles.MutedText)
                .Trimming(TextTrimming.EndEllipsis);

        Visual summary = item.Auth is { } auth
            ? new HStack(
                    new TextBlock(SecretFormatting.Display(auth.Name)).Style(StraumrStyles.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis),
                    new TextBlock(AuthFormatting.TypeName(auth.Config)).Style(StraumrStyles.AccentText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .HorizontalAlignment(Align.Stretch))
                .Spacing(2)
                .HorizontalAlignment(Align.Stretch)
            : new TextBlock($"{item.Name} · cannot be read").Style(StraumrStyles.RedText)
                .Trimming(TextTrimming.EndEllipsis);

        Visual badge = new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyles.TokenChip);
        return StraumrSurfaces.Bar(summary, new ComputedVisual(() => _fetching.Value
            ? InFlightPulse.Create("FETCHING", _fetchClock)
            : badge));
    }

    /// <summary>How the auth is set up: the fields that decide what it does, credentials excluded.</summary>
    private Visual BuildConfiguration()
    {
        if (SelectedItem is not { } item)
            return Unavailable();
        if (item.Auth is not { } auth)
            return new VStack(
                    FieldList.Create(
                        ("Problem", FieldList.Problem(item.Problem ?? "the file is not a valid auth")),
                        ("Path", FieldList.Wrapped(PathFormatting.Display(item.Path)))),
                    new TextBlock("Press e to open the file in your editor and repair it.")
                        .Style(StraumrStyles.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch))
                .Spacing(1)
                .HorizontalAlignment(Align.Stretch);

        var rows = new List<(string Label, Visual Value)>();
        switch (auth.Config)
        {
            case BearerAuthConfig bearer:
                rows.Add(("Prefix", Value(bearer.Prefix)));
                break;
            case BasicAuthConfig basic:
                rows.Add(("Username", Value(basic.Username)));
                break;
            case OAuth2Config oauth:
                rows.Add(("Grant", FieldList.Text(AuthEditingHelpers.GrantDisplayName(oauth.GrantType))));
                rows.Add(("Token URL", Value(oauth.TokenUrl)));
                rows.Add(("Client ID", Value(oauth.ClientId)));
                rows.Add(("Client secret", Credential(oauth.ClientSecret)));
                rows.Add(("Scope", Value(oauth.Scope)));
                if (oauth.GrantType == OAuth2GrantType.AuthorizationCode)
                {
                    rows.Add(("Authorization URL", Value(oauth.AuthorizationUrl)));
                    rows.Add(("Redirect URI", Value(oauth.RedirectUri)));
                    rows.Add(("PKCE", oauth.UsePkce
                        ? FieldList.Styled(oauth.CodeChallengeMethod, StraumrStyles.AmberText)
                        : FieldList.Styled("off", StraumrStyles.MutedText)));
                }

                if (oauth.GrantType == OAuth2GrantType.ResourceOwnerPassword)
                {
                    rows.Add(("Username", Value(oauth.Username)));
                    rows.Add(("Password", Credential(oauth.Password)));
                }

                break;
            case CustomAuthConfig custom:
                rows.Add(("Request", Value($"{custom.Method} {custom.Url}".Trim())));
                rows.Add(("Headers", FieldList.Count(custom.Headers.Count)));
                rows.Add(("Params", FieldList.Count(custom.Params.Count)));
                rows.Add(("Body", FieldList.Text(RequestEditingHelpers.BodyTypeDisplayName(custom.BodyType))));
                rows.Add(("Source", FieldList.Text(AuthEditingHelpers.ExtractionSourceDisplayName(custom.Source))));
                rows.Add(("Expression", Value(custom.ExtractionExpression)));
                break;
        }

        return FieldList.Create(rows.ToArray());
    }

    /// <summary>
    /// What the auth is holding and what happens on the next send. It says nothing about the
    /// credential material itself: that a token is not printed on screen needs no line of its own,
    /// which is the rule the Requests screen's Authentication pane already follows.
    /// </summary>
    private Visual BuildCredential()
    {
        if (SelectedItem is not { } item)
            return Unavailable();
        if (item.Auth is not { } auth)
            return new TextBlock("Unavailable until the file is valid.")
                .Style(StraumrStyles.MutedText).Wrap(true);

        AuthStatus status = AuthFormatting.Status(auth);
        var rows = new List<(string Label, Visual Value)>
        {
            ("Status", FieldList.Styled(status.Text, status.Style))
        };

        switch (auth.Config)
        {
            // A credential that is a secret reference is named, because the name is what says where
            // the value comes from and is the half a reader can act on. A literal one is not shown.
            case BearerAuthConfig bearer when IsReference(bearer.Token):
                rows.Add(("Token", FieldList.Wrapped(SecretFormatting.Display(bearer.Token))));
                break;
            case BasicAuthConfig basic when IsReference(basic.Password):
                rows.Add(("Password", FieldList.Wrapped(SecretFormatting.Display(basic.Password))));
                break;
            case OAuth2Config { Token: { } token }:
                rows.Add(("Token type", FieldList.Text(token.TokenType)));
                rows.Add(("Expires", token.ExpiresAt is { } expiry
                    ? FieldList.Styled(TimestampFormatting.Absolute(expiry),
                        token.IsExpired ? StraumrStyles.RedText : StraumrStyles.AmberText)
                    : FieldList.Styled("no expiry", StraumrStyles.MutedText)));
                rows.Add(("Refresh token", token.RefreshToken is null
                    ? FieldList.Styled("none", StraumrStyles.MutedText)
                    : FieldList.Styled("available", StraumrStyles.AmberText)));
                break;
            case CustomAuthConfig custom:
                rows.Add(("Template", FieldList.Wrapped(custom.ApplyHeaderTemplate)));
                break;
        }

        rows.Add(("Injects", FieldList.Text(AuthFormatting.Injects(auth.Config))));
        if (AuthEditingHelpers.SupportsFetch(auth.Config))
            rows.Add(("Auto-renew", auth.AutoRenewAuth
                ? FieldList.Styled("on", StraumrStyles.AmberText)
                : FieldList.Styled("off", StraumrStyles.MutedText)));

        return FieldList.Create(rows.ToArray());
    }

    private Visual BuildSecrets()
    {
        if (SelectedItem is null)
            return Unavailable();
        if (_references.Value is not { } references)
            return new TextBlock("Loading…").Style(StraumrStyles.MutedText);
        return SecretList.Create(references);
    }

    /// <summary>
    /// The requests that send with this auth. It is what answers whether deleting it breaks anything,
    /// and it is the reciprocal of the Requests screen naming this auth in its Authentication pane.
    /// </summary>
    private Visual BuildUsedBy()
    {
        if (SelectedItem is null)
            return Unavailable();

        IReadOnlyList<StraumrRequest> requests = _usedBy.Value;
        if (requests.Count == 0)
            return new TextBlock("No requests use this auth.").Style(StraumrStyles.MutedText).Wrap(true);

        return new VStack(requests
                .Select(request => (Visual)new HStack(
                        new TextBlock(request.Method.Method)
                            .Style(HttpMethodFormatting.Style(request.Method))
                            .MinWidth(request.Method.Method.Length),
                        new TextBlock(SecretFormatting.Display(request.Name))
                            .Style(StraumrStyles.PrimaryText)
                            .Trimming(TextTrimming.EndEllipsis)
                            .HorizontalAlignment(Align.Stretch))
                    .Spacing(1)
                    .HorizontalAlignment(Align.Stretch))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }

    private static Visual Unavailable() =>
        new TextBlock("Unavailable.").Style(StraumrStyles.MutedText);

    /// <summary>A configured value, or the inert word for one that has not been given yet.</summary>
    private static Visual Value(string value) =>
        value.Length == 0
            ? FieldList.Styled("not set", StraumrStyles.MutedText)
            : FieldList.Wrapped(SecretFormatting.Display(value));

    /// <summary>
    /// A value that is credential material. A secret reference is named — that is the point of using
    /// one, and the name is not the secret — and anything else is reported as present or absent
    /// without being shown.
    /// </summary>
    private static Visual Credential(string value) =>
        IsReference(value) ? FieldList.Wrapped(SecretFormatting.Display(value)) :
        value.Length == 0 ? FieldList.Styled("not set", StraumrStyles.MutedText) :
        FieldList.Styled("set", StraumrStyles.AmberText);

    private static bool IsReference(string value) => SecretHelpers.SecretPattern.IsMatch(value);

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
        _list.SetRows(_visible.Value.Select(item => new ResourceRow(
            SecretFormatting.Display(item.Name),
            Meta: item.Auth is { } auth ? AuthFormatting.Meta(auth.Config) : "cannot be read",
            HasContent: item.Auth is { } held && AuthFormatting.HasCredential(held.Config),
            IsBroken: item.IsBroken)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value && _workspace is not null)
            _emptyMessage.Value = _query.Value.Length > 0
                ? "No auths match this filter."
                : "No auths in this workspace. Press c to create one.";
    }

    private IEnumerable<string> AuthNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResult> SelectAuthAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
            return Task.FromResult(TuiCommandResult.NoWorkspace);
        if (!TuiCommandArguments.TryParseSingle(argument, out string name, out string? error))
            return Task.FromResult(TuiCommandResult.Failed(error!));
        if (name.Length == 0)
            return Task.FromResult(TuiCommandResult.Failed("usage: select <name>"));
        return Task.FromResult(SelectAuth(name));
    }

    /// <summary>
    /// Runs a command against the auth it names, or against the selection when it names none,
    /// having first said why it cannot run at all. Every command that acts on one auth goes through
    /// here, so they take their argument, report a missing workspace and report a name that matches
    /// nothing or too much in the same words.
    /// </summary>
    private TuiCommandResult OnSelected(string argument, Func<AuthScreenItem, TuiCommandResult> action)
    {
        if (_workspace is null)
            return TuiCommandResult.NoWorkspace;
        if (!TuiCommandArguments.TryParseSingle(argument, out string name, out string? error))
            return TuiCommandResult.Failed(error!);
        if (name.Length > 0)
        {
            TuiCommandResult selection = SelectAuth(name);
            if (selection.IsError)
                return selection;
        }

        return SelectedItem is { } item ? action(item) : TuiCommandResult.Failed("no auth selected");
    }

    private Task<TuiCommandResult> CreateAuthAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
            return Task.FromResult(TuiCommandResult.NoWorkspace);
        return Task.FromResult(argument.Length > 0
            ? TuiCommandResult.Failed("usage: create")
            : OpenEditorFor(null, 'c'));
    }

    private Task<TuiCommandResult> EditAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? EditAsJson(item)
            : OpenEditorFor(item, 'e')));

    private Task<TuiCommandResult> CopyAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item => item.IsBroken
            ? TuiCommandResult.Failed($"cannot copy {item.Name}: the auth cannot be read")
            : OpenEditorFor(item, 'y')));

    private Task<TuiCommandResult> DeleteAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ =>
        {
            ShowDeleteDialog();
            return TuiCommandResult.None;
        }));

    /// <remarks>
    /// The guard the keystroke can leave unsaid: a key pressed while the editor is up never reaches
    /// the screen behind it, but a command dispatched from another screen arrives without that.
    /// </remarks>
    private TuiCommandResult OpenEditorFor(AuthScreenItem? source, char openingGesture)
    {
        if (_editorView is not null)
            return TuiCommandResult.Failed("the auth editor is already open");
        OpenEditor(source, openingGesture);
        return TuiCommandResult.None;
    }

    private TuiCommandResult SelectAuth(string name)
    {
        List<AuthScreenItem> matches = _items.FindAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count != 1)
            return TuiCommandResult.Failed(matches.Count == 0
                ? $"no auth matches {name}"
                : $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResult.None;
    }

    private async Task<TuiCommandResult> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
            return TuiCommandResult.Failed("usage: refresh");
        if (_workspace is null)
            return TuiCommandResult.NoWorkspace;
        await LoadAsync(cancellationToken);
        return _loadError.Value
            ? TuiCommandResult.Failed(_emptyMessage.Value)
            : TuiCommandResult.Ok($"reloaded {CountFormatting.Label(_items.Count, "auth")}");
    }

    private Task<TuiCommandResult> FetchAuthAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ => QueueFetch()));

    private Task<TuiCommandResult> EditJsonAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, EditAsJson));

    /// <summary>
    /// Goes and gets what the auth is for: an OAuth2 token or a custom auth's extracted value. The
    /// result is saved, because an auth holds its token — that is the difference between fetching
    /// here and a send fetching one for itself.
    /// </summary>
    private TuiCommandResult QueueFetch()
    {
        if (_workspace is null)
            return TuiCommandResult.NoWorkspace;
        if (SelectedItem is not { Auth: { } auth } item)
            return TuiCommandResult.Failed("no auth selected");
        if (!AuthEditingHelpers.SupportsFetch(auth.Config))
            return TuiCommandResult.Failed($"{auth.Name} holds its credential; there is nothing to fetch");
        if (_fetchCancellation is not null)
            return TuiCommandResult.Failed("an auth is already being fetched");

        _fetchCancellation = new CancellationTokenSource();
        _fetchClock.Restart();
        _fetching.Value = true;
        _pendingFetchId = item.Id;
        return TuiCommandResult.None;
    }

    private async Task FetchAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace || _fetchCancellation is null)
            return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _fetchCancellation.Token);
        try
        {
            StraumrAuth auth = await _auths.GetAsync(workspace, id, updateLastAccessed: false, linked.Token);
            string report;
            switch (auth.Config)
            {
                case OAuth2Config oauth:
                    OAuth2Token token = await _auths.FetchTokenAsync(oauth, linked.Token);
                    oauth.Token = token;
                    report = token.ExpiresAt is { } expiry
                        ? $"fetched token for {auth.Name}; expires {TimestampFormatting.Absolute(expiry)}"
                        : $"fetched token for {auth.Name}";
                    break;
                case CustomAuthConfig custom:
                    await _auths.ExecuteCustomAuthAsync(custom, linked.Token);
                    report = $"fetched value for {auth.Name}";
                    break;
                default:
                    return;
            }

            await _auths.SaveAsync(workspace, auth, linked.Token);
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, id);
            NotificationRequested?.Invoke(TuiCommandResult.Ok(report));
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && _fetchCancellation.IsCancellationRequested)
        {
            NotificationRequested?.Invoke(TuiCommandResult.Failed("fetch cancelled"));
        }
        catch (Exception exception) when (IsRecoverable(exception) ||
                                          exception is HttpRequestException or UriFormatException
                                              or InvalidOperationException)
        {
            NotificationRequested?.Invoke(TuiCommandResult.Failed($"fetch failed: {exception.Message}"));
        }
        finally
        {
            _fetchClock.Stop();
            _fetching.Value = false;
            _fetchCancellation.Dispose();
            _fetchCancellation = null;
        }
    }

    /// <summary>
    /// Opens the auth editor: on nothing for a new auth, on the selection for a change or a copy. A
    /// copy opens with its source's name cleared rather than pre-filled, because the one thing a copy
    /// must be given is a name of its own.
    /// </summary>
    private void OpenEditor(AuthScreenItem? source, char openingGesture)
    {
        if (_workspace is null || _editorView is not null)
            return;

        bool isNew = source is null || openingGesture == 'y';
        StraumrAuth state = source?.Auth is { } auth
            ? isNew ? auth.CopyAs(string.Empty) : Working(auth)
            : new StraumrAuth
            {
                Name = string.Empty,
                Config = AuthEditingHelpers.CreateConfig(AuthType.Bearer)
            };

        _editingId = isNew ? null : source!.Id;
        // A copy is the one opening that clears the name it was given, so it is the one that has to
        // keep saying which name that was.
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new AuthEditor(state, ActiveWorkspaceName, isNew, openingGesture,
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

    /// <summary>
    /// A working copy of an auth, identity included. The editor writes into whatever it is handed, and
    /// what the list is holding is what the detail regions are drawing from: editing that directly
    /// would show every keystroke behind the form and survive a cancel.
    /// </summary>
    private static StraumrAuth Working(StraumrAuth auth)
    {
        StraumrAuth copy = auth.CopyAs(auth.Name);
        copy.Id = auth.Id;
        copy.Modified = auth.Modified;
        copy.LastAccessed = auth.LastAccessed;
        return copy;
    }

    /// <remarks>
    /// Create and save are one path with one difference, which is whether Core is being given an auth
    /// it has never seen. An edit applies onto the auth as loaded, so anything the form does not show
    /// — its access times — survives being edited by a form that never mentions it.
    /// </remarks>
    private async Task<TuiCommandResult> SaveEditAsync(StraumrAuth state, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
            return TuiCommandResult.Failed("no active workspace");

        try
        {
            StraumrAuth saved;
            bool created = _editingId is null;
            if (_editingId is { } id)
            {
                StraumrAuth existing = await _auths.GetAsync(workspace, id, updateLastAccessed: false, cancellationToken);
                existing.Name = state.Name;
                existing.Config = state.Config;
                existing.AutoRenewAuth = state.AutoRenewAuth;
                saved = await _auths.SaveAsync(workspace, existing, cancellationToken);
            }
            else
            {
                saved = await _auths.CreateAsync(workspace, state, cancellationToken);
                // The editor is still open on it, so from here it is an auth that exists.
                _editingId = saved.Id;
            }

            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, saved.Id);
            return TuiCommandResult.Ok(created ? $"created auth {saved.Name}" : $"updated auth {saved.Name}");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResult.Failed(exception.Message);
        }
    }

    /// <summary>
    /// Writes a custom auth's body in the reader's own editor. The editor screen is put down first and
    /// picked up again afterwards, because the other program needs the terminal this one is drawing on.
    /// </summary>
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

    /// <summary>
    /// An auth that parses is edited in the form; one that does not cannot be loaded into fields at
    /// all, so the same key opens the text that needs repairing.
    /// </summary>
    private void AuthEdit()
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
    private TuiCommandResult EditAsJson(AuthScreenItem item)
    {
        if (_workspace is not { } workspace)
            return TuiCommandResult.NoWorkspace;
        if (!_editor.IsConfigured)
            return TuiCommandResult.Failed("edit failed: no default editor is configured");

        ExternalActionRequested?.Invoke(new TuiExternalAction(token => EditAsync(workspace, item, token), _list));
        return TuiCommandResult.None;
    }

    private async Task<TuiCommandResult> EditAsync(
        StraumrWorkspaceEntry workspace, AuthScreenItem item, CancellationToken cancellationToken)
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
                await File.WriteAllTextAsync(item.Path, edited, cancellationToken);
            else
            {
                _files.CarryCommentsFrom(item.Path, edited);
                await _auths.SaveAsync(workspace, auth!, cancellationToken);
            }
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, item.Id);
            return problem is null
                ? TuiCommandResult.Ok($"updated auth {auth!.Name}")
                : TuiCommandResult.Failed($"saved the edit, but {problem}; press e to repair it");
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
        {
            return TuiCommandResult.Failed($"edit failed: {exception.Message}");
        }
    }

    /// <remarks>
    /// The confirmation names what depends on the auth rather than asking a bare yes or no. A request
    /// pointed at an auth that no longer exists fails on the next send, and this screen is the only
    /// place that knows which requests those are.
    /// </remarks>
    private void ShowDeleteDialog()
    {
        if (SelectedItem is not { } item)
            return;

        // Counted from the workspace's requests rather than from the region showing them, which is
        // written on the update pass and would be one selection behind a fast cursor.
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
            destructive: true,
            () => _pendingDeleteId = item.Id).Show();
    }

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
            await _auths.DeleteAsync(workspace, id, cancellationToken);
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, null);
            // The row below the one deleted takes its place, as it does in every list in this app;
            // on the last row that is the row above.
            if (_visible.Value.Count > 0)
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            NotificationRequested?.Invoke(TuiCommandResult.Ok($"deleted auth {item.Name}"));
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

    private static string? Validate(StraumrAuth? auth, Guid id) => auth switch
    {
        null => "the file is not a valid auth",
        { } when auth.Id != id => "the auth ID does not match its file",
        { } when string.IsNullOrWhiteSpace(auth.Name) => "the auth name is missing",
        { } when auth.Name.Contains('"') => "the auth name contains a double quote",
        { Config: null } => "the auth has no configuration",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    /// <summary>
    /// A fresh <c>f</c> for each visual that offers it. One <see cref="Command"/> instance belongs to
    /// the visual it is added to, so the list and the detail regions each get their own.
    /// </summary>
    private Command FetchCommand() =>
        ActionCommand("Fetch", 'f', () => NotifyIfFailed(QueueFetch()),
            () => !_fetching.Value && SelectedItem is { Auth: { } auth } &&
                  AuthEditingHelpers.SupportsFetch(auth.Config));

    /// <remarks>
    /// <c>Escape</c> only while a fetch is in flight, and only on the surfaces that offer the fetch:
    /// on the screen root it would take the key the filter closes itself with, because a command on an
    /// ancestor runs before the focused control sees it.
    /// </remarks>
    private Command CancelFetchCommand() =>
        ActionCommandKey("Cancel", new KeyGesture(TerminalKey.Escape), () => _fetchCancellation?.Cancel(),
            () => _fetching.Value);

    private void NotifyIfFailed(TuiCommandResult result)
    {
        if (result.IsError)
            NotificationRequested?.Invoke(result);
    }

    /// <summary>
    /// A <c>Ctrl</c>-plus-letter action, for something that has to stay reachable without taking one
    /// of the plain letters the screen's own actions use.
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

    private static Command ActionCommand(string label, char key, Action execute, Func<bool> available,
        CommandPresentation presentation = CommandPresentation.CommandBar) =>
        ActionCommandKey(label, new KeyGesture(key), execute, available, presentation);

    private static Command ActionCommandKey(string label, KeyGesture gesture, Action execute,
        Func<bool> available, CommandPresentation presentation = CommandPresentation.CommandBar) => new()
    {
        Id = $"Auth.{label}", LabelMarkup = label, Gesture = gesture,
        Importance = CommandImportance.Primary, Presentation = presentation,
        IsVisible = _ => available(), CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false, Execute = _ => execute()
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
