using System.Text.Json;
using Straumr.Console.Tui.Screens.Components.Secret;
using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Configuration;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;
using ScrollableContent = Straumr.Console.Tui.Screens.Components.Shared.ScrollableContent;

namespace Straumr.Console.Tui.Screens;

public sealed class SecretScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Secrets);
    private readonly IStraumrAuthService _auths;
    private readonly State<int> _count = new(0);
    private readonly ExternalEditorService _editor;
    private readonly State<string> _emptyMessage = new("Loading secrets…");
    private readonly IStraumrFileService _files;
    private readonly ResourceFilter _filter;
    private readonly ResourceList _list;
    private readonly State<bool> _loadError = new(false);
    private readonly State<bool> _loading = new(true);
    private readonly State<int> _matchCount = new(0);
    private readonly State<string> _query = new(string.Empty);
    private readonly State<KnownReferenceService> _references = new(new KnownReferenceService());
    private readonly ScrollableContent _referencesView;
    private readonly IStraumrRequestService _requests;
    private readonly ScrollableContent _secretView;
    private readonly IStraumrSecretService _secrets;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly PaneSplits _splits;
    private readonly IStraumrStateService _state;
    private readonly State<List<SecretScreenItemModel>> _visible = new([]);
    private readonly IStraumrWorkspaceService _workspaces;
    private Guid? _displayedId;
    private Guid? _editingId;
    private SecretEditor? _editorView;
    private List<SecretScreenItemModel> _items = [];
    private Guid? _pendingDeleteId;
    private SecretJsonRenameModel? _pendingJsonRename;
    private Func<CancellationToken, Task<TuiCommandResultModel>>? _pendingNotify;
    private Func<CancellationToken, Task<TuiCommandResultModel?>>? _pendingSave;
    private bool _savePaneLayout;

    public SecretScreen(IStraumrStateService state, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, IStraumrSecretService secrets,
        IStraumrFileService files, ExternalEditorService editor)
    {
        (_state, _workspaces, _requests, _auths, _secrets, _files, _editor) =
            (state, workspaces, requests, auths, secrets, files, editor);
        StraumrPaneLayout layout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey) ?? new StraumrPaneLayout();
        _splits = new PaneSplits(layout.Panels, layout.Sections, layout.Stack);
        _splits.Changed += QueuePaneLayoutSave;

        _list = new ResourceList([], ResourceScreenLayoutHelpers.Message(
                new TextBlock(() => _emptyMessage.Value)
                    .Style(() => _loadError.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedText)
                    .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            TuiKeybindHelpers.CombinedLabel("Edit", "Secret.Edit"));
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => EditSelected();
        _filter = new ResourceFilter("filter secrets", ApplyFilter, () => _list);
        _list.AddCommand(ActionCommand("New", () => OpenEditor(null, false), () => !_loadError.Value));
        _list.AddCommand(ActionCommand("Edit", EditSelected, () => SelectedItem is not null,
            TuiKeybindHelpers.SecondaryPresentation("ResourceList.Activate")));
        _list.AddCommand(ActionCommand("Copy", () => OpenEditor(SelectedItem, true),
            () => SelectedItem is { IsBroken: false }));
        _list.AddCommand(ActionCommand("Delete", ShowDeleteDialog, () => SelectedItem is not null));
        _list.AddCommand(JsonCommand(false, CommandPresentation.CommandBar));
        _list.AddCommand(JsonCommand(true, CommandPresentation.None));

        _secretView = new ScrollableContent(new ComputedVisual(BuildSecret));
        _referencesView = new ScrollableContent(new ComputedVisual(BuildReferences));
        Visual sections = ResourceScreenLayoutHelpers.TwoPaneSections(_splits,
            "Secret", ResourceScreenLayoutHelpers.Pane(_secretView),
            "Known references", ResourceScreenLayoutHelpers.Pane(_referencesView));
        Visual listView = ResourceScreenLayoutHelpers.Scrollable(_list);
        Root = ResourceScreenLayoutHelpers.Create("Secrets",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter, _splits,
            () => _loading.Value
                ? ResourceScreenLayoutHelpers.Message(new HStack(new Spinner(),
                    new TextBlock("Loading secrets…").Style(StraumrStyleService.MutedText)).Spacing(1))
                : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayoutHelpers.EmptySections() : sections);

        PromptCommands =
        [
            new TuiCommandModel("select", SelectSecretAsync) { Aliases = ["s"], ArgumentValues = SecretNames },
            new TuiCommandModel("create", CreateSecretAsync) { Aliases = ["new"] },
            new TuiCommandModel("edit", (argument, _) => Task.FromResult(OnSelected(argument,
                item => item.IsBroken ? EditAsJson(item) : OpenEditorFor(item, false)))) { ArgumentValues = SecretNames },
            new TuiCommandModel("copy", (argument, _) => Task.FromResult(OnSelected(argument,
                item => item.IsBroken ? TuiCommandResultModel.Failed($"cannot copy {item.Name}: the secret cannot be read")
                    : OpenEditorFor(item, true)))) { ArgumentValues = SecretNames },
            new TuiCommandModel("delete", (argument, _) => Task.FromResult(OnSelected(argument, _ =>
            {
                ShowDeleteDialog();
                return TuiCommandResultModel.None;
            }))) { ArgumentValues = SecretNames },
            new TuiCommandModel("json", (argument, _) => Task.FromResult(OnSelected(argument, EditAsJson)))
                { ArgumentValues = SecretNames },
            new TuiCommandModel("refresh", RefreshAsync)
        ];
    }

    private SecretScreenItemModel? SelectedItem =>
        (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

    public TuiScreen Kind => TuiScreen.Secrets;
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
        _loading.Value = true;
        _loadError.Value = false;
        _displayedId = null;
        ActiveWorkspaceName = null;
        _items = [];
        _references.Value = new KnownReferenceService();
        try
        {
            await _state.LoadAsync(cancellationToken);
            if (_state.State.CurrentWorkspace is { } active)
            {
                try
                {
                    StraumrWorkspace workspace = await _workspaces.GetAsync(active.Id, false, cancellationToken);
                    if (workspace.Id == active.Id)
                    {
                        ActiveWorkspaceName = workspace.Name;
                    }
                }
                catch (Exception exception) when (IsRecoverable(exception)) { }
            }

            foreach (StraumrSecretEntry entry in _state.State.Secrets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    StraumrSecret secret = await _secrets.GetAsync(entry.Id, false, cancellationToken);
                    string? problem = Validate(secret, entry.Id);
                    _items.Add(new SecretScreenItemModel(entry.Id, entry.Path, problem is null ? secret : null, problem));
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    _items.Add(new SecretScreenItemModel(entry.Id, entry.Path, null, exception.Message));
                }
            }

            _items = _items.OrderByDescending(item => item.Secret?.LastAccessed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            SecretCatalogService.Set(_items.Where(item => !item.IsBroken).Select(item => item.Name));
            _references.Value = await KnownReferenceService.LoadAsync(_state.State.Workspaces,
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
            if (await save(cancellationToken) is { } result)
            {
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
            }
        }

        if (_pendingNotify is { } notify)
        {
            _pendingNotify = null;
            TuiCommandResultModel result = await notify(cancellationToken);
            if (result.Message is not null)
            {
                NotificationRequested?.Invoke(result);
            }
        }

        if (_pendingJsonRename is { Rename: { } cascade } pending)
        {
            _pendingJsonRename = null;
            ShowRenameDialog(cascade, pending.Secret.Name,
                () => _pendingNotify = token => ApplyJsonEditAsync(pending, token),
                () => _pendingNotify = _ => Task.FromResult(TuiCommandResultModel.Failed("edit not saved")));
        }

        if (_savePaneLayout)
        {
            _savePaneLayout = false;
            try { await _state.SaveAsync(cancellationToken); }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                NotificationRequested?.Invoke(TuiCommandResultModel.Failed($"cannot save pane layout: {exception.Message}"));
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
        {
            return new TextBlock("No secret selected.").Style(StraumrStyleService.MutedText)
                .Trimming(TextTrimming.EndEllipsis);
        }

        Visual summary = item.IsBroken
            ? new TextBlock($"{item.Name} · cannot be read").Style(StraumrStyleService.RedText)
                .Trimming(TextTrimming.EndEllipsis)
            : new HStack(
                    new TextBlock(SecretFormatting.Display(item.Name)).Style(StraumrStyleService.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis),
                    new TextBlock("Global secret").Style(StraumrStyleService.MutedText)
                        .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch))
                .Spacing(2).HorizontalAlignment(Align.Stretch);
        return StraumrSurfaceHelpers.Bar(summary,
            new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyleService.TokenChip));
    }

    private Visual BuildSecret()
    {
        if (SelectedItem is not { } item)
        {
            return Message("Unavailable.");
        }

        if (item.Secret is not { } secret)
        {
            return new VStack(
                    FieldListHelpers.Create(("Problem", FieldListHelpers.Problem(item.Problem ?? "the file is not a valid secret")),
                        ("Storage", FieldListHelpers.Wrapped(PathFormatting.Display(item.Path)))),
                    Message(
                        $"Press {TuiKeybindHelpers.Hint("Secret.Edit")} on the row to repair it in your editor, or {TuiKeybindHelpers.Hint("Secret.Delete")} to remove the entry."))
                .Spacing(1).HorizontalAlignment(Align.Stretch);
        }

        return FieldListHelpers.Create(
            ("Value", FieldListHelpers.Styled(secret.Value.Length == 0 ? "not set" : "••••••••••••",
                secret.Value.Length == 0 ? StraumrStyleService.MutedText : StraumrStyleService.AmberText)),
            ("Modified", FieldListHelpers.Text(TimestampFormatting.Absolute(secret.Modified))),
            ("Last accessed", FieldListHelpers.Text(LastAccessed(secret))),
            ("Storage", FieldListHelpers.Wrapped(PathFormatting.Display(item.Path))));
    }

    private Visual BuildReferences()
    {
        if (SelectedItem is not { } item)
        {
            return Message("Unavailable.");
        }

        if (item.IsBroken)
        {
            return Message("References cannot be matched until the secret's name can be read.");
        }

        return ReferenceViewHelpers.Create(ReferenceViewHelpers.Placeholder(item.Name, true),
            CountFormatting.Label(_references.Value.ScannedWorkspaces, "scanned workspace"),
            _references.Value.ForSecret(item.Name), _references.Value);
    }

    private static Visual Message(string text) =>
        new TextBlock(text).Style(StraumrStyleService.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch);

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
        _list.SetRows(_visible.Value.Select(item => new ResourceRowModel(SecretFormatting.Display(item.Name),
            item.Secret is { } secret ? LastAccessed(secret) : "cannot be read",
            item.Secret is { Value.Length: > 0 }, IsBroken: item.IsBroken)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value)
        {
            _emptyMessage.Value = _query.Value.Length > 0
                ? "No secrets match this filter." : $"No global secrets. Press {TuiKeybindHelpers.Hint("Secret.New")} to create one.";
        }
    }

    private IEnumerable<string> SecretNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResultModel> SelectSecretAsync(string argument, CancellationToken cancellationToken)
    {
        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return Task.FromResult(TuiCommandResultModel.Failed(error!));
        }

        return Task.FromResult(name.Length == 0
            ? TuiCommandResultModel.Failed("usage: select <name or ID>") : SelectSecret(name));
    }

    private TuiCommandResultModel SelectSecret(string name)
    {
        List<SecretScreenItemModel> matches = _items.FindAll(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            item.Id.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
        {
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                                             item.Id.ToString().StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }

        if (matches.Count != 1)
        {
            return TuiCommandResultModel.Failed(matches.Count == 0 ? $"no secret matches {name}"
                : $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        }

        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResultModel.None;
    }

    private TuiCommandResultModel OnSelected(string argument, Func<SecretScreenItemModel, TuiCommandResultModel> action)
    {
        if (_loadError.Value)
        {
            return TuiCommandResultModel.Failed(_emptyMessage.Value);
        }

        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return TuiCommandResultModel.Failed(error!);
        }

        if (name.Length > 0)
        {
            TuiCommandResultModel selection = SelectSecret(name);
            if (selection.IsError)
            {
                return selection;
            }
        }
        return SelectedItem is { } item ? action(item) : TuiCommandResultModel.Failed("no secret selected");
    }

    private Task<TuiCommandResultModel> CreateSecretAsync(string argument, CancellationToken cancellationToken) =>
        Task.FromResult(_loadError.Value ? TuiCommandResultModel.Failed(_emptyMessage.Value) : argument.Length > 0
            ? TuiCommandResultModel.Failed("usage: create") : OpenEditorFor(null, false));

    private async Task<TuiCommandResultModel> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return TuiCommandResultModel.Failed("usage: refresh");
        }

        await LoadAsync(cancellationToken);
        return _loadError.Value ? TuiCommandResultModel.Failed(_emptyMessage.Value)
            : TuiCommandResultModel.Ok($"reloaded {CountFormatting.Label(_items.Count, "secret")}");
    }

    private TuiCommandResultModel OpenEditorFor(SecretScreenItemModel? source, bool copy)
    {
        if (_editorView is not null)
        {
            return TuiCommandResultModel.Failed("the secret editor is already open");
        }

        OpenEditor(source, copy);
        return TuiCommandResultModel.None;
    }

    private void EditSelected()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        if (item.IsBroken)
        {
            NotifyIfFailed(EditAsJson(item));
        }
        else
        {
            OpenEditor(item, false);
        }
    }

    private void OpenEditor(SecretScreenItemModel? source, bool copy)
    {
        if (_editorView is not null)
        {
            return;
        }

        bool isNew = source is null || copy;
        StraumrSecret state = source?.Secret is { } secret
            ? secret.CopyAs(isNew ? string.Empty : secret.Name)
            : new StraumrSecret { Name = string.Empty, Value = string.Empty };
        _editingId = isNew ? null : source!.Id;
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new SecretEditor(state, ActiveWorkspaceName, isNew,
            _references, () => _pendingSave = token => SaveEditAsync(state, token),
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

    private async Task<TuiCommandResultModel?> SaveEditAsync(StraumrSecret state, CancellationToken cancellationToken)
    {
        if (_editingId is not { } id)
        {
            return await WriteEditAsync(state, null, cancellationToken);
        }

        string opened;
        try
        {
            StraumrSecret existing = await _secrets.GetAsync(id, false, cancellationToken);
            if (existing.Id != id)
            {
                return TuiCommandResultModel.Failed("the secret ID no longer matches its registry entry; close and refresh to repair it");
            }

            opened = existing.Name;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResultModel.Failed(exception.Message);
        }

        if (opened.Equals(state.Name, StringComparison.OrdinalIgnoreCase))
        {
            return await WriteEditAsync(state, null, cancellationToken);
        }

        try
        {
            _references.Value = await KnownReferenceService.LoadAsync(_state.State.Workspaces,
                _workspaces, _requests, _auths, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResultModel.Failed(exception.Message);
        }

        IReadOnlyList<ReferenceUsageModel> usages = _references.Value.ForSecret(opened);
        if (usages.Count == 0)
        {
            return await WriteEditAsync(state, null, cancellationToken);
        }

        var rename = new ReferenceRenameModel(opened, usages);
        ShowRenameDialog(rename, state.Name,
            () => _pendingSave = token => WriteEditAsync(state, rename, token),
            () => _pendingSave = _ => Task.FromResult<TuiCommandResultModel?>(
                TuiCommandResultModel.Failed("rename not saved")));
        return null;
    }

    private void ShowRenameDialog(ReferenceRenameModel rename, string name, Action update, Action cancel)
    {
        int resources = rename.Usages.Select(usage => (usage.WorkspaceId, usage.ResourceId)).Distinct().Count();
        int workspaces = rename.Usages.Select(usage => usage.WorkspaceId).Distinct().Count();
        string detail = $"Used by {CountFormatting.Label(resources, "resource")} in " +
                        $"{CountFormatting.Label(workspaces, "workspace")}.";
        if (_references.Value.Notice is { } notice)
        {
            detail += $" {notice}";
        }

        new ConfirmDialog("Rename secret",
            $"Update {CountFormatting.Label(rename.Usages.Count, "reference")} to {name}?",
            detail, "Rename and update", false, update, cancel).Show();
    }

    private async Task<TuiCommandResultModel?> WriteEditAsync(StraumrSecret state, ReferenceRenameModel? rename,
        CancellationToken cancellationToken)
    {
        try
        {
            bool created = _editingId is null;
            StraumrSecret saved;
            if (_editingId is { } id)
            {
                StraumrSecret existing = await _secrets.GetAsync(id, false, cancellationToken);
                if (existing.Id != id)
                {
                    return TuiCommandResultModel.Failed("the secret ID no longer matches its registry entry; close and refresh to repair it");
                }

                existing.Name = state.Name;
                existing.Value = state.Value;
                saved = await _secrets.SaveAsync(existing, cancellationToken);
            }
            else
            {
                saved = await _secrets.CreateAsync(state, cancellationToken);
                _editingId = saved.Id;
            }

            string message = created ? $"created secret {saved.Name}" : $"updated secret {saved.Name}";
            if (rename is { } cascade)
            {
                message += await RewriteReferencesAsync(cascade, saved.Name, cancellationToken);
            }

            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, saved.Id);
            return TuiCommandResultModel.Ok(message);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return TuiCommandResultModel.Failed(exception.Message);
        }
    }

    private async Task<string> RewriteReferencesAsync(ReferenceRenameModel rename, string name,
        CancellationToken cancellationToken)
    {
        ReferenceRewriteModel rewrite = await ReferenceRewriteHelpers.ApplyAsync(_state.State.Workspaces,
            rename.Usages, rename.OldName, name, true, _requests, _auths, cancellationToken);
        string report = $" and {CountFormatting.Label(rewrite.References, "reference")} " +
                        $"in {CountFormatting.Label(rewrite.Resources, "resource")}";
        if (rewrite.Problems.Count == 0)
        {
            return report;
        }

        return $"{report}; {string.Join("; ", rewrite.Problems.Take(2))}" +
               (rewrite.Problems.Count > 2 ? $" and {rewrite.Problems.Count - 2} more" : string.Empty);
    }

    private TuiCommandResultModel EditAsJson(SecretScreenItemModel item)
    {
        if (!_editor.IsConfigured)
        {
            return TuiCommandResultModel.Failed("edit failed: no default editor is configured");
        }

        ExternalActionRequested?.Invoke(new TuiExternalActionModel(token => EditJsonAsync(item, token), _list));
        return TuiCommandResultModel.None;
    }

    private async Task<TuiCommandResultModel> EditJsonAsync(SecretScreenItemModel item, CancellationToken cancellationToken)
    {
        try
        {
            string original = File.Exists(item.Path) ? await File.ReadAllTextAsync(item.Path, cancellationToken)
                : JsonSerializer.Serialize(new StraumrSecret { Id = item.Id, Name = string.Empty, Value = string.Empty },
                    StraumrJsonContext.Default.StraumrSecret);
            string edited = await _editor.EditJsonAsync(original, cancellationToken);
            if (edited == original)
            {
                return TuiCommandResultModel.None;
            }

            StraumrSecret? secret = null;
            try { secret = JsonSerializer.Deserialize(edited, StraumrJsonContext.Default.StraumrSecret); }
            catch (JsonException) { }
            string? problem = Validate(secret, item.Id);
            if (problem is not null)
            {
                return TuiCommandResultModel.Failed($"edit not saved: {problem}");
            }

            var pending = new SecretJsonRenameModel(item, secret!, edited, original, null);
            if (item.Secret is { } current &&
                !current.Name.Equals(secret!.Name, StringComparison.OrdinalIgnoreCase))
            {
                _references.Value = await KnownReferenceService.LoadAsync(_state.State.Workspaces,
                    _workspaces, _requests, _auths, cancellationToken);
                IReadOnlyList<ReferenceUsageModel> usages = _references.Value.ForSecret(current.Name);
                if (usages.Count > 0)
                {
                    _pendingJsonRename = pending with { Rename = new ReferenceRenameModel(current.Name, usages) };
                    return TuiCommandResultModel.None;
                }
            }

            return await ApplyJsonEditAsync(pending, cancellationToken);
        }
        catch (Exception exception) when (IsRecoverable(exception) || exception is ExternalEditorException)
        {
            return TuiCommandResultModel.Failed($"edit failed: {exception.Message}");
        }
    }

    private async Task<TuiCommandResultModel> ApplyJsonEditAsync(SecretJsonRenameModel edit, CancellationToken cancellationToken)
    {
        try
        {
            bool missing = !File.Exists(edit.Item.Path);
            if (missing)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(edit.Item.Path)!);
                await File.WriteAllTextAsync(edit.Item.Path, edit.Original, cancellationToken);
            }
            _files.CarryCommentsFrom(edit.Item.Path, edit.Edited);
            try { await _secrets.SaveAsync(edit.Secret, cancellationToken); }
            catch
            {
                if (missing && File.Exists(edit.Item.Path))
                {
                    File.Delete(edit.Item.Path);
                }

                throw;
            }

            string message = $"updated secret {edit.Secret.Name}";
            if (edit.Rename is { } cascade)
            {
                message += await RewriteReferencesAsync(cascade, edit.Secret.Name, cancellationToken);
            }

            await LoadAsync(cancellationToken);
            ApplyFilter(_filter.Text, edit.Item.Id);
            return TuiCommandResultModel.Ok(message);
        }
        catch (Exception exception) when (IsRecoverable(exception))
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

        int dependents = _references.Value.ForSecret(item.Name)
            .Select(reference => (reference.WorkspaceId, reference.ResourceId, reference.Kind)).Distinct().Count();
        string detail = "The global secret will be permanently deleted. References are not changed.";
        if (dependents > 0)
        {
            detail += $" {CountFormatting.Label(dependents, "resource")} in registered workspaces still reference it.";
        }

        if (item.IsBroken)
        {
            detail += " Its name cannot be read, so its references cannot be counted.";
        }

        if (_references.Value.Notice is { } notice)
        {
            detail += $" {notice}";
        }

        new ConfirmDialog("Delete secret", $"Delete {SecretFormatting.Display(item.Name)}?", detail,
            "Delete", true, () => _pendingDeleteId = item.Id).Show();
    }

    private async Task DeletePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } id)
        {
            return;
        }

        _pendingDeleteId = null;
        if (_items.Find(item => item.Id == id) is not { } item)
        {
            return;
        }

        int index = _selectedIndex.Value;
        try
        {
            await _secrets.DeleteAsync(id, cancellationToken);
            await LoadAsync(cancellationToken);
            if (_visible.Value.Count > 0)
            {
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            }

            NotificationRequested?.Invoke(TuiCommandResultModel.Ok($"deleted secret {item.Name}"));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            NotificationRequested?.Invoke(TuiCommandResultModel.Failed($"delete failed: {exception.Message}"));
        }
    }

    private static string? Validate(StraumrSecret? secret, Guid id) => secret switch
    {
        null => "the file is not a valid secret",
        not null when secret.Id != id => "the secret ID does not match its registry entry",
        not null when string.IsNullOrWhiteSpace(secret.Name) => "the secret name is missing",
        not null when secret.Name.Contains('"') => "the secret name contains a double quote",
        { Value: null } => "the secret has no value",
        _ => null
    };

    internal static bool IsRecoverable(Exception exception) =>
        exception is StraumrException or IOException or UnauthorizedAccessException or JsonException;

    private void NotifyIfFailed(TuiCommandResultModel result)
    {
        if (result.IsError)
        {
            NotificationRequested?.Invoke(result);
        }
    }

    private Command JsonCommand(bool letterVariant, CommandPresentation presentation) => new()
    {
        Id = letterVariant ? "Secret.EditJson.Letter" : "Secret.EditJson",
        LabelMarkup = "Edit JSON",
        Gesture = TuiKeybindHelpers.Get(letterVariant ? "Secret.EditJson.Letter" : "Secret.EditJson"),
        Importance = CommandImportance.Secondary,
        Presentation = presentation,
        IsVisible = _ => SelectedItem is not null,
        CanExecute = _ => SelectedItem is not null,
        ConsumesGestureWhenUnavailable = false,
        Execute = _ =>
        {
            if (SelectedItem is { } item) { NotifyIfFailed(EditAsJson(item)); }
        }
    };

    private static Command ActionCommand(string label, Action execute, Func<bool> available,
        CommandPresentation presentation = CommandPresentation.CommandBar) => new()
    {
        Id = $"Secret.{label}",
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get($"Secret.{label}"),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => TuiKeybindHelpers.Run($"Secret.{label}", execute)
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
