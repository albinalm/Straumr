using System.Text.Json;
using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Console.Tui.Screens.Components.Variable;
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

public sealed class VariableScreen : ITuiScreen
{
    private const string PaneLayoutKey = nameof(TuiScreen.Variables);
    private readonly IStraumrAuthService _auths;
    private readonly State<int> _count = new(0);
    private readonly ExternalEditorService _editor;
    private readonly State<string> _emptyMessage = new("Loading variables…");
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
    private readonly State<int> _selectedIndex = new(-1);
    private readonly PaneSplits _splits;
    private readonly IStraumrStateService _state;
    private readonly IStraumrVariableService _variables;
    private readonly ScrollableContent _variableView;
    private readonly State<List<VariableScreenItemModel>> _visible = new([]);
    private readonly IStraumrWorkspaceService _workspaces;
    private Guid? _displayedId;
    private Guid? _editingId;
    private VariableEditor? _editorView;
    private List<VariableScreenItemModel> _items = [];
    private Guid? _pendingDeleteId;
    private VariableJsonRenameModel? _pendingJsonRename;
    private Func<CancellationToken, Task<TuiCommandResultModel>>? _pendingNotify;
    private Func<CancellationToken, Task<TuiCommandResultModel?>>? _pendingSave;
    private bool _savePaneLayout;
    private StraumrWorkspaceEntry? _workspace;

    public VariableScreen(IStraumrStateService state, IStraumrWorkspaceService workspaces,
        IStraumrRequestService requests, IStraumrAuthService auths, IStraumrVariableService variables,
        IStraumrFileService files, ExternalEditorService editor)
    {
        (_state, _workspaces, _requests, _auths, _variables, _files, _editor) =
            (state, workspaces, requests, auths, variables, files, editor);
        StraumrPaneLayout layout = state.State.PaneLayouts.GetValueOrDefault(PaneLayoutKey) ?? new StraumrPaneLayout();
        _splits = new PaneSplits(layout.Panels, layout.Sections, layout.Stack);
        _splits.Changed += QueuePaneLayoutSave;

        _list = new ResourceList([], ResourceScreenLayoutHelpers.Message(
                new TextBlock(() => _emptyMessage.Value)
                    .Style(() => _loadError.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedText)
                    .Wrap(true).Trimming(TextTrimming.EndEllipsis)),
            TuiKeybindHelpers.CombinedLabel("Edit", "Variable.Edit"));
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => EditSelected();
        _filter = new ResourceFilter("filter variables", ApplyFilter, () => _list);
        _list.AddCommand(ActionCommand("New", () => OpenEditor(null), () => !_loadError.Value && _workspace is not null));
        _list.AddCommand(ActionCommand("Edit", () => EditSelected(), () => SelectedItem is not null,
            TuiKeybindHelpers.SecondaryPresentation("ResourceList.Activate")));
        _list.AddCommand(ActionCommand("Copy", () => OpenEditor(SelectedItem, true),
            () => SelectedItem is { IsBroken: false }));
        _list.AddCommand(ActionCommand("Delete", ShowDeleteDialog, () => SelectedItem is not null));
        _list.AddCommand(JsonCommand(false, CommandPresentation.CommandBar));
        _list.AddCommand(JsonCommand(true, CommandPresentation.None));

        _variableView = new ScrollableContent(new ComputedVisual(BuildVariable));
        _referencesView = new ScrollableContent(new ComputedVisual(BuildReferences));
        Visual sections = ResourceScreenLayoutHelpers.TwoPaneSections(_splits,
            "Variable", ResourceScreenLayoutHelpers.Pane(_variableView),
            "Known references", ResourceScreenLayoutHelpers.Pane(_referencesView));
        Visual listView = ResourceScreenLayoutHelpers.Scrollable(_list);
        Root = ResourceScreenLayoutHelpers.Create("Variables",
            () => _query.Value.Length == 0 ? _count.Value.ToString() : $"{_matchCount.Value}/{_count.Value}",
            _filter, _splits,
            () => _loading.Value
                ? ResourceScreenLayoutHelpers.Message(new HStack(new Spinner(),
                    new TextBlock("Loading variables…").Style(StraumrStyleService.MutedText)).Spacing(1))
                : listView,
            BuildHead,
            () => SelectedItem is null ? ResourceScreenLayoutHelpers.EmptySections() : sections);

        PromptCommands =
        [
            new TuiCommandModel("select", SelectVariableAsync) { Aliases = ["v"], ArgumentValues = VariableNames },
            new TuiCommandModel("create", CreateVariableAsync) { Aliases = ["new"] },
            new TuiCommandModel("edit", (argument, _) => Task.FromResult(OnSelected(argument,
                item => item.IsBroken ? EditAsJson(item) : OpenEditorFor(item, false)))) { ArgumentValues = VariableNames },
            new TuiCommandModel("copy", (argument, _) => Task.FromResult(OnSelected(argument,
                item => item.IsBroken ? TuiCommandResultModel.Failed($"cannot copy {item.Name}: the variable cannot be read")
                    : OpenEditorFor(item, true)))) { ArgumentValues = VariableNames },
            new TuiCommandModel("delete", (argument, _) => Task.FromResult(OnSelected(argument, _ =>
            {
                ShowDeleteDialog();
                return TuiCommandResultModel.None;
            }))) { ArgumentValues = VariableNames },
            new TuiCommandModel("json", (argument, _) => Task.FromResult(OnSelected(argument, EditAsJson)))
                { ArgumentValues = VariableNames },
            new TuiCommandModel("refresh", RefreshAsync)
        ];
    }

    private VariableScreenItemModel? SelectedItem =>
        (uint)_selectedIndex.Value < (uint)_visible.Value.Count ? _visible.Value[_selectedIndex.Value] : null;

    public TuiScreen Kind => TuiScreen.Variables;
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
        _references.Value = new KnownReferenceService();
        try
        {
            await _state.LoadAsync(cancellationToken);
            _workspace = _state.State.CurrentWorkspace;
            ActiveWorkspaceName = null;
            _items = [];
            if (_workspace is null)
            {
                VariableCatalogService.Set([]);
                _emptyMessage.Value = "No active workspace. Use :workspace to choose one.";
                ApplyFilter(string.Empty);
                return;
            }

            StraumrWorkspace workspace = await _workspaces.GetAsync(_workspace.Id, false, cancellationToken);
            if (workspace.Id != _workspace.Id)
            {
                throw new StraumrException(
                    "The active workspace has a mismatched ID; repair it in Workspaces.", StraumrError.CorruptEntry);
            }

            ActiveWorkspaceName = workspace.Name;
            foreach (Guid id in workspace.Variables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(Path.GetDirectoryName(_workspace.Path)!, $"{id}.jsonc");
                try
                {
                    StraumrVariable variable = await _variables.GetAsync(_workspace, id, false, cancellationToken);
                    string? problem = Validate(variable, id);
                    _items.Add(new VariableScreenItemModel(id, path, problem is null ? variable : null, problem));
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    _items.Add(new VariableScreenItemModel(id, path, null, exception.Message));
                }
            }

            _items = _items.OrderByDescending(item => item.Variable?.LastAccessed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            VariableCatalogService.Set(_items.Where(item => !item.IsBroken).Select(item => item.Name));
            _references.Value = await KnownReferenceService.LoadAsync(_state.State.Workspaces,
                _workspaces, _requests, _auths, cancellationToken);
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
            _emptyMessage.Value = $"Cannot load variables: {exception.Message}\nUse :refresh to retry, or :workspace.";
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
            ShowRenameDialog(cascade, pending.Variable.Name,
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
            _variableView.ScrollOffset = 0;
            _referencesView.ScrollOffset = 0;
        }
    }

    private Visual BuildHead()
    {
        if (SelectedItem is not { } item)
        {
            return new TextBlock("No variable selected.").Style(StraumrStyleService.MutedText)
                .Trimming(TextTrimming.EndEllipsis);
        }

        Visual summary = item.IsBroken
            ? new TextBlock($"{item.Name} · cannot be read").Style(StraumrStyleService.RedText)
                .Trimming(TextTrimming.EndEllipsis)
            : new HStack(
                    new TextBlock(item.Name).Style(StraumrStyleService.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis),
                    new TextBlock("Workspace variable").Style(StraumrStyleService.MutedText)
                        .Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch))
                .Spacing(2).HorizontalAlignment(Align.Stretch);
        return StraumrSurfaceHelpers.Bar(summary,
            new TextBlock($" {item.Id.ToString()[..8]} ").Style(StraumrStyleService.TokenChip));
    }

    private Visual BuildVariable()
    {
        if (SelectedItem is not { } item)
        {
            return Message("Unavailable.");
        }

        if (item.Variable is not { } variable)
        {
            return new VStack(
                    FieldListHelpers.Create(("Problem", FieldListHelpers.Problem(item.Problem ?? "the file is not a valid variable")),
                        ("Storage", FieldListHelpers.Wrapped(PathFormatting.Display(item.Path)))),
                    Message(
                        $"Press {TuiKeybindHelpers.Hint("Variable.Edit")} on the row to repair it in your editor, or {TuiKeybindHelpers.Hint("Variable.Delete")} to remove the entry."))
                .Spacing(1).HorizontalAlignment(Align.Stretch);
        }

        return FieldListHelpers.Create(
            ("Value", variable.Value.Length == 0
                ? FieldListHelpers.Styled("not set", StraumrStyleService.MutedText)
                : FieldListHelpers.Wrapped(variable.Value)),
            ("Modified", FieldListHelpers.Text(TimestampFormatting.Absolute(variable.Modified))),
            ("Last accessed", FieldListHelpers.Text(LastAccessed(variable))),
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
            return Message("References cannot be matched until the variable's name can be read.");
        }

        if (_workspace is not { } workspace)
        {
            return Message("Unavailable.");
        }

        return ReferenceViewHelpers.Create(ReferenceViewHelpers.Placeholder(item.Name, false), "this workspace",
            _references.Value.ForVariable(workspace.Id, item.Name), _references.Value);
    }

    private static Visual Message(string text) =>
        new TextBlock(text).Style(StraumrStyleService.MutedText).Wrap(true).HorizontalAlignment(Align.Stretch);

    private static string LastAccessed(StraumrVariable variable) => variable.LastAccessed == DateTimeOffset.MinValue
        ? "never accessed" : TimestampFormatting.Relative(variable.LastAccessed);

    private void ApplyFilter(string query) => ApplyFilter(query, SelectedItem?.Id);

    private void ApplyFilter(string query, Guid? selected)
    {
        _query.Value = query.Trim();
        _count.Value = _items.Count;
        _visible.Value = _items.Where(item => item.Name.Contains(_query.Value, StringComparison.OrdinalIgnoreCase) ||
                                              item.Id.ToString().Contains(_query.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        _matchCount.Value = _visible.Value.Count;
        _list.SetRows(_visible.Value.Select(item => new ResourceRowModel(item.Name,
            item.Variable is { } variable ? LastAccessed(variable) : "cannot be read",
            item.Variable is { Value.Length: > 0 }, IsBroken: item.IsBroken)));
        int index = selected is { } id ? _visible.Value.FindIndex(item => item.Id == id) : -1;
        _selectedIndex.Value = index >= 0 ? index : _visible.Value.Count > 0 ? 0 : -1;
        if (!_loadError.Value && _workspace is not null)
        {
            _emptyMessage.Value = _query.Value.Length > 0
                ? "No variables match this filter."
                : $"No variables in this workspace. Press {TuiKeybindHelpers.Hint("Variable.New")} to create one.";
        }
    }

    private IEnumerable<string> VariableNames() => _items.Select(item => item.Name);

    private Task<TuiCommandResultModel> SelectVariableAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
        {
            return Task.FromResult(TuiCommandResultModel.NoWorkspace);
        }

        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return Task.FromResult(TuiCommandResultModel.Failed(error!));
        }

        return Task.FromResult(name.Length == 0
            ? TuiCommandResultModel.Failed("usage: select <name or ID>") : SelectVariable(name));
    }

    private TuiCommandResultModel SelectVariable(string name)
    {
        List<VariableScreenItemModel> matches = _items.FindAll(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            item.Id.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
        {
            matches = _items.FindAll(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                                             item.Id.ToString().StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }

        if (matches.Count != 1)
        {
            return TuiCommandResultModel.Failed(matches.Count == 0 ? $"no variable matches {name}"
                : $"{name} matches {string.Join(", ", matches.Select(item => item.Name))}");
        }

        _filter.Clear();
        _selectedIndex.Value = _visible.Value.IndexOf(matches[0]);
        return TuiCommandResultModel.None;
    }

    private TuiCommandResultModel OnSelected(string argument, Func<VariableScreenItemModel, TuiCommandResultModel> action)
    {
        if (_workspace is null)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

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
            TuiCommandResultModel selection = SelectVariable(name);
            if (selection.IsError)
            {
                return selection;
            }
        }

        return SelectedItem is { } item ? action(item) : TuiCommandResultModel.Failed("no variable selected");
    }

    private Task<TuiCommandResultModel> CreateVariableAsync(string argument, CancellationToken cancellationToken)
    {
        if (_workspace is null)
        {
            return Task.FromResult(TuiCommandResultModel.NoWorkspace);
        }

        return Task.FromResult(_loadError.Value ? TuiCommandResultModel.Failed(_emptyMessage.Value) : argument.Length > 0
            ? TuiCommandResultModel.Failed("usage: create") : OpenEditorFor(null, false));
    }

    private async Task<TuiCommandResultModel> RefreshAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return TuiCommandResultModel.Failed("usage: refresh");
        }

        await LoadAsync(cancellationToken);
        return _loadError.Value ? TuiCommandResultModel.Failed(_emptyMessage.Value)
            : TuiCommandResultModel.Ok($"reloaded {CountFormatting.Label(_items.Count, "variable")}");
    }

    private TuiCommandResultModel OpenEditorFor(VariableScreenItemModel? source, bool copy)
    {
        if (_editorView is not null)
        {
            return TuiCommandResultModel.Failed("the variable editor is already open");
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

    private void OpenEditor(VariableScreenItemModel? source, bool copy = false)
    {
        if (_editorView is not null || _workspace is null)
        {
            return;
        }

        bool isNew = source is null || copy;
        StraumrVariable state = source?.Variable is { } variable
            ? variable.CopyAs(isNew ? string.Empty : variable.Name)
            : new StraumrVariable { Name = string.Empty, Value = string.Empty };
        _editingId = isNew ? null : source!.Id;
        string? sourceName = isNew && source is not null ? source.Name : null;
        var editor = new VariableEditor(state, ActiveWorkspaceName, _workspace.Id, isNew, _references,
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

    private async Task<TuiCommandResultModel?> SaveEditAsync(StraumrVariable state, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        if (_editingId is not { } id)
        {
            return await WriteEditAsync(state, null, cancellationToken);
        }

        string opened;
        try
        {
            StraumrVariable existing = await _variables.GetAsync(workspace, id, false, cancellationToken);
            if (existing.Id != id)
            {
                return TuiCommandResultModel.Failed("the variable ID no longer matches its workspace entry; close and refresh to repair it");
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

        IReadOnlyList<ReferenceUsageModel> usages = _references.Value.ForVariable(workspace.Id, opened);
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
        int resources = rename.Usages.Select(usage => usage.ResourceId).Distinct().Count();
        string detail = $"Used by {CountFormatting.Label(resources, "resource")} in this workspace.";
        if (_references.Value.Notice is { } notice)
        {
            detail += $" {notice}";
        }

        new ConfirmDialog("Rename variable",
            $"Update {CountFormatting.Label(rename.Usages.Count, "reference")} to {name}?",
            detail, "Rename and update", false, update, cancel).Show();
    }

    private async Task<TuiCommandResultModel?> WriteEditAsync(StraumrVariable state, ReferenceRenameModel? rename,
        CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        try
        {
            bool created = _editingId is null;
            StraumrVariable saved;
            if (_editingId is { } id)
            {
                StraumrVariable existing = await _variables.GetAsync(workspace, id, false, cancellationToken);
                if (existing.Id != id)
                {
                    return TuiCommandResultModel.Failed("the variable ID no longer matches its workspace entry; close and refresh to repair it");
                }

                existing.Name = state.Name;
                existing.Value = state.Value;
                saved = await _variables.SaveAsync(workspace, existing, cancellationToken);
            }
            else
            {
                saved = await _variables.CreateAsync(workspace, state, cancellationToken);
                _editingId = saved.Id;
            }

            string message = created ? $"created variable {saved.Name}" : $"updated variable {saved.Name}";
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
            rename.Usages, rename.OldName, name, false, _requests, _auths, cancellationToken);
        string report = $" and {CountFormatting.Label(rewrite.References, "reference")} " +
                        $"in {CountFormatting.Label(rewrite.Resources, "resource")}";
        if (rewrite.Problems.Count == 0)
        {
            return report;
        }

        return $"{report}; {string.Join("; ", rewrite.Problems.Take(2))}" +
               (rewrite.Problems.Count > 2 ? $" and {rewrite.Problems.Count - 2} more" : string.Empty);
    }

    private TuiCommandResultModel EditAsJson(VariableScreenItemModel item)
    {
        if (!_editor.IsConfigured)
        {
            return TuiCommandResultModel.Failed("edit failed: no default editor is configured");
        }

        ExternalActionRequested?.Invoke(new TuiExternalActionModel(token => EditJsonAsync(item, token), _list));
        return TuiCommandResultModel.None;
    }

    private async Task<TuiCommandResultModel> EditJsonAsync(VariableScreenItemModel item, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        try
        {
            string original = File.Exists(item.Path) ? await File.ReadAllTextAsync(item.Path, cancellationToken)
                : JsonSerializer.Serialize(new StraumrVariable { Id = item.Id, Name = string.Empty, Value = string.Empty },
                    StraumrJsonContext.Default.StraumrVariable);
            string edited = await _editor.EditJsonAsync(original, cancellationToken);
            if (edited == original)
            {
                return TuiCommandResultModel.None;
            }

            StraumrVariable? variable = null;
            try { variable = JsonSerializer.Deserialize(edited, StraumrJsonContext.Default.StraumrVariable); }
            catch (JsonException) { }
            string? problem = Validate(variable, item.Id);
            if (problem is not null)
            {
                return TuiCommandResultModel.Failed($"edit not saved: {problem}");
            }

            var pending = new VariableJsonRenameModel(item, variable!, edited, original, null);
            if (item.Variable is { } current &&
                !current.Name.Equals(variable!.Name, StringComparison.OrdinalIgnoreCase))
            {
                _references.Value = await KnownReferenceService.LoadAsync(_state.State.Workspaces,
                    _workspaces, _requests, _auths, cancellationToken);
                IReadOnlyList<ReferenceUsageModel> usages = _references.Value.ForVariable(workspace.Id, current.Name);
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

    private async Task<TuiCommandResultModel> ApplyJsonEditAsync(VariableJsonRenameModel edit, CancellationToken cancellationToken)
    {
        if (_workspace is not { } workspace)
        {
            return TuiCommandResultModel.NoWorkspace;
        }

        try
        {
            bool missing = !File.Exists(edit.Item.Path);
            if (missing)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(edit.Item.Path)!);
                await File.WriteAllTextAsync(edit.Item.Path, edit.Original, cancellationToken);
            }

            _files.CarryCommentsFrom(edit.Item.Path, edit.Edited);
            try { await _variables.SaveAsync(workspace, edit.Variable, cancellationToken); }
            catch
            {
                if (missing && File.Exists(edit.Item.Path))
                {
                    File.Delete(edit.Item.Path);
                }

                throw;
            }

            string message = $"updated variable {edit.Variable.Name}";
            if (edit.Rename is { } cascade)
            {
                message += await RewriteReferencesAsync(cascade, edit.Variable.Name, cancellationToken);
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
        if (SelectedItem is not { } item || _workspace is not { } workspace)
        {
            return;
        }

        int dependents = _references.Value.ForVariable(workspace.Id, item.Name)
            .Select(reference => (reference.ResourceId, reference.Kind)).Distinct().Count();
        string detail = "The workspace variable will be permanently deleted. References are not changed.";
        if (dependents > 0)
        {
            detail += $" {CountFormatting.Label(dependents, "resource")} in this workspace still reference it.";
        }

        if (item.IsBroken)
        {
            detail += " Its name cannot be read, so its references cannot be counted.";
        }

        if (_references.Value.Notice is { } notice)
        {
            detail += $" {notice}";
        }

        new ConfirmDialog("Delete variable", $"Delete {item.Name}?", detail,
            "Delete", true, () => _pendingDeleteId = item.Id).Show();
    }

    private async Task DeletePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } id || _workspace is not { } workspace)
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
            await _variables.DeleteAsync(workspace, id, cancellationToken);
            await LoadAsync(cancellationToken);
            if (_visible.Value.Count > 0)
            {
                _selectedIndex.Value = Math.Clamp(index, 0, _visible.Value.Count - 1);
            }

            NotificationRequested?.Invoke(TuiCommandResultModel.Ok($"deleted variable {item.Name}"));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            NotificationRequested?.Invoke(TuiCommandResultModel.Failed($"delete failed: {exception.Message}"));
        }
    }

    private static string? Validate(StraumrVariable? variable, Guid id) => variable switch
    {
        null => "the file is not a valid variable",
        not null when variable.Id != id => "the variable ID does not match its workspace entry",
        not null when string.IsNullOrWhiteSpace(variable.Name) => "the variable name is missing",
        not null when variable.Name.Contains('"') => "the variable name contains a double quote",
        not null when variable.Name.Contains('{') || variable.Name.Contains('}') =>
            "the variable name contains a brace",
        not null when VariableHelpers.IsSecretName(variable.Name) =>
            $"the variable name starts with {VariableHelpers.SecretPrefix}, which resolves a secret",
        { Value: null } => "the variable has no value",
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
        Id = letterVariant ? "Variable.EditJson.Letter" : "Variable.EditJson",
        LabelMarkup = "Edit JSON",
        Gesture = TuiKeybindHelpers.Get(letterVariant ? "Variable.EditJson.Letter" : "Variable.EditJson"),
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
        Id = $"Variable.{label}",
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get($"Variable.{label}"),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => TuiKeybindHelpers.Run($"Variable.{label}", execute)
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
