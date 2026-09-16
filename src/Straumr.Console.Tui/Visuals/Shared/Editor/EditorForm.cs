using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>
/// One page of a resource editor: its fields, laid out label beside value, and the answer to
/// whether what they hold can be saved.
/// </summary>
internal sealed class EditorForm
{
    /// <summary>Blank cells kept between the widest label and the column of inputs.</summary>
    private const int LabelGap = 2;

    private readonly EditorField[] _fields;
    private readonly EditorField[] _rows;
    private readonly (EditorField Field, Visual Wrapper)[] _wrappers;

    /// <summary>The stack of one-row fields and the blank line under it, shown or hidden together.</summary>
    private readonly Visual[] _rowRegion;

    public EditorForm(string title, params EditorField[] fields)
    {
        Title = title;
        _fields = fields;
        foreach (EditorField field in _fields)
        {
            field.Changed = () =>
            {
                // A field that changed may be the discriminator deciding which other fields apply.
                Sync();
                Changed?.Invoke();
            };
        }

        // More than one field can want the leftover height as long as a discriminator keeps all but
        // one hidden — a body is a document, a form's fields, or a multipart form's parts, and never
        // two of those at once. They stack in one cell and the visible one is the one that fills it.
        EditorField[] growing = _fields.Where(field => field.GrowsToFill).ToArray();
        _rows = _fields.Where(field => !field.GrowsToFill).ToArray();

        // The label column is sized from the labels themselves rather than by a grid, because a
        // hidden field has to take no height at all and a grid row cannot be asked to disappear.
        int labelWidth = _fields.Length == 0 ? 0 : _fields.Max(field => field.Label.Length) + LabelGap;

        (EditorField Field, Visual Wrapper)[] rowWrappers =
            _rows.Select(field => (field, Row(field, labelWidth))).ToArray();
        (EditorField Field, Visual Wrapper)[] fillingWrappers =
            growing.Select(field => (field, Filling(field))).ToArray();
        _wrappers = [.. rowWrappers, .. fillingWrappers];

        var stack = new VStack(rowWrappers.Select(pair => pair.Wrapper).ToArray())
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        if (growing.Length == 0)
        {
            // Nothing on the page wants the leftover height, so the page is as tall as its fields
            // and scrolls when the terminal is shorter than they are.
            Root = ResourceScreenLayout.Scrollable(stack);
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

        Sync();
    }

    /// <summary>
    /// Applies the discriminators: shows the fields that currently apply and hides the rest.
    /// </summary>
    /// <remarks>
    /// Assigned outright rather than bound. A field's <c>Visible</c> reads the state being edited,
    /// which is a plain object and not part of the binding graph, so a function bound over it is
    /// evaluated once when the form is built and never again — the page went on saying the request
    /// sent no body after a type had been chosen for it. It is also what the pane above does with
    /// its pages, and for the related reason that focus is revoked from a visual that is invisible
    /// during the focus pass, so visibility has to be settled before anything asks for focus.
    /// </remarks>
    public void Sync()
    {
        foreach ((EditorField field, Visual wrapper) in _wrappers)
            wrapper.IsVisible = field.IsVisible;

        bool anyRow = _rows.Any(field => field.IsVisible);
        foreach (Visual visual in _rowRegion)
            visual.IsVisible = anyRow;
    }

    private static Visual Filling(EditorField field) =>
        new VStack(
                Label(field),
                field.Content)
            .Spacing(0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    /// <summary>
    /// A field's label, filled with the focus chip while that field holds focus.
    /// </summary>
    /// <remarks>
    /// The same chip a region title carries, on the developer's own reading that a field being
    /// written into is worth saying loudly. It is the one place in the app where two chips show at
    /// once — the page title on the rule above names the page, this names the field inside it — so
    /// they sit at different levels rather than competing for the same answer.
    /// The label column is already the longest label plus the gap, which is exactly what the chip's
    /// cell of fill on each side needs, so lighting a label never moves the input beside it.
    /// </remarks>
    private static TextBlock Label(EditorField field) =>
        StraumrSurfaces.FocusTitle(field.Label, field.FocusTarget.Owns);

    public string Title { get; }

    public Visual Root { get; }

    /// <summary>
    /// Whether the page applies at all given the rest of the resource. It is the field-level
    /// discriminator one level up: an auth's type decides which of its pages exist, not merely which
    /// rows on them do, and a page kept on the rule with nothing on it reads as one that failed to
    /// load. Omitted for a page that always applies, which is every page a request has.
    /// </summary>
    public Func<bool>? Visible { get; init; }

    public bool Applies => Visible?.Invoke() ?? true;

    /// <summary>Raised on every edit, so the view can mark the resource unsaved.</summary>
    public event Action? Changed;

    /// <summary>
    /// The first field a reader would fill in. Asked for rather than held, because which field is
    /// first depends on which of them currently apply.
    /// </summary>
    public Visual FocusTarget =>
        _fields.FirstOrDefault(item => item.IsVisible)?.FocusTarget ?? Root;

    /// <summary>
    /// The first problem on this page, with that field focused and marked. Hidden fields are not
    /// checked: a field that does not apply cannot be wrong, and a value left behind by a
    /// discriminator's other branch is not the reader's to fix.
    /// </summary>
    public string? Validate()
    {
        foreach (EditorField field in _fields)
        {
            field.ClearProblem();
        }

        // A page that does not apply cannot be wrong, for the reason a hidden field cannot: what it
        // holds belongs to a branch of the discriminator the reader did not take.
        if (!Applies)
        {
            return null;
        }

        foreach (EditorField field in _fields.Where(field => field.IsVisible))
        {
            if (field.Validate() is not { } problem)
                continue;

            field.ShowProblem(problem);
            field.FocusTarget.App?.Focus(field.FocusTarget);
            return problem;
        }

        return null;
    }

    /// <summary>Pushes any field that holds its value until asked into the state being edited.</summary>
    public void Commit()
    {
        foreach (EditorField field in _fields)
            field.Commit();
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
