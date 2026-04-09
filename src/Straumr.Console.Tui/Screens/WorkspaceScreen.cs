using System.Collections.ObjectModel;
using System.Text;
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
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Screens;

public sealed class WorkspaceScreen : Screen
{
    private const string HintsText = "j/k Navigate  g/G Jump  / Filter  Enter Open  q Quit  Esc Quit";

    private readonly StraumrTheme _theme;
    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<string> _sourceItems = [];

    private SelectionListView? _listView;
    private InteractiveTextField? _filterField;
    private Label? _emptyLabel;
    private Label? _summaryLabel;
    private readonly StatusNotificationBar? _statusBar;
    private string _currentFilter = string.Empty;

    public WorkspaceScreen(IStraumrWorkspaceService workspaceService, IStraumrOptionsService optionsService, StraumrTheme theme)
    {
        _theme = theme;
        _sourceItems.AddRange(LoadWorkspaceLines(optionsService, workspaceService));

        Add(new Banner { Theme = _theme });
        Add(new HintsBar { Text = HintsText });
        AddView(BuildWorkspaceFrame());

        _statusBar = Add(new StatusNotificationBar());
        ShowStatus($" {_sourceItems.Count} workspace{(_sourceItems.Count == 1 ? string.Empty : "s")} loaded");
    }

    public override bool OnKeyDown(Key key)
    {
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
            Y = Banner.FigletHeight + 2,
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
        list.Accepting += (_, _) => ActivateSelection();
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

    private void OnFilterChanged(string text) => ApplyFilter(text);
    private void OnAcceptFilter() => FocusList();
    private void OnExitFilter() => FocusList();

    private void ApplyFilter(string filter)
    {
        _currentFilter = filter;
        _displayItems.Clear();

        foreach (string workspace in _sourceItems)
        {
            if (MatchesFilter(workspace, filter))
            {
                _displayItems.Add(workspace);
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
        if (_listView is null)
        {
            return false;
        }

        Rune rune = key.AsRune;

        if (!key.IsCtrl && !key.IsAlt)
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
                case 'q':
                case 'Q':
                    Quit();
                    return true;
            }
        }

        if (key == Key.Enter)
        {
            ActivateSelection();
            return true;
        }

        if (key == Key.Esc)
        {
            Quit();
            return true;
        }

        return false;
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

    private void ActivateSelection()
    {
        if (_listView?.SelectedItem is null || _displayItems.Count == 0)
        {
            return;
        }

        int index = _listView.SelectedItem.Value;
        if (index < 0 || index >= _displayItems.Count)
        {
            return;
        }

        string workspace = _displayItems[index];
        ShowStatus($" \"{workspace.Trim()}\" selected");
    }

    private void ShowStatus(string message) => _statusBar?.ShowSuccess(message);

    private static IReadOnlyList<string> LoadWorkspaceLines(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        if (optionsService.Options.Workspaces.Count == 0)
        {
            return ["No workspaces found."];
        }

        List<WorkspaceLine> items = optionsService.Options.Workspaces
            .Select(entry => BuildWorkspaceLine(entry, optionsService, workspaceService))
            .ToList();

        return items
            .OrderByDescending(item => item.LastAccessed)
            .Select(item => item.Display)
            .ToList();
    }

    private static WorkspaceLine BuildWorkspaceLine(
        StraumrWorkspaceEntry entry,
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        var name = "Unknown";
        string status;
        DateTimeOffset? lastAccessed = null;

        try
        {
            StraumrWorkspace workspace = workspaceService.PeekWorkspace(entry.Path).GetAwaiter().GetResult();
            name = workspace.Name;
            status = "Valid";
            lastAccessed = workspace.LastAccessed;
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "Corrupt";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "Missing";
        }

        bool isCurrent = optionsService.Options.CurrentWorkspace?.Id == entry.Id;
        string idShort = entry.Id.ToString("N")[..8];
        string marker = isCurrent ? "* " : "  ";
        var display = $"{marker}{name}  [{idShort}]  {status}";

        return new WorkspaceLine(display, lastAccessed);
    }

    private sealed record WorkspaceLine(string Display, DateTimeOffset? LastAccessed);
}
