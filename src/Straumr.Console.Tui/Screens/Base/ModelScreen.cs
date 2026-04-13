using System.Collections.ObjectModel;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Factories;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens.Base;

public abstract class ModelScreen<TEntry> : Screen
{
    private readonly Dictionary<string, ModelCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<MarkupLabel> _displayItems = [];
    private readonly List<TEntry> _sourceEntries = [];
    private readonly List<TEntry> _displayEntries = [];
    private readonly StraumrTheme _theme;
    private readonly string _screenTitle;
    private readonly string _emptyStateText;
    private readonly string _itemTypeNamePlural;

    private FrameView? _frameView;
    private SelectionListView? _listView;
    private Label? _filterLabel;
    private InteractiveTextField? _filterField;
    private View? _commandContainer;
    private Label? _commandLabel;
    private InteractiveTextField? _commandField;
    private bool _commandActive;
    private Label? _emptyLabel;
    private Label? _summaryLabel;
    private readonly StatusNotificationBar _statusBar;
    private string _currentFilter = string.Empty;
    private bool _commandsConfigured;
    private TEntry? _pendingSelection;
    private bool _hasPendingSelection;

    protected abstract string ModelHintsText { get; }

    private string InternalHintsText => $"j/k Navigate  g/G Jump  {ModelHintsText}  i Inspect  / Filter  : Command";

    protected ModelScreen(
        StraumrTheme theme,
        string screenTitle,
        string emptyStateText,
        string itemTypeNamePlural)
    {
        _theme = theme;
        _screenTitle = screenTitle;
        _emptyStateText = emptyStateText;
        _itemTypeNamePlural = itemTypeNamePlural;

        Add(new Banner { Theme = _theme });
        Add(new HintsBar { Text = InternalHintsText });
        AddView(BuildModelFrame());
        AddView(BuildCommandBar());

        _statusBar = Add(new StatusNotificationBar());
    }

    protected TEntry? SelectedEntry => GetSelectedEntry();

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        EnsureCommandsConfigured();

        IReadOnlyList<TEntry> entries = await LoadEntriesAsync(cancellationToken);
        _sourceEntries.Clear();
        _sourceEntries.AddRange(entries);

        ApplyFilter(_currentFilter);
        OnInitialized();
    }

    public override bool OnKeyDown(Key key)
    {
        if (_commandActive)
        {
            return false;
        }

        if (_sourceEntries.Count == 0
            && key is { IsCtrl: false, IsAlt: false }
            && KeyHelpers.GetCharValue(key) == ':')
        {
            ShowCommandField();
            return true;
        }

        return false;
    }

    protected abstract Task<IReadOnlyList<TEntry>> LoadEntriesAsync(CancellationToken cancellationToken);
    protected abstract string GetDisplayText(TEntry entry);
    private string GetFilterText(TEntry entry) => MarkupText.ToPlain(GetDisplayText(entry));

    protected virtual void OnInitialized() { }
    protected virtual IEnumerable<ModelCommand> GetCommands() => [];

    protected virtual bool HandleModelKeyDown(Key key, TEntry? selectedEntry) => false;
    protected virtual bool IsSameEntry(TEntry? left, TEntry? right) => EqualityComparer<TEntry>.Default.Equals(left, right);

    protected virtual void OpenSelectedEntry() { }
    protected virtual void InspectSelectedEntry() { }

    protected void ShowSuccess(string text) => _statusBar.ShowStatus(text,
        ColorResolver.Resolve(_theme.Success), ColorResolver.Resolve(_theme.Surface));

    protected void ShowInfo(string text) => _statusBar.ShowStatus(text,
        ColorResolver.Resolve(_theme.Info), ColorResolver.Resolve(_theme.Surface));

    protected void ShowDanger(string text) => _statusBar.ShowStatus(text,
        ColorResolver.Resolve(_theme.Danger), ColorResolver.Resolve(_theme.Surface));

    protected async Task RefreshAsync(bool notifyInitialized = false)
    {
        _pendingSelection = GetSelectedEntry();
        _hasPendingSelection = true;
        IReadOnlyList<TEntry> entries = await LoadEntriesAsync(CancellationToken.None);
        _sourceEntries.Clear();
        _sourceEntries.AddRange(entries);
        ApplyFilter(_currentFilter);
        if (notifyInitialized)
        {
            OnInitialized();
        }
    }

    protected void UpdateTitle(string title) => _frameView!.Title = title;

    protected sealed record ModelCommand(
        string Name,
        Action<string[]> Handler,
        params string[] Aliases);

    private FrameView BuildModelFrame()
    {
        FrameView frame = CreateFrame();
        _frameView = frame;
        _filterLabel = CreateFilterLabel();
        _filterField = CreateFilterField(_filterLabel);
        _listView = CreateListView();
        _emptyLabel = CreateEmptyLabel();
        _summaryLabel = CreateSummaryLabel();

        frame.Add(_filterLabel, _filterField, _listView, _emptyLabel, _summaryLabel);

        ApplyFilter(_currentFilter);
        WireInitialFocus();

        return frame;
    }

    private FrameView CreateFrame()
    {
            return new FrameView
        {
            Title = _screenTitle,
            X = 2,
            Y = Banner.FigletHeight + 5,
            Width = Dim.Fill(4),
            Height = Dim.Fill(2),
        };
    }

    private static Label CreateFilterLabel()
    {
        return new Label
        {
            Text = "Filter",
            X = 1,
            Y = 1,
        };
    }

    private InteractiveTextField CreateFilterField(Label filterLabel)
    {
        InteractiveTextField field = TextFieldFactory.CreateFilterField(OnFilterChanged, OnAcceptFilter, OnExitFilter);
        field.X = Pos.Right(filterLabel) + 1;
        field.Y = filterLabel.Y;
        field.Width = Dim.Fill(3);
        ApplyFilterFieldTheme(field);
        return field;
    }

    private SelectionListView CreateListView()
    {
        SelectionListView list = new(HandleListKeyDown, ColorResolver.BuildListScheme(_theme))
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = Dim.Fill(4),
        };

        list.SetMarkupSource(_displayItems);
        list.Accepting += (_, _) => OpenSelectedEntry();
        return list;
    }

    private Label CreateEmptyLabel()
    {
        return new Label
        {
            Text = _emptyStateText,
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Visible = false,
        };
    }

    private static Label CreateSummaryLabel()
    {
        return new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(2),
            TextAlignment = Alignment.End,
        };
    }

    private void WireInitialFocus()
    {
        _listView?.Initialized += (_, _) =>
        {
            if (_displayItems.Count > 0)
            {
                _listView.SetFocus();
            }
            else if (_sourceEntries.Count > 0)
            {
                _filterField?.SetFocus();
            }
        };
    }

    private View BuildCommandBar()
    {
        _commandContainer = new View
        {
            X = 2,
            Y = Banner.FigletHeight + 1,
            Width = Dim.Fill(4),
            Height = 4,
            Visible = false,
            CanFocus = true,
        };

        _commandLabel = new Label
        {
            Text = ":",
            X = 1,
            Y = 2,
            Visible = false,
        };

        _commandField = new InteractiveTextField
        {
            X = Pos.Right(_commandLabel) + 1,
            Y = 1,
            Width = Dim.Fill(2),
            BorderStyle = LineStyle.Single,
            Visible = false,
        };
        _commandField.CanFocus = true;
        _commandField.Enabled = true;
        ApplyCommandFieldTheme(_commandField);

        _commandField.Bind(Key.Enter, (f, _) =>
        {
            string command = f.Text;
            f.Text = string.Empty;
            HideCommandField();
            if (!string.IsNullOrWhiteSpace(command))
            {
                ExecuteCommand(command);
            }
            return true;
        });

        _commandField.Bind(Key.Esc, (f, _) =>
        {
            f.Text = string.Empty;
            HideCommandField();
            return true;
        }, clearText: true);

        _commandContainer.Add(_commandLabel, _commandField);
        return _commandContainer;
    }

    private void OnFilterChanged(string text) => ApplyFilter(text);

    private void OnAcceptFilter() => FocusList();

    private void OnExitFilter() => FocusList();

    private void ApplyFilter(string filter)
    {
        _currentFilter = filter;
        _displayItems.Clear();
        _displayEntries.Clear();

        foreach (TEntry entry in _sourceEntries)
        {
            string displayText = GetDisplayText(entry);
            if (MatchesFilter(GetFilterText(entry), filter))
            {
                _displayItems.Add(new MarkupLabel
                {
                    Markup = displayText,
                    Theme = _theme,
                });
                _displayEntries.Add(entry);
            }
        }

        bool hasItems = _displayItems.Count > 0;
        bool sourceHasItems = _sourceEntries.Count > 0;

        if (_filterLabel is not null)
        {
            _filterLabel.Visible = sourceHasItems;
        }

        if (_filterField is not null)
        {
            _filterField.Visible = sourceHasItems;
            _filterField.CanFocus = sourceHasItems;
        }

        if (_listView is not null)
        {
            _listView.Visible = hasItems;
            _listView.SelectedItem = hasItems ? GetSelectedIndex() : null;
        }

        if (_emptyLabel is not null)
        {
            _emptyLabel.Visible = !hasItems;
            _emptyLabel.Text = string.IsNullOrWhiteSpace(filter) ? _emptyStateText : "No matches";
        }

        UpdateSummary();
    }

    private int GetSelectedIndex()
    {
        if (_displayEntries.Count == 0)
        {
            _hasPendingSelection = false;
            _pendingSelection = default;
            return 0;
        }

        if (_hasPendingSelection)
        {
            for (int index = 0; index < _displayEntries.Count; index++)
            {
                if (IsSameEntry(_displayEntries[index], _pendingSelection))
                {
                    _hasPendingSelection = false;
                    _pendingSelection = default;
                    return index * MarkupLabelListDataSource.RowsPerItem;
                }
            }
        }

        _hasPendingSelection = false;
        _pendingSelection = default;
        return 0;
    }

    private static bool MatchesFilter(string value, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateSummary()
    {
        if (_summaryLabel is null)
        {
            return;
        }

        string scope = string.IsNullOrWhiteSpace(_currentFilter)
            ? _itemTypeNamePlural
            : $"{_itemTypeNamePlural} matching \"{_currentFilter}\"";

        _summaryLabel.Text = $"{_displayItems.Count}/{_sourceEntries.Count} {scope}";
    }

    private bool HandleListKeyDown(Key key)
    {
        if (_commandActive || _listView is null)
        {
            return false;
        }

        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 'j':
                    MoveSelection(1);
                    return true;
                case 'k':
                    MoveSelection(-1);
                    return true;
                case 'g':
                    MoveSelectionToStart();
                    return true;
                case 'G':
                    MoveSelectionToEnd();
                    return true;
                case 'i':
                    InspectSelectedEntry();
                    return true;
                case '/':
                    FocusFilter();
                    return true;
                case ':':
                    ShowCommandField();
                    return true;
                case 'o':
                    OpenSelectedEntry();
                    return true;
            }
        }

        return HandleModelKeyDown(key, GetSelectedEntry());
    }

    private TEntry? GetSelectedEntry()
    {
        if (_listView?.SelectedItem is null || _displayEntries.Count == 0)
        {
            return default;
        }

        int index = _listView.SelectedItem.Value / MarkupLabelListDataSource.RowsPerItem;
        if (index < 0 || index >= _displayEntries.Count)
        {
            return default;
        }

        return _displayEntries[index];
    }

    private void MoveSelection(int delta)
    {
        if (_listView is null || _displayItems.Count == 0)
        {
            return;
        }

        int currentLogical = (_listView.SelectedItem ?? 0) / MarkupLabelListDataSource.RowsPerItem;
        int nextLogical = Math.Clamp(currentLogical + delta, 0, _displayItems.Count - 1);
        _listView.SelectedItem = nextLogical * MarkupLabelListDataSource.RowsPerItem;
    }

    private void MoveSelectionToStart()
    {
        if (_listView is null || _displayItems.Count == 0)
        {
            return;
        }

        _listView.SelectedItem = 0;
    }

    private void MoveSelectionToEnd()
    {
        if (_listView is null || _displayItems.Count == 0)
        {
            return;
        }

        _listView.SelectedItem = (_displayItems.Count - 1) * MarkupLabelListDataSource.RowsPerItem;
    }

    private void FocusFilter()
    {
        _filterField?.SetFocus();
        _filterField?.EnterEditMode();
    }

    protected void FocusList()
    {
        if (_displayItems.Count == 0)
        {
            if (_sourceEntries.Count > 0)
            {
                _filterField?.SetFocus();
            }
            return;
        }

        _listView?.SetFocus();
    }

    private void ShowCommandField()
    {
        if (_commandContainer is null || _commandField is null || _commandLabel is null)
        {
            return;
        }

        _commandActive = true;
        
        _listView?.CanFocus = false;
        _filterField?.CanFocus = false;

        _commandContainer.Visible = true;
        _commandLabel.Visible = true;
        _commandField.Visible = true;
        _commandField.Text = string.Empty;
        _commandField.SetFocus();
        _commandField.EnterEditMode();
    }

    private void HideCommandField()
    {
        if (_commandContainer is null || _commandField is null || _commandLabel is null)
        {
            return;
        }

        _commandActive = false;
        if (_listView is not null)
        {
            _listView.CanFocus = true;
        }

        if (_filterField is not null)
        {
            _filterField.CanFocus = true;
        }

        _commandContainer.Visible = false;
        _commandField.Visible = false;
        _commandLabel.Visible = false;
        FocusList();
    }

    private void ApplyCommandFieldTheme(InteractiveTextField field)
    {
        Color background = ColorResolver.Resolve(_theme.Surface);
        Color foreground = ColorResolver.Resolve(_theme.OnSurface);
        field.ApplyTheme(background, foreground);

        Color accent = ColorResolver.Resolve(_theme.Accent);
        field.SetBorderColors(accent, accent, accent);
    }

    private void ApplyFilterFieldTheme(InteractiveTextField field)
    {
        Color background = ColorResolver.Resolve(_theme.Surface);
        Color foreground = ColorResolver.Resolve(_theme.OnSurface);
        field.ApplyTheme(background, foreground);
    }

    private void EnsureCommandsConfigured()
    {
        if (_commandsConfigured)
        {
            return;
        }

        RegisterDefaultCommands();

        foreach (ModelCommand command in GetCommands())
        {
            RegisterCommand(command);
        }

        _commandsConfigured = true;
    }

    private void RegisterDefaultCommands()
    {
        RegisterCommand(new ModelCommand("q", _ => Quit(), "quit", "exit"));
        RegisterCommand(new ModelCommand("requests", _ => NavigateTo<RequestsScreen>(), "rq", "request"));
        RegisterCommand(new ModelCommand("workspaces", _ => NavigateTo<WorkspacesScreen>(), "ws", "workspace"));
    }

    private void RegisterCommand(ModelCommand command)
    {
        _commands[command.Name] = command;

        foreach (string alias in command.Aliases)
        {
            _commands[alias] = command;
        }
    }

    private void ExecuteCommand(string commandText)
    {
        EnsureCommandsConfigured();

        string[] parts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        string commandName = parts[0];
        string[] args = parts.Skip(1).ToArray();

        if (!_commands.TryGetValue(commandName, out ModelCommand? command))
        {
            ShowDanger($"Unknown command: {commandName}");
            return;
        }

        command.Handler(args);
    }

}
