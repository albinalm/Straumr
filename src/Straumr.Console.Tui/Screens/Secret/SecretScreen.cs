using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Configuration;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Secret;

public sealed class SecretScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Secrets);
    private readonly IStraumrStateService _state;
    private readonly IStraumrWorkspaceService _workspaces;
    private readonly IStraumrRequestService _requests;
    private readonly IStraumrAuthService _auths;
    private readonly IStraumrSecretService _secrets;
    private readonly IStraumrFileService _files;
    private readonly ExternalEditor _editor;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<int> _count = new(0);
    private readonly State<int> _matchCount = new(0);
    private readonly State<string> _query = new(string.Empty);
    private readonly State<string> _emptyMessage = new("Loading secrets…");
    private readonly State<bool> _loading = new(true);
    private readonly State<bool> _loadError = new(false);
    private readonly State<KnownSecretReferences> _references = new(new KnownSecretReferences());
    private readonly State<List<SecretScreenItem>> _visible = new([]);
    private readonly ResourceList _list;
    private readonly ResourceFilter _filter;
    private readonly PaneSplits _splits;
    private readonly ScrollableContent _secretView;
    private readonly ScrollableContent _referencesView;
    private List<SecretScreenItem> _items = [];
    private Guid? _displayedId;
    private Guid? _pendingDeleteId;
    private Guid? _editingId;
    private SecretEditor? _editorView;
    private Func<CancellationToken, Task<TuiCommandResult>>? _pendingSave;
    private bool _savePaneLayout;

    public SecretScreen(IStraumrStateService state, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, IStraumrSecretService secrets,
        IStraumrFileService files, ExternalEditor editor)
    {
        (_state, _workspaces, _requests, _auths, _secrets, _files, _editor) =
            (state, workspaces, requests, auths, secrets, files, editor);
        var layout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey) ?? new StraumrPaneLayout();
        _splits = new PaneSplits(layout.Panels, layout.Sections, layout.Stack);
        _splits.Changed += QueuePaneLayoutSave;

        _list = new ResourceList([], ResourceScreenLayout.Message(
                new TextBlock(() => _emptyMessage.Value)
                    .Style(() => _loadError.Value ? StraumrStyles.RedText : StraumrStyles.MutedText)
                    .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            activateLabel: $"{StraumrStyles.KeyMarkup("e")} Edit");
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => EditSelected();
        _filter = new ResourceFilter("filter secrets", ApplyFilter, () => _list);
        _list.AddCommand(ActionCommand("New", 'c', () => OpenEditor(null, 'c'), () => !_loadError.Value));
        _list.AddCommand(ActionCommand("Edit", 'e', () => EditSelected('e'), () => SelectedItem is not null,
            CommandPresentation.None));
        _list.AddCommand(ActionCommand("Copy", 'y', () => OpenEditor(SelectedItem, 'y'),
            () => SelectedItem is { IsBroken: false }));
        _list.AddCommand(ActionCommand("Delete", 'd', ShowDeleteDialog, () => SelectedItem is not null));
        _list.AddCommand(JsonCommand((char)('E' & 0x1F), CommandPresentation.CommandBar));
        _list.AddCommand(JsonCommand('e', CommandPresentation.None));

        _secretView = new ScrollableContent(new ComputedVisual(BuildSecret));
        _referencesView = new ScrollableContent(new ComputedVisual(BuildReferences));
        Visual sections = ResourceScreenLayout.TwoPaneSections(_splits,
            "Secret", ResourceScreenLayout.Pane(_secretView),
            "Known references", ResourceScreenLayout.Pane(_referencesView));
        Visual listView = ResourceScreenLayout.Scrollable(_list);
        Root = ResourceScreenLayout.Create("Secrets",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter, _splits,
            () => _loading.Value
                ? ResourceScreenLayout.Message(new HStack(new Spinner(),
                    new TextBlock("Loading secrets…").Style(StraumrStyles.MutedText)).Spacing(1))
                : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayout.EmptySections() : sections);

        PromptCommands =
        [
            new TuiCommand("select", SelectSecretAsync) { Aliases = ["s"], ArgumentValues = SecretNames },
            new TuiCommand("create", CreateSecretAsync) { Aliases = ["new"] },
            new TuiCommand("edit", (argument, _) => Task.FromResult(OnSelected(argument,
                item => item.IsBroken ? EditAsJson(item) : OpenEditorFor(item, 'e')))) { ArgumentValues = SecretNames },
            new TuiCommand("copy", (argument, _) => Task.FromResult(OnSelected(argument,
                item => item.IsBroken ? TuiCommandResult.Failed($"cannot copy {item.Name}: the secret cannot be read")
                    : OpenEditorFor(item, 'y')))) { ArgumentValues = SecretNames },
            new TuiCommand("delete", (argument, _) => Task.FromResult(OnSelected(argument, _ =>
                { ShowDeleteDialog(); return TuiCommandResult.None; }))) { ArgumentValues = SecretNames },
            new TuiCommand("json", (argument, _) => Task.FromResult(OnSelected(argument, EditAsJson)))
                { ArgumentValues = SecretNames },
            new TuiCommand("refresh", RefreshAsync)
        ];
    }

    public TuiScreen Kind => TuiScreen.Secrets;
    public Visual Root { get; }
    public Visual FocusTarget => _list;
    public string? ActiveWorkspaceName { get; private set; }
    public IReadOnlyList<TuiCommand> PromptCommands { get; }
    public event Action<TuiCommandResult>? NotificationRequested;
    public event Action<TuiExternalAction>? ExternalActionRequested;
    public event Action? TransientScreenOpened;
    public event Action? TransientScreenClosed;

    private SecretScreenItem? SelectedItem =>
        (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Guid? selected = SelectedItem?.Id;
        _loading.Value = true;
        _loadError.Value = false;
        _displayedId = null;
        ActiveWorkspaceName = null;
        _items = [];
        _references.Value = new KnownSecretReferences();
        try
        {
            await _state.LoadAsync(cancellationToken);
            // Workspace context is only a header on this screen. A missing or broken active
            // workspace must not prevent access to the global secret store.
            if (_state.State.CurrentWorkspace is { } active)
            {
                try
                {
                    var workspace = await _workspaces.GetAsync(active.Id, updateLastAccessed: false, cancellationToken);
                    if (workspace.Id == active.Id)
                        ActiveWorkspaceName = workspace.Name;
                }
                catch (Exception exception) when (IsRecoverable(exception)) { }
            }

            foreach (var entry in _state.State.Secrets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var secret = await _secrets.GetAsync(entry.Id, updateLastAccessed: false, cancellationToken);
                    string? problem = Validate(secret, entry.Id);
                    _items.Add(new SecretScreenItem(entry.Id, entry.Path, problem is null ? secret : null, problem));
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    // Keep missing entries as well as corrupt files: the registry is also what
                    // makes it possible to delete a secret whose file has disappeared.
                    _items.Add(new SecretScreenItem(entry.Id, entry.Path, null, exception.Message));
                }
            }

            _items = _items.OrderByDescending(item => item.Secret?.LastAccessed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _references.Value = await KnownSecretReferences.LoadAsync(_state.State.Workspaces,
                _workspaces, _requests, _auths, cancellationToken);
            ApplyFilter(_filter.Text, selected);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            _items = [];
            _loadError.Value = true;
            _emptyMessage.Value = $"Cannot load secrets: {exception.Message}\nUse :refresh to retry.";
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
        await DeletePendingAsync(cancellationToken);
        if (_pendingSave is { } save)
        {
            _pendingSave = null;
            TuiCommandResult result = await save(cancellationToken);
            if (result.IsError)
                _editorView?.Failed(result.Message!);
            else if (_editorView is { } editor)
            {
                editor.Saved();
                editor.Report(result.Message!, error: false);
            }
        }

        if (_savePaneLayout)
        {
            _savePaneLayout = false;
            try { await _state.SaveAsync(cancellationToken); }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                NotificationRequested?.Invoke(TuiCommandResult.Failed($"cannot save pane layout: {exception.Message}"));
            }
        }

        if (SelectedItem?.Id != _displayedId)
        {
            _displayedId = SelectedItem?.Id;
            _secretView.ScrollOffset = 0;
            _referencesView.ScrollOffset = 0;
        }
    }

    private Visual BuildHead()
    {
        if (SelectedItem is not { } item)
            return new TextBlock("No secret selected.").Style(StraumrStyles.MutedText)
                .Trimming(TextTrimming.EndEllipsis);
        Visual summary = item.IsBroken
            ? new TextBlock($"{item.Name} · cannot be read").Style(StraumrStyles.RedText)
                .Trimming(TextTrimming.EndEllipsis)
            : new HStack(
                    new TextBlock(SecretFormatting.Display(item.Name)).Style(StraumrStyles.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis),
                    new TextBlock("Global secret").Style(StraumrStyles.MutedText)
                        .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch))
                .Spacing(2).HorizontalAlignment(Align.Stretch);
        return StraumrSurfaces.Bar(summary,
            new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyles.TokenChip));
    }

    private Visual BuildSecret()
    {
        if (SelectedItem is not { } item)
            return Message("Unavailable.");
        if (item.Secret is not { } secret)
            return new VStack(
                FieldList.Create(("Problem", FieldList.Problem(item.Problem ?? "the file is not a valid secret")),
                    ("Storage", FieldList.Wrapped(PathFormatting.Display(item.Path)))),
                Message("Press e on the row to repair it in your editor, or d to remove the entry."))
                .Spacing(1).HorizontalAlignment(Align.Stretch);
        return FieldList.Create(
            // A fixed mask says nothing about the value or its length.
            ("Value", FieldList.Styled(secret.Value.Length == 0 ? "not set" : "••••••••••••",
                secret.Value.Length == 0 ? StraumrStyles.MutedText : StraumrStyles.AmberText)),
            ("Modified", FieldList.Text(TimestampFormatting.Absolute(secret.Modified))),
            ("Last accessed", FieldList.Text(LastAccessed(secret))),
            ("Storage", FieldList.Wrapped(PathFormatting.Display(item.Path))));
    }

    private Visual BuildReferences()
    {
        if (SelectedItem is not { } item)
            return Message("Unavailable.");
        if (item.IsBroken)
            return Message("References cannot be matched until the secret's name can be read.");
        IReadOnlyList<SecretUsage> references = _references.Value.For(item.Name);
        var content = new List<Visual>();
        if (references.Count == 0)
            content.Add(Message("No known references in registered workspaces."));
        foreach (var reference in references)
            content.Add(new VStack(
                    new TextBlock(SecretFormatting.Display(reference.Resource)).Style(StraumrStyles.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch),
                    Message($"{reference.Workspace} · {reference.Kind} · {reference.Field}"))
                .HorizontalAlignment(Align.Stretch));
        if (_references.Value.Notice is { } notice)
            content.Add(new TextBlock(notice).Style(StraumrStyles.AmberText).Wrap(true)
                .HorizontalAlignment(Align.Stretch));
        content.Add(FieldList.Create(("Placeholder", FieldList.Wrapped("{{secret:" + item.Name + "}}"))));
        return new VStack(content.ToArray()).Spacing(1).HorizontalAlignment(Align.Stretch);
    }

    private static Visual Message(string text) =>
        new TextBlock(text).Style(StraumrStyles.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch);

    private static string LastAccessed(StraumrSecret secret) => secret.LastAccessed == DateTimeOffset.MinValue
        ? "never accessed" : TimestampFormatting.Relative(secret.LastAccessed);

    private void ApplyFilter(string query) => ApplyFilter(query, SelectedItem?.Id);

    private void ApplyFilter(string query, Guid? selected)
    {
        _query.Value = query.Trim();
        _count.Value = _items.Count;
        _visible.Value = _items.Where(item => item.Name.Contains(_query.Value, StringComparison.OrdinalIgnoreCase) ||
            item.Id.ToString().Contains(_query.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        _matchCount.Value = _visible.Value.Count;
        _list.SetRows(_visible.Value.Select(item => new ResourceRow(SecretFormatting.Display(item.Name),
            Meta: item.Secret is { } secret ? LastAccessed(secret) : "cannot be read",
            HasContent: item.Secret is { Value.Length: > 0 }, IsBroken: item.IsBroken)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value)
            _emptyMessage.Value = _query.Value.Length > 0
                ? "No secrets match this filter." : "No global secrets. Press c to create one.";
    }

    private IEnumerable<string> SecretNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResult> SelectSecretAsync(string argument, CancellationToken cancellationToken)
    {
        if (!TuiCommandArguments.TryParseSingle(argument, out string name, out string? error))
            return Task.FromResult(TuiCommandResult.Failed(error!));
        return Task.FromResult(name.Length == 0
            ? TuiCommandResult.Failed("usage: select <name or ID>") : SelectSecret(name));
    }

    private TuiCommandResult SelectSecret(string name)
    {
        List<SecretScreenItem> matches = _items.FindAll(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            item.Id.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                item.Id.ToString().StartsWith(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count != 1)
            return TuiCommandResult.Failed(matches.Count == 0 ? $"no secret matches {name}"
                : $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResult.None;
    }

    private TuiCommandResult OnSelected(string argument, Func<SecretScreenItem, TuiCommandResult> action)
    {
        if (_loadError.Value)
            return TuiCommandResult.Failed(_emptyMessage.Value);
        if (!TuiCommandArguments.TryParseSingle(argument, out string name, out string? error))
            return TuiCommandResult.Failed(error!);
        if (name.Length > 0)
        {
            TuiCommandResult selection = SelectSecret(name);
            if (selection.IsError) return selection;
        }
        return SelectedItem is { } item ? action(item) : TuiCommandResult.Failed("no secret selected");
    }

    private Task<TuiCommandResult> CreateSecretAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(_loadError.Value ? TuiCommandResult.Failed(_emptyMessage.Value) : argument.Length > 0
            ? TuiCommandResult.Failed("usage: create") : OpenEditorFor(null, 'c'));

    private async Task<TuiCommandResult> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0) return TuiCommandResult.Failed("usage: refresh");
        await LoadAsync(cancellationToken);
        return _loadError.Value ? TuiCommandResult.Failed(_emptyMessage.Value)
            : TuiCommandResult.Ok($"reloaded {CountFormatting.Label(_items.Count, "secret")}");
    }

    private TuiCommandResult OpenEditorFor(SecretScreenItem? source, char operation)
    {
        if (_editorView is not null) return TuiCommandResult.Failed("the secret editor is already open");
        OpenEditor(source, operation, fromCommand: true);
        return TuiCommandResult.None;
    }

    private void EditSelected(char? openingGesture = null)
    {
        if (SelectedItem is not { } item) return;
        if (item.IsBroken) NotifyIfFailed(EditAsJson(item));
        else OpenEditor(item, openingGesture ?? 'e', fromCommand: openingGesture is null);
    }

    private void OpenEditor(SecretScreenItem? source, char operation, bool fromCommand = false)
    {
        if (_editorView is not null) return;
        bool isNew = source is null || operation == 'y';
        var state = source?.Secret is { } secret
            ? secret.CopyAs(isNew ? string.Empty : secret.Name)
            : new StraumrSecret { Name = string.Empty, Value = string.Empty };
        _editingId = isNew ? null : source!.Id;
        // A copy is the one opening that clears the name it was given, so it is the one that has to
        // keep saying which name that was.
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new SecretEditor(state, ActiveWorkspaceName, isNew, fromCommand ? null : operation,
            () => _pendingSave = token => SaveEditAsync(state, token),
            () =>
            {
                _editorView = null;
                _editingId = null;
                _list.App?.Focus(_list);
                TransientScreenClosed?.Invoke();
            },
            sourceName);
        _editorView = editor;
        editor.Show();
        TransientScreenOpened?.Invoke();
    }

    private async Task<TuiCommandResult> SaveEditAsync(StraumrSecret state, CancellationToken cancellationToken)
    {
        try
        {
            bool created = _editingId is null;
            StraumrSecret saved;
            if (_editingId is { } id)
            {
                var existing = await _secrets.GetAsync(id, updateLastAccessed: false, cancellationToken);
                if (existing.Id != id)
                    return TuiCommandResult.Failed("the secret ID no longer matches its registry entry; close and refresh to repair it");
                existing.Name = state.Name;
                existing.Value = state.Value;
                saved = await _secrets.SaveAsync(existing, cancellationToken);
            }
            else
            {
                saved = await _secrets.CreateAsync(state, cancellationToken);
                _editingId = saved.Id;
            }
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, saved.Id);
            return TuiCommandResult.Ok(created ? $"created secret {saved.Name}" : $"updated secret {saved.Name}");
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResult.Failed(exception.Message);
        }
    }

    private TuiCommandResult EditAsJson(SecretScreenItem item)
    {
        if (!_editor.IsConfigured)
            return TuiCommandResult.Failed("edit failed: no default editor is configured");
        ExternalActionRequested?.Invoke(new TuiExternalAction(token => EditJsonAsync(item, token), _list));
        return TuiCommandResult.None;
    }

    private async Task<TuiCommandResult> EditJsonAsync(SecretScreenItem item, CancellationToken cancellationToken)
    {
        try
        {
            string original = File.Exists(item.Path) ? await File.ReadAllTextAsync(item.Path, cancellationToken)
                : JsonSerializer.Serialize(new StraumrSecret { Id = item.Id, Name = string.Empty, Value = string.Empty },
                    StraumrJsonContext.Default.StraumrSecret);
            string edited = await _editor.EditJsonAsync(original, cancellationToken);
            if (edited == original) return TuiCommandResult.None;
            StraumrSecret? secret = null;
            try { secret = JsonSerializer.Deserialize(edited, StraumrJsonContext.Default.StraumrSecret); }
            catch (JsonException) { }
            string? problem = Validate(secret, item.Id);
            if (problem is not null)
                return TuiCommandResult.Failed($"edit not saved: {problem}");
            // SaveAsync owns conflicts and timestamps. A missing registered file has to be
            // restored first; its registry entry already exists, so CreateAsync is not applicable.
            bool missing = !File.Exists(item.Path);
            if (missing)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Path)!);
                await File.WriteAllTextAsync(item.Path, original, cancellationToken);
            }
            _files.CarryCommentsFrom(item.Path, edited);
            try { await _secrets.SaveAsync(secret!, cancellationToken); }
            catch
            {
                if (missing && File.Exists(item.Path)) File.Delete(item.Path);
                throw;
            }
            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, item.Id);
            return TuiCommandResult.Ok($"updated secret {secret!.Name}");
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
        {
            return TuiCommandResult.Failed($"edit failed: {exception.Message}");
        }
    }

    private void ShowDeleteDialog()
    {
        if (SelectedItem is not { } item) return;
        int dependents = _references.Value.For(item.Name)
            .Select(reference => (reference.WorkspaceId, reference.ResourceId, reference.Kind)).Distinct().Count();
        string detail = "The global secret will be permanently deleted. References are not changed.";
        if (dependents > 0)
            detail += $" {CountFormatting.Label(dependents, "resource")} in registered workspaces still reference it.";
        if (item.IsBroken)
            detail += " Its name cannot be read, so its references cannot be counted.";
        if (_references.Value.Notice is { } notice)
            detail += $" {notice}";
        new ConfirmDialog("Delete secret", $"Delete {SecretFormatting.Display(item.Name)}?", detail,
            "Delete", destructive: true, () => _pendingDeleteId = item.Id).Show();
    }

    private async Task DeletePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } id) return;
        _pendingDeleteId = null;
        if (_items.Find(item => item.Id == id) is not { } item) return;
        int index = _selectedIndex.Value;
        try
        {
            await _secrets.DeleteAsync(id, cancellationToken);
            await LoadAsync(cancellationToken);
            if (_visible.Value.Count > 0)
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            NotificationRequested?.Invoke(TuiCommandResult.Ok($"deleted secret {item.Name}"));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            NotificationRequested?.Invoke(TuiCommandResult.Failed($"delete failed: {exception.Message}"));
        }
    }

    private static string? Validate(StraumrSecret? secret, Guid id) => secret switch
    {
        null => "the file is not a valid secret",
        { } when secret.Id != id => "the secret ID does not match its registry entry",
        { } when string.IsNullOrWhiteSpace(secret.Name) => "the secret name is missing",
        { } when secret.Name.Contains('"') => "the secret name contains a double quote",
        { Value: null } => "the secret has no value",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    private void NotifyIfFailed(TuiCommandResult result)
    {
        if (result.IsError) NotificationRequested?.Invoke(result);
    }

    private Command JsonCommand(char key, CommandPresentation presentation) => new()
    {
        Id = $"Secret.EditJson.{(int)key}", LabelMarkup = "Edit JSON",
        Gesture = new KeyGesture(key, TerminalModifiers.Ctrl),
        Importance = CommandImportance.Secondary, Presentation = presentation,
        IsVisible = _ => SelectedItem is not null, CanExecute = _ => SelectedItem is not null,
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => { if (SelectedItem is { } item) NotifyIfFailed(EditAsJson(item)); }
    };

    private static Command ActionCommand(string label, char key, Action execute, Func<bool> available,
        CommandPresentation presentation = CommandPresentation.CommandBar) => new()
    {
        Id = $"Secret.{label}", LabelMarkup = label, Gesture = new KeyGesture(key),
        Importance = CommandImportance.Primary, Presentation = presentation,
        IsVisible = _ => available(), CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false, Execute = _ => execute()
    };

    private void QueuePaneLayoutSave()
    {
        _state.State.PaneLayouts[PaneLayoutKey] = new StraumrPaneLayout
        {
            Panels = _splits.Panels.Share, Sections = _splits.Sections.Share, Stack = _splits.Stack.Share
        };
        _savePaneLayout = true;
    }
}
