using Straumr.Console.Tui.Screens.Components.Editor;
using Straumr.Core.Configuration;
using Tomlyn;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class QuickStartView
{
    private const int Cards = 5;
    private const int ThemeCard = 2;
    private const int WorkspaceCard = 3;

    internal const string Wordmark = """
                                      ____  _
                                     / ___|| |_ _ __ __ _ _   _ _ __ ___  _ __
                                     \___ \| __| '__/ _` | | | | '_ ` _ \| '__|
                                      ___) | |_| | | (_| | |_| | | | | | | |
                                     |____/ \__|_|  \__,_|\__,_|_| |_| |_|_|
                                     """;
    private readonly Button _back;
    private readonly Padder _card;
    private readonly Button _next;
    private readonly State<string> _nextLabel = new("Next");
    private readonly State<string> _problem = new("");
    private readonly State<string> _progress = new("");

    private readonly QuickStartService _session;
    private readonly Size? _snapshotViewport;
    private readonly ThemeSelectionService _theme;
    private bool _advance;
    private ResourceList? _choices;
    private Action<int>? _choose;
    private int _chosen;
    private ScrollViewer? _contentScroll;
    private Visual? _firstField;
    private Func<Visual>? _content;
    private Func<Visual>? _contentLast;
    private Visual? _contentRoot;
    private bool _focus = true;
    private ResourceRowModel[] _rows = [];

    public QuickStartView(QuickStartService session, ThemeSelectionService theme, Size? snapshotViewport = null)
    {
        _session = session;
        _theme = theme;
        _snapshotViewport = snapshotViewport;
        Group frame = new Group().Padding(new Thickness(0)).Style(StraumrStyleService.WindowGroup)
            .HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
        Root = frame;
        _card = new Padder().Padding(new Thickness(0)).HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
        _next = new Button(() => _nextLabel.Value).Style(StraumrStyleService.PrimaryButton);
        _next.Click(() => _advance = true);
        _back = new Button("Back").Style(StraumrStyleService.Button);
        _back.Click(Back);
        ButtonRowHelpers.Hover(_back, _next);
        HStack buttons = new HStack(_back, _next).Spacing(2).HorizontalAlignment(Align.Center);
        Grid column = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(new FlexiblePane(_card), 0, 0)
            .Cell(new TextBlock(() => _problem.Value).Style(StraumrStyleService.RedText).Trimming(TextTrimming.EndEllipsis)
                .MinHeight(1).MaxHeight(1), 1, 0)
            .Cell(buttons, 2, 0)
            .Cell(new TextBlock(() => _progress.Value).Style(StraumrStyleService.BrandText)
                .HorizontalAlignment(Align.Center), 3, 0)
            .MinWidth(() => Math.Max(1, Math.Min(66, Columns - 6)))
            .MaxWidth(66)
            .MinHeight(() => Math.Max(5, Math.Min(20, Rows - 6)))
            .MaxHeight(() => Math.Max(5, Math.Min(20, Rows - 6)))
            .HorizontalAlignment(Align.Center).VerticalAlignment(Align.Center);
        HintBar hints = new HintBar().Style(StraumrStyleService.CommandBar)
            .MinHeight(() => Math.Min(Math.Max(1, Rows / 3), (100 + Math.Max(1, Columns - 4) - 1) / Math.Max(1, Columns - 4)));
        Grid shell = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto })
            .Cell(new FlexiblePane(column), 0, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 1, 0)
            .Cell(StraumrSurfaceHelpers.Inset(hints, StraumrSurfaceHelpers.RowInset), 2, 0)
            .HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
        frame.Content = shell;
        ShowCard();
    }

    public Visual Root { get; }

    public bool PreviewChanged { get; private set; }

    private int Columns => _snapshotViewport?.Width ?? TerminalViewportHelpers.Columns(_card);
    private int Rows => _snapshotViewport?.Height ?? TerminalViewportHelpers.Rows(_card);

    internal void Advance() => _advance = true;

    internal void GoBack() => Back();

    private void Scroll(int direction)
    {
        if (_contentScroll is not null)
        {
            _contentScroll.VerticalOffset += direction * Math.Max(1, _contentScroll.ViewportHeight);
        }
    }

    private static void ButtonKey(Button button, string label, Action action)
    {
        foreach (string id in (string[])["ResourceList.Activate", "ResourceList.ActivateAlternate"])
        {
            button.AddCommand(new Command
            {
                Id = "QuickStart.Activate." + label + "." + id,
                LabelMarkup = label,
                Gesture = TuiKeybindHelpers.Get(id),
                Importance = CommandImportance.Primary,
                Presentation = id == "ResourceList.Activate" ? CommandPresentation.CommandBar : CommandPresentation.None,
                Execute = _ => action()
            });
        }
    }

    private void AddColumnKeys()
    {
        foreach ((string id, int step) in ((string, int)[])
                 [("ResourceList.Next", 1), ("ResourceList.Down", 1), ("ResourceList.Previous", -1), ("ResourceList.Up", -1)])
        {
            Root.AddCommand(new Command
            {
                Id = "QuickStart.Column." + id,
                LabelMarkup = step > 0 ? "Next" : "Prev",
                Gesture = TuiKeybindHelpers.Get(id),
                Presentation = CommandPresentation.None,
                CanExecute = _ => Column(step) is not null,
                ConsumesGestureWhenUnavailable = false,
                Execute = _ => Column(step)?.Invoke()
            });
        }

        foreach ((string id, int step) in ((string, int)[])[("ResourceList.Next", 1), ("ResourceList.Previous", -1)])
        {
            Root.AddCommand(new Command
            {
                Id = "QuickStart.Column.Hint." + id,
                LabelMarkup = step > 0 ? "Next" : "Prev",
                Gesture = TuiKeybindHelpers.Get(id),
                Importance = CommandImportance.Primary,
                Presentation = CommandPresentation.CommandBar,
                RouteGesture = false,
                IsVisible = _ => (_next.Owns() || _back.Owns()) && Column(step) is not null,
                Execute = _ => { }
            });
        }
    }

    private Action? Column(int step)
    {
        Button first = _back.IsVisible ? _back : _next;
        if (step > 0)
        {
            return _choices is { } list && list.Owns() && list.SelectedIndex >= list.Count - 1 ? () => first.App?.Focus(first)
                : _back.Owns() && _next.IsVisible ? () => _next.App?.Focus(_next)
                : null;
        }

        if (_next.Owns() && _back.IsVisible)
        {
            return () => _back.App?.Focus(_back);
        }

        if (!_next.Owns() && !_back.Owns())
        {
            return null;
        }

        if (_choices is { Count: > 0 } choices)
        {
            return () =>
            {
                choices.SelectedIndex = choices.Count - 1;
                choices.App?.Focus(choices);
            };
        }

        return _contentLast?.Invoke() is { } last ? () => last.App?.Focus(last) : null;
    }

    private void LeaveContent(int step)
    {
        if (step > 0)
        {
            Button first = _back.IsVisible ? _back : _next;
            first.App?.Focus(first);
        }
    }

    private void AddFieldKeys()
    {
        foreach ((string id, string label, int step) in ((string, string, int)[])
                 [("Straumr.NextField", "Next field", 1), ("Straumr.PreviousField", "Previous field", -1)])
        {
            Root.AddCommand(new Command
            {
                Id = "QuickStart." + id,
                LabelMarkup = label,
                Gesture = TuiKeybindHelpers.Get(id),
                Importance = CommandImportance.Secondary,
                Presentation = CommandPresentation.CommandBar,
                IsVisible = _ => Stops().Count > 1,
                CanExecute = _ => Stops().Count > 1,
                Execute = _ => FocusStop(step)
            });
        }
    }

    private List<(Func<bool> Owns, Visual Target)> Stops()
    {
        List<(Func<bool> Owns, Visual Target)> stops = [];
        if ((_choices ?? _contentRoot) is { } owner && (_choices ?? _content?.Invoke()) is { } target)
        {
            stops.Add((owner.Owns, target));
        }

        if (_back.IsVisible)
        {
            stops.Add((_back.Owns, _back));
        }

        stops.Add((_next.Owns, _next));
        return stops;
    }

    private void FocusStop(int step)
    {
        List<(Func<bool> Owns, Visual Target)> stops = Stops();
        int index = stops.FindIndex(stop => stop.Owns());
        Visual target = stops[index < 0 ? 0 : (index + step + stops.Count) % stops.Count].Target;
        target.App?.Focus(target);
    }

    private void AddKey(string id, string label, Action action, Func<bool>? visible = null) =>
        Root.AddCommand(new Command
        {
            Id = "QuickStart." + id,
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get(id),
            Presentation = CommandPresentation.CommandBar,
            Importance = CommandImportance.Primary,
            IsVisible = _ => visible?.Invoke() ?? true,
            CanExecute = _ => visible?.Invoke() ?? true,
            Execute = _ => action()
        });

    private void Back()
    {
        if (_session.CreatingWorkspace && _session.Workspaces.Count > 0)
        {
            _session.CreatingWorkspace = false;
            _problem.Value = "";
            ShowCard();
            return;
        }
        if (_session.Card == 0)
        {
            return;
        }

        _session.Card--;
        ShowCard();
    }

    private void ShowCard()
    {
        _choices = null;
        _choose = null;
        _firstField = null;
        _content = null;
        _contentLast = null;
        _contentRoot = null;
        _rows = [];
        _problem.Value = _session.Problem ?? "";
        _back.IsVisible = _session.Card > 0 || _session.CreatingWorkspace && _session.Workspaces.Count > 0;
        _nextLabel.Value = _session.Card == Cards - 1 ? "Start" : "Next";
        foreach (Button button in (Button[])[_next, _back])
        {
            foreach (Command command in button.Commands.Where(command => command.Id.StartsWith("QuickStart.", StringComparison.Ordinal)).ToArray())
            {
                button.RemoveCommand(command.Id);
            }
        }

        ButtonKey(_next, _nextLabel.Value, () => _advance = true);
        ButtonKey(_back, "Back", Back);
        _progress.Value = string.Join(' ', Enumerable.Range(0, Cards).Select(i => i <= _session.Card ? "●" : "○"));
        Visual content = _session.Card switch
        {
            0 => Welcome(),
            1 => Presets(),
            ThemeCard => Themes(),
            WorkspaceCard => Workspace(),
            _ => Map()
        };
        _contentScroll = new ScrollViewer(content, false)
        {
            HorizontalScrollEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _contentScroll.SetStyle(StraumrStyleService.ListScrollViewer);
        _card.Content = _contentScroll;
        foreach (Command command in Root.Commands.ToArray())
        {
            Root.RemoveCommand(command.Id);
        }

        AddKey("ScrollableContent.PageDown", "Scroll down", () => Scroll(1), () => Rows < 24 && _session.Card > 0);
        AddKey("ScrollableContent.PageUp", "Scroll up", () => Scroll(-1), () => Rows < 24 && _session.Card > 0);
        AddFieldKeys();
        Root.BlockFieldTraversal();
        AddColumnKeys();

        _focus = true;
    }

    private Visual Welcome() => new VStack(
            new VStack(Wordmark.Split('\n').Select(line => (Visual)new TextBlock(line.TrimEnd())
                    .Style(StraumrStyleService.BrandText)).ToArray())
                .HorizontalAlignment(Align.Center).IsVisible(() => Columns >= 56 && Rows >= 23),
            new TextBlock("Straumr").Style(StraumrStyleService.BrandText).HorizontalAlignment(Align.Center)
                .IsVisible(() => Columns < 56 || Rows < 23),
            new TextBlock("Quick start guide").Style(StraumrStyleService.PrimaryText).Wrap(true)
                .HorizontalAlignment(Align.Center))
        .Spacing(1).HorizontalAlignment(Align.Center).VerticalAlignment(Align.Center);

    private Visual Presets()
    {
        string[] names = StraumrKeybindPresets.Names.ToArray();
        return Card("Keybind preset", $"{TuiKeybindHelpers.Hint("ResourceList.Activate")} chooses, and the keys change under you.",
            Choices(names.Select(name => new ResourceRowModel(Title(name), Keys(name), true)),
                Math.Max(0, Array.IndexOf(names, _session.Preset)), i => _session.PreviewPreset(names[i])));
    }

    private static string Title(string name) => name switch
    {
        "vim" => "Vim · the default",
        "commander" => "Commander · function keys",
        "client" => "Client · Postman and friends",
        _ => char.ToUpperInvariant(name[0]) + name[1..]
    };

    private static string Keys(string preset)
    {
        string move = $"Move {StraumrKeybindPresets.Hint(preset, "ResourceList.Previous")} " +
                      $"{StraumrKeybindPresets.Hint(preset, "ResourceList.Next")}";
        return $"{move}   Filter {StraumrKeybindPresets.Hint(preset, "ResourceFilter.Open")}   " +
               $"Delete {StraumrKeybindPresets.Hint(preset, "Request.Delete")}";
    }

    private Visual Themes() => Card("Theme", $"{TuiKeybindHelpers.Hint("ResourceList.Activate")} chooses, and the screen changes with it.",
        Choices(_session.Themes.Select(t => new ResourceRowModel(t.Label, t.Problem ?? Origin(t.Label),
                t.Problem is null, IsBroken: t.Problem is not null)),
            Math.Max(0, _session.Themes.FindIndex(t => t.Reference == _session.ThemeReference)), Preview));

    private static string Origin(string label) =>
        label.EndsWith("(installed)", StringComparison.Ordinal) ? "from your themes folder" : "built in";

    private void Preview(int index)
    {
        QuickStartThemeModel choice = _session.Themes[index];
        _session.Problem = choice.Problem;
        _problem.Value = choice.Problem ?? "";
        if (choice.Theme is null || choice.Reference == _session.ThemeReference)
        {
            return;
        }

        _session.ThemeReference = choice.Reference;
        PreviewChanged = _theme.Preview(choice.Theme);
    }

    private Visual Workspace()
    {
        if (!_session.CreatingWorkspace && _session.Workspaces.Count > 0)
        {
            return Card("Workspace", "Pick your active workspace.",
                Choices(_session.Workspaces
                        .Select(w => new ResourceRowModel(w.Name,
                            $"{CountFormatting.Label(w.Requests, "request")} · {CountFormatting.Label(w.Auths, "auth")}",
                            w.Requests > 0 || w.Auths > 0, w.Directory))
                        .Append(new ResourceRowModel("Create a new workspace", "start an empty one", true)),
                    _session.WorkspaceIndex, Pick));
        }

        var name = new TextField("Name", _session.WorkspaceName, value => _session.WorkspaceName = value, "workspace name");
        var location = new TextField("Location", _session.WorkspaceLocation, value => _session.WorkspaceLocation = value,
            "workspace directory");
        var browse = new ActionField("Browse", "choose a folder", "Browse", () =>
            new FolderBrowserDialog(_session.WorkspaceLocation, _session.WorkspaceLocation, location.Set).Show());
        var form = new EditorForm("Workspace", name, location, browse) { Leave = LeaveContent };
        _firstField = form.FocusTarget;
        _content = () => form.FocusTarget;
        _contentLast = () => form.LastTarget;
        _contentRoot = form.Root;
        return Card("Create a workspace", _session.Workspaces.Count == 0
            ? "Create your first workspace." : "Create another workspace.", form.Root.MinHeight(5));
    }

    private void Pick(int index)
    {
        if (index < _session.Workspaces.Count)
        {
            _session.WorkspaceIndex = index;
        }
        else
        {
            _session.CreatingWorkspace = true;
        }
    }

    private Visual Choices(IEnumerable<ResourceRowModel> rows, int chosen, Action<int> select)
    {
        _rows = rows.ToArray();
        _chosen = Math.Clamp(chosen, 0, Math.Max(0, _rows.Length - 1));
        _choose = select;
        _choices = new ResourceList(Marked(), activateLabel: "Choose") { SelectsOnHover = true };
        _choices.SelectedIndex = _chosen;
        _choices.ItemActivated += Choose;
        int height = Height(_rows, _session.Card == WorkspaceCard ? 3 : 4);
        return ResourceScreenLayoutHelpers.Scrollable(_choices).MinHeight(height).MaxHeight(height);
    }

    private IEnumerable<ResourceRowModel> Marked() =>
        _rows.Select((row, index) => row with { IsCurrent = index == _chosen });

    private static int Height(IReadOnlyList<ResourceRowModel> rows, int visible)
    {
        int line = rows.Any(row => row.Detail is not null) ? 3 : rows.Any(row => row.Meta is not null) ? 2 : 1;
        int spacing = line == 1 ? 0 : 1;
        int items = Math.Clamp(rows.Count, 1, visible);
        return items * (line + spacing) - spacing + 1;
    }

    private void Choose(int index)
    {
        if (_choices is null || _rows.Length == 0)
        {
            return;
        }

        int previousKeybindVersion = StraumrKeybinds.Version;
        _choose?.Invoke(Math.Clamp(index, 0, _rows.Length - 1));
        PreviewChanged |= previousKeybindVersion != StraumrKeybinds.Version;
        ShowCard();
    }

    private static Visual Card(string title, string subtitle, Visual content) => new VStack(
            new VStack([
                new TextBlock(title).Style(StraumrStyleService.BrandText).HorizontalAlignment(Align.Center),
                .. subtitle.Length == 0 ? (Visual[])[] :
                [
                    new TextBlock(subtitle).Style(StraumrStyleService.MutedText)
                        .Wrap(true).HorizontalAlignment(Align.Center)
                ]
            ]).HorizontalAlignment(Align.Stretch),
            content)
        .Spacing(1).HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Center);

    private Visual Map() => Card("You are set up", "", new VStack(
            new VStack(
                    Row(PromptCommand("ws"), "Workspaces", "browse and edit your workspace collection", false),
                    Row(PromptCommand("rq"), "Requests", "← you start here", true, true),
                    Row(PromptCommand("au"), "Auths", "fetched before a request and applied to it", false),
                    Row(PromptCommand("vr"), "Variables", "variables contained within the workspace", false),
                    Row(PromptCommand("sc"), "Secrets", "secret values, stored outside the workspace", false))
                .HorizontalAlignment(Align.Center),
            new TextBlock("Tip: You can also use ☰ to navigate between screens.")
                .Style(StraumrStyleService.MutedBrightText).HorizontalAlignment(Align.Center))
        .Spacing(1).HorizontalAlignment(Align.Stretch));

    private static string PromptCommand(string alias)
    {
        string key = TuiKeybindHelpers.Hint("Straumr.OpenCommandPrompt");
        return key == ":" ? $":{alias}" : $"{key} {alias}";
    }

    private Visual Row(string alias, string screen, string phrase, bool here, bool always = false) => new HStack(
            new TextBlock(alias).Style(StraumrStyleService.AccentText).MinWidth(4),
            new TextBlock(screen).Style(here ? StraumrStyleService.BrightText : StraumrStyleService.PrimaryText).MinWidth(12),
            new TextBlock(phrase).Style(here ? StraumrStyleService.BrightText : StraumrStyleService.MutedBrightText)
                .IsVisible(() => always || Columns >= 68))
        .Spacing(1);

    public async Task UpdateAsync(CancellationToken token)
    {
        if (Root.App?.Root.EnumerateVisualsDepthFirst().Any(v => v is IModalVisual { IsModal: true }) is true)
        {
            return;
        }

        if (_focus && _next.App is { } app)
        {
            Visual target = _choices ?? _firstField ?? _next;
            if (target.App != app)
            {
                return;
            }

            _focus = false;
            app.Focus(target);
        }
        if (!_advance)
        {
            return;
        }

        _advance = false;
        try
        {
            if (_session.Card == Cards - 1)
            {
                await _session.CompleteAsync(token);
                _theme.Apply();
            }
            else
            {
                if (_session.Card == ThemeCard && _session.Themes[_chosen].Problem is { } problem)
                {
                    throw new InvalidOperationException(problem);
                }

                if (_session.Card == WorkspaceCard)
                {
                    await _session.ActivateWorkspaceAsync(token);
                }

                _session.Card++;
                _session.Problem = null;
                ShowCard();
            }
        }
        catch (Exception exception) when (RequestScreen.IsRecoverable(exception) || exception is InvalidOperationException or TomlException)
        {
            _problem.Value = _session.Problem = exception.Message.Split('\n')[0].Trim();
        }
    }
}
