using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Enums;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Screens.Auth;

/// <summary>
/// What a custom auth's three extraction sources do, how each one's expression is written, and what
/// it returns. The Extract page asks for one expression whose meaning changes completely with the
/// source above it, and a placeholder has room for one example of the three.
/// </summary>
/// <remarks>
/// The text here describes <c>StraumrAuthService</c>'s three extractors and nothing else: the dotted
/// walk that is deliberately not JSONPath, the header lookup that falls through to the content
/// headers, and the first-match regex that prefers group 1. Anything claimed here that the service
/// does not do is a bug in this file. The section titles come from
/// <see cref="AuthEditingHelpers.ExtractionSourceDisplayName"/> so the help and the dropdown cannot
/// come to call the same source two things.
/// </remarks>
internal sealed class ExtractionHelpDialog
{
    private const int DialogWidth = 72;

    /// <summary>The height the three sections want when the terminal can spare it.</summary>
    private const int DialogHeight = 30;

    /// <summary>
    /// Height below which the dialog stops shrinking. Under this the chrome alone fills it and
    /// scrolling a two-row window is worse than clipping.
    /// </summary>
    private const int MinimumDialogHeight = 12;

    /// <summary>Rows left to the screen around the dialog so it never sits flush against the frame.</summary>
    private const int ViewportMargin = 2;

    /// <summary>
    /// Cells the expression column is held to, so the notes beside three different expressions line
    /// up. The widest example written here is 19 cells; one longer than this pushes its own note
    /// right rather than truncating, which is the harmless direction to fail in.
    /// </summary>
    private const int ExpressionColumn = 20;

    private readonly Dialog _dialog;

    public ExtractionHelpDialog()
    {
        var body = new VStack(
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

        // Focus starts on the text rather than on the button, so the keys that move through it work
        // without a Tab first; the button is one Tab away for a reader reaching for the pointer.
        var scroll = new ScrollableContent(body);
        scroll.AutoFocus(scroll.IsReachable);

        var closeButton = new Button("Close");
        closeButton.SetStyle(StraumrStyles.Button);

        var content = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(scroll, 0, 0)
            .Cell(new HStack(closeButton).HorizontalAlignment(Align.End), 1, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        _dialog = StraumrDialog.Create(
            new TextBlock("Extracting a value").Style(StraumrStyles.AccentText),
            content,
            DialogWidth);
        _dialog.Height(ComputeHeight);

        closeButton.Click(() => _dialog.Close());
    }

    public void Show() => _dialog.Show();

    /// <remarks>
    /// Re-clamped on every update pass rather than set once in <see cref="Show"/>, so a resize while
    /// the help is open reflows it instead of leaving it sized for the terminal it was opened in.
    /// <c>Visual.App</c> is null until the dialog is shown, which is why the unclamped height is the
    /// fallback. This is the folder browser's arrangement, for the same reason.
    /// </remarks>
    private int? ComputeHeight() =>
        _dialog.App is { } app
            ? Math.Clamp(app.Terminal.Size.Rows - ViewportMargin, MinimumDialogHeight, DialogHeight)
            : DialogHeight;

    private static Visual Section(
        ExtractionSource source,
        string prose,
        params (string Expression, string Note)[] examples)
    {
        Visual[] lines =
        [
            new TextBlock(AuthEditingHelpers.ExtractionSourceDisplayName(source))
                .Style(StraumrStyles.BrightText),
            new TextBlock(prose)
                .Style(StraumrStyles.MutedText)
                .Wrap(true)
                .HorizontalAlignment(Align.Stretch),
            .. examples.Select(Example)
        ];

        return new VStack(lines).HorizontalAlignment(Align.Stretch);
    }

    /// <remarks>
    /// The expression is painted as the fields paint a value and the note as they paint a label,
    /// because that is what the two are: one is something to type into the box above, the other is
    /// prose about it.
    /// </remarks>
    private static Visual Example((string Expression, string Note) example) =>
        new HStack(
                new TextBlock(example.Expression.PadRight(ExpressionColumn))
                    .Style(StraumrStyles.AccentText),
                new TextBlock(example.Note).Style(StraumrStyles.MutedText))
            .HorizontalAlignment(Align.Stretch);
}
