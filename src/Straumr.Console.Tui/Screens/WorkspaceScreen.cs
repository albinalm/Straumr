using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Straumr.Console.Shared.Console;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Factories;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens;

public sealed class WorkspaceScreen : Screen
{
    private const string HintsText = "j/k Navigate  g/G Jump  s Set active  / Filter  Enter Open  q Quit  Esc Quit";

    private readonly IInteractiveConsole _interactiveConsole;
    private readonly IStraumrWorkspaceService _workspaceService;
    private readonly IStraumrOptionsService _optionsService;
    private readonly StraumrTheme _theme;
    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<WorkspaceItem> _sourceItems = [];
    private readonly List<WorkspaceItem> _displayEntries = [];

    private SelectionListView? _listView;
    private InteractiveTextField? _filterField;
    private View? _commandContainer;
    private Label? _commandLabel;
    private InteractiveTextField? _commandField;
    private bool _commandActive;
    private Label? _emptyLabel;
    private Label? _summaryLabel;
    private readonly StatusNotificationBar _statusBar;
    private string _currentFilter = string.Empty;

    public WorkspaceScreen(
        IInteractiveConsole interactiveConsole,
        IStraumrWorkspaceService workspaceService,
        IStraumrOptionsService optionsService,
        StraumrTheme theme)
    {
        _interactiveConsole = interactiveConsole;
        _workspaceService = workspaceService;
        _optionsService = optionsService;
        _theme = theme;

        Add(new Banner { Theme = _theme });
        Add(new HintsBar { Text = HintsText });
        AddView(BuildWorkspaceFrame());
        AddView(BuildCommandBar());

        _statusBar = Add(new StatusNotificationBar());
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkspaceItem> lines = await LoadWorkspaceItemsAsync(cancellationToken);
        _sourceItems.Clear();
        foreach (WorkspaceItem line in lines)
        {
            _sourceItems.Add(line);
        }

        ApplyFilter(_currentFilter);
        int workspaceCount = _optionsService.Options.Workspaces.Count;
        ShowSuccess($" {workspaceCount} workspace{(workspaceCount == 1 ? string.Empty : "s")} loaded");
        
    }
    
    private void ShowSuccess(string text) =>  _statusBar.ShowStatus(text,
        ColorResolver.Resolve(_theme.Success), ColorResolver.Resolve(_theme.Surface));
    
    private void ShowInfo(string text) =>  _statusBar.ShowStatus(text,
        ColorResolver.Resolve(_theme.Info), ColorResolver.Resolve(_theme.Surface));
    
    private void ShowDanger(string text) =>  _statusBar.ShowStatus(text,
        ColorResolver.Resolve(_theme.Danger), ColorResolver.Resolve(_theme.Surface));
    
    public override bool OnKeyDown(Key key)
    {
        if (_commandActive)
        {
            return false;
        }

        Rune rune = key.AsRune;
        if (key == Key.Esc || (!key.IsCtrl && !key.IsAlt && (rune.Value == 'q' || rune.Value == 'Q')))
        {
            Quit();
            return true;
        }

        return false;
    }

    private FrameView BuildWorkspaceFrame()
    {
        FrameView frame = CreateFrame();
        Label filterLabel = CreateFilterLabel();
        _filterField = CreateFilterField(filterLabel);
        _listView = CreateListView();
        _emptyLabel = CreateEmptyLabel();
        _summaryLabel = CreateSummaryLabel();

        frame.Add(filterLabel, _filterField, _listView, _emptyLabel, _summaryLabel);

        ApplyFilter(_currentFilter);
        WireInitialFocus();

        return frame;
    }

    private static FrameView CreateFrame()
    {
        return new FrameView
        {
            Title = "Workspaces",
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

        list.SetSource(_displayItems);
        list.Accepting += (_, _) => EnterWorkspace();
        return list;
    }

    private static Label CreateEmptyLabel()
    {
        return new Label
        {
            Text = "No workspaces found",
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
            else
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
        }, clearText: true);

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

        foreach (WorkspaceItem workspace in _sourceItems)
        {
            if (MatchesFilter(workspace.Display, filter))
            {
                _displayItems.Add(workspace.Display);
                _displayEntries.Add(workspace);
            }
        }

        bool hasItems = _displayItems.Count > 0;

        if (_listView is not null)
        {
            _listView.Visible = hasItems;
            _listView.SelectedItem = hasItems ? 0 : null;
        }

        if (_emptyLabel is not null)
        {
            _emptyLabel.Visible = !hasItems;
            _emptyLabel.Text = string.IsNullOrWhiteSpace(filter) ? "No workspaces found" : "No matches";
        }

        UpdateSummary();
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
            ? "workspaces"
            : $"matching \"{_currentFilter}\"";

        _summaryLabel.Text = $"{_displayItems.Count}/{_sourceItems.Count} {scope}";
    }

    private bool HandleListKeyDown(Key key)
    {
        if (_commandActive)
        {
            return false;
        }

        if (_listView is null)
        {
            return false;
        }

        Rune rune = key.AsRune;

        if (key is { IsCtrl: false, IsAlt: false })
        {
            switch (rune.Value)
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
                case '/':
                    FocusFilter();
                    return true;
                case ':':
                    ShowCommandField();
                    return true;
                case 's':
                    SetCurrentWorkspace();
                    return true;
                case 'q':
                case 'Q':
                    Quit();
                    return true;
            }
        }

        if (key == Key.Enter)
        {
            EnterWorkspace();
            return true;
        }

        if (key == Key.Esc)
        {
            Quit();
            return true;
        }

        return false;
    }

    private void SetCurrentWorkspace()
    {
        WorkspaceItem? selectedItem = GetSelectedItem();
        if (selectedItem is null)
        {
            return;
        }

        if (_optionsService.Options.CurrentWorkspace != null &&
            selectedItem.Entry.Id == _optionsService.Options.CurrentWorkspace?.Id)
        {
            ShowInfo($"🤔 {selectedItem.Identifier} is already the active workspace.");
            return;
        }

        if (selectedItem.IsDamaged)
        {
            ShowDanger($"😬 {selectedItem.Identifier} is damaged and cannot be set as default workspace.");
            return;
        }

        _workspaceService.Activate(selectedItem.Entry.Id.ToString());
        ShowSuccess($"😎 {selectedItem.Identifier} is now the active workspace.");
    }

    private WorkspaceItem? GetSelectedItem()
    {
        if (_listView?.SelectedItem is null || _displayEntries.Count == 0)
        {
            return null;
        }

        int index = _listView.SelectedItem.Value;
        if (index < 0 || index >= _displayEntries.Count)
        {
            return null;
        }

        return _displayEntries[index];
    }

    private void MoveSelection(int delta)
    {
        if (_listView is null || _displayItems.Count == 0)
        {
            return;
        }

        int current = _listView.SelectedItem ?? 0;
        int next = Math.Clamp(current + delta, 0, _displayItems.Count - 1);
        _listView.SelectedItem = next;
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

        _listView.SelectedItem = _displayItems.Count - 1;
    }

    private void FocusFilter()
    {
        _filterField?.SetFocus();
        _filterField?.EnterEditMode();
    }

    private void FocusList()
    {
        if (_displayItems.Count == 0)
        {
            _filterField?.SetFocus();
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
        if (_listView is not null)
        {
            _listView.CanFocus = false;
        }
        if (_filterField is not null)
        {
            _filterField.CanFocus = false;
        }

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

    private void ExecuteCommand(string command)
    {
    }

    private void ApplyCommandFieldTheme(InteractiveTextField field)
    {
        Color background = _theme != null ? ColorResolver.Resolve(_theme.Surface) : Color.Black;
        Color foreground = _theme != null ? ColorResolver.Resolve(_theme.OnSurface) : Color.White;
        field.ApplyTheme(background, foreground);

        if (_theme is null)
        {
            return;
        }

        Color accent = ColorResolver.Resolve(_theme.Accent);
        field.SetBorderColors(accent, accent, accent);
    }

    private void EnterWorkspace()
    {
        WorkspaceItem? workspace = GetSelectedItem();
        if (workspace is null)
        {
            return;
        }

        ShowWorkspaceChildren();
    }

    private void ShowWorkspaceChildren()
    {
        string? select = _interactiveConsole.Select("Choose child", ["Test1", "Test2", "Test3"]);
    }

    private async Task<IReadOnlyList<WorkspaceItem>> LoadWorkspaceItemsAsync(CancellationToken cancellationToken)
    {
        if (_optionsService.Options.Workspaces.Count == 0)
        {
            return [];
        }

        var items = new List<WorkspaceItem>();

        foreach (StraumrWorkspaceEntry entry in _optionsService.Options.Workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceItem line = await BuildWorkspaceItemAsync(entry);
            items.Add(line);
        }

        return items
            .OrderByDescending(item => item.LastAccessed)
            .ToList();
    }

    private async Task<WorkspaceItem> BuildWorkspaceItemAsync(StraumrWorkspaceEntry entry)
    {
        var lineBuilder = new StringBuilder();
        DateTimeOffset? lastAccessed = null;
        var isDamaged = false;
        string identifier;
        if (_optionsService.Options.CurrentWorkspace != null && entry.Id == _optionsService.Options.CurrentWorkspace.Id)
        {
            lineBuilder.Append("(Current) ");
        }

        try
        {
            StraumrWorkspace workspace = await _workspaceService.PeekWorkspace(entry.Path);
            lineBuilder.Append(workspace.Name);
            lastAccessed = workspace.LastAccessed;
            identifier = workspace.Name;
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            lineBuilder.Append($"{entry.Id} [Corrupt] ");
            isDamaged = true;
            identifier = entry.Id.ToString();
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            lineBuilder.Append($"{entry.Id} [Missing] ");
            isDamaged = true;
            identifier = entry.Id.ToString();
        }

        return new WorkspaceItem(entry, lineBuilder.ToString(), identifier, isDamaged, lastAccessed);
    }

    private sealed record WorkspaceItem(
        StraumrWorkspaceEntry Entry,
        string Display,
        string? Identifier,
        bool IsDamaged,
        DateTimeOffset? LastAccessed);
}
