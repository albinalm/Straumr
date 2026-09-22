using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Enums;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Auth;

internal sealed class ExtractionHelpDialog
{
    private const int DialogWidth = 72;

    private const int DialogHeight = 30;

    private const int MinimumDialogHeight = 12;

    private const int ViewportMargin = 2;

    private const int ExpressionColumn = 20;

    private readonly Dialog _dialog;

    public ExtractionHelpDialog()
    {
        VStack body = new VStack(
                Section(
                    ExtractionSource.JsonPath,
                    "Walks the JSON response body one segment at a time. Segments are separated by "
                    + "dots, and a bare number picks an element out of an array. This is not "
                    + "JSONPath: there is no $, no bracketed index, and no wildcards or filters. A "
                    + "string arrives unquoted; an object or an array arrives as raw JSON.",
                    ("access_token", "{\"access_token\":\"ab12\"}  ->  ab12"),
                    ("data.token", "{\"data\":{\"token\":\"ab12\"}}  ->  ab12"),
                    ("tokens.0.value", "{\"tokens\":[{\"value\":\"ab12\"}]}  ->  ab12")),
                Section(
                    ExtractionSource.ResponseHeader,
                    "Reads one header off the response, looking at the response's own headers first "
                    + "and then the content headers. Write the name on its own, without a colon. "
                    + "Case does not matter, and a header sent more than once gives its first value.",
                    ("X-Auth-Token", "the X-Auth-Token header of the response"),
                    ("Authorization", "found whatever case it was sent in")),
                Section(
                    ExtractionSource.Regex,
                    "Matches the response body as text and takes the first match. A pattern with a "
                    + "capture group yields group 1; a pattern without one yields the whole match. "
                    + "Matching is case-sensitive, and a dot does not cross a line break.",
                    ("\"token\":\"([^\"]+)\"", "->  group 1 of the first match"),
                    ("[A-Za-z0-9._-]{40,}", "->  the whole match, there being no group")))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        var scroll = new ScrollableContent(body);
        scroll.AutoFocus(scroll.IsReachable);

        var closeButton = new Button("Close");
        closeButton.SetStyle(StraumrStyleService.Button);

        Grid content = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(scroll, 0, 0)
            .Cell(new HStack(closeButton).HorizontalAlignment(Align.End), 1, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        _dialog = StraumrDialogHelpers.Create(
            new TextBlock("Extracting a value").Style(StraumrStyleService.AccentText),
            content,
            DialogWidth);
        _dialog.Height(ComputeHeight);

        closeButton.Click(() => _dialog.Close());
    }

    public void Show() => TuiWindowHelpers.Show(_dialog);

    private int? ComputeHeight() =>
        Math.Clamp(TerminalViewportHelpers.Rows(_dialog) - ViewportMargin, MinimumDialogHeight, DialogHeight);

    private static Visual Section(
        ExtractionSource source,
        string prose,
        params (string Expression, string Note)[] examples)
    {
        Visual[] lines =
        [
            new TextBlock(AuthEditingHelpers.ExtractionSourceDisplayName(source))
                .Style(StraumrStyleService.BrightText),
            new TextBlock(prose)
                .Style(StraumrStyleService.MutedText)
                .Wrap(true)
                .HorizontalAlignment(Align.Stretch),
            .. examples.Select(Example)
        ];

        return new VStack(lines).HorizontalAlignment(Align.Stretch);
    }

    private static Visual Example((string Expression, string Note) example) =>
        new HStack(
                new TextBlock(example.Expression.PadRight(ExpressionColumn))
                    .Style(StraumrStyleService.AccentText),
                new TextBlock(example.Note).Style(StraumrStyleService.MutedText))
            .HorizontalAlignment(Align.Stretch);
}
