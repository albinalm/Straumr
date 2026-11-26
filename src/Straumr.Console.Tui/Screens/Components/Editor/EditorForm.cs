using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;
using ScrollableContent = Straumr.Console.Tui.Screens.Components.Shared.ScrollableContent;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class EditorForm
{
    private const int LabelGap = 3;

    private readonly EditorField[] _fields;

    private readonly Visual[] _rowRegion;
    private readonly EditorField[] _rows;
    private readonly (EditorField Field, Visual Wrapper)[] _wrappers;

    public EditorForm(string title, params EditorField[] fields)
    {
        Title = title;
        _fields = fields;
        foreach (EditorField field in _fields)
        {
            field.Changed = () =>
            {
                Sync();
                Changed?.Invoke();
            };
        }

        EditorField[] growing = _fields.Where(field => field.GrowsToFill).ToArray();
        _rows = _fields.Where(field => !field.GrowsToFill).ToArray();

        int labelWidth = _fields.Length == 0 ? 0 : _fields.Max(field => field.Label.Length) + LabelGap;

        (EditorField Field, Visual Wrapper)[] rowWrappers =
            _rows.Select(field => (field, Row(field, labelWidth))).ToArray();
        (EditorField Field, Visual Wrapper)[] fillingWrappers =
            growing.Select(field => (field, Filling(field))).ToArray();
        _wrappers = [.. rowWrappers, .. fillingWrappers];

        VStack stack = new VStack(rowWrappers.Select(pair => pair.Wrapper).ToArray())
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        if (growing.Length == 0)
        {
            Root = ResourceScreenLayoutHelpers.Scrollable(stack);
            _rowRegion = [];
        }
        else
        {
            var separator = new TextBlock(" ");
            _rowRegion = [stack, separator];
            Root = new Grid()
                .Columns(new ColumnDefinition { Width = GridLength.Star() })
                .Rows(
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star() })
                .Cell(stack, 0, 0)
                .Cell(separator, 1, 0)
                .Cell(new ZStack(fillingWrappers.Select(pair => pair.Wrapper).ToArray())
                    .HorizontalAlignment(Align.Stretch)
                    .VerticalAlignment(Align.Stretch), 2, 0)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch);
        }

        Root.AddCommand(MoveCommand("ResourceList.Next", "Next", 1, CommandPresentation.CommandBar));
        Root.AddCommand(MoveCommand("ResourceList.Previous", "Prev", -1, CommandPresentation.CommandBar));
        Root.AddCommand(MoveCommand("ResourceList.Down", "Next", 1, CommandPresentation.None));
        Root.AddCommand(MoveCommand("ResourceList.Up", "Prev", -1, CommandPresentation.None));
        Sync();
    }

    public string Title { get; }

    public Visual Root { get; }

    public Func<bool>? Visible { get; init; }

    public Action<int>? Leave { get; init; }

    public bool Applies => Visible?.Invoke() ?? true;

    public Visual FocusTarget =>
        Reachable.FirstOrDefault()?.FocusTarget ?? Root;

    public Visual LastTarget => Reachable.LastOrDefault()?.FocusTarget ?? Root;

    private EditorField[] Reachable =>
        _wrappers.Select(pair => pair.Field).Where(item => item.IsVisible && item.FocusTarget.Focusable).ToArray();

    public void Sync()
    {
        foreach ((EditorField field, Visual wrapper) in _wrappers)
        {
            wrapper.IsVisible = field.IsVisible;
        }

        bool anyRow = _rows.Any(field => field.IsVisible);
        foreach (Visual visual in _rowRegion)
        {
            visual.IsVisible = anyRow;
        }
    }

    private static Visual Filling(EditorField field) =>
        new VStack(
                Label(field),
                field.Content)
            .Spacing(0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private static TextBlock Label(EditorField field) =>
        StraumrSurfaceHelpers.FocusTitle(field.Label, field.FocusTarget.Owns)
            .PointerPressed((_, e) =>
            {
                if (e.Button != TerminalMouseButton.Left || !field.FocusTarget.Focusable)
                {
                    return;
                }

                field.FocusTarget.App?.Focus(field.FocusTarget);
                e.Handled = true;
            });

    public event Action? Changed;

    public string? Validate()
    {
        foreach (EditorField field in _fields)
        {
            field.ClearProblem();
        }

        if (!Applies)
        {
            return null;
        }

        foreach (EditorField field in _fields.Where(field => field.IsVisible))
        {
            if (field.Validate() is not { } problem)
            {
                continue;
            }

            field.ShowProblem(problem);
            field.FocusTarget.App?.Focus(field.FocusTarget);
            return problem;
        }

        return null;
    }

    public void Commit()
    {
        foreach (EditorField field in _fields)
        {
            field.Commit();
        }
    }

    private Command MoveCommand(string id, string label, int step, CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        CanExecute = _ => Move(step) is not null,
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => Move(step)?.Invoke()
    };

    private Action? Move(int step)
    {
        bool atEdge = Root.App?.FocusedElement switch
        {
            ResourceList list => step > 0 ? list.SelectedIndex >= list.Count - 1 : list.SelectedIndex <= 0,
            ScrollableContent content => step > 0 ? content.AtBottom : content.AtTop,
            _ => true
        };
        EditorField[] fields = Reachable;
        int index = Array.FindIndex(fields, field => field.FocusTarget.Owns());
        if (!atEdge || index < 0)
        {
            return null;
        }

        int next = index + step;
        if ((uint)next < (uint)fields.Length)
        {
            Visual focus = fields[next].FocusTarget;
            return () => focus.App?.Focus(focus);
        }

        return Leave is { } leave ? () => leave(step) : null;
    }

    private static Visual Row(EditorField field, int labelWidth) =>
        new HStack(
                Label(field)
                    .MinWidth(labelWidth)
                    .VerticalAlignment(Align.Start),
                field.Content)
            .Spacing(0)
            .HorizontalAlignment(Align.Stretch);
}
