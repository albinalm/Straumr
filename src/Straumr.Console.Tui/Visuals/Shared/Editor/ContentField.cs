using Straumr.Console.Shared.Helpers;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>
/// A document rather than a line: a request body, and later a custom auth's. The field shows what
/// the document holds and gives the writing of it to the reader's own editor.
/// </summary>
/// <remarks>
/// That handover is the point. A body is written in JSON, XML or nothing in particular, and the
/// editor the reader already has knows those languages, indents them, closes their brackets and
/// quotes, and is configured the way they configured it. An editor built into this form would be a
/// worse one of those, so the form shows the body and <c>$EDITOR</c> writes it, exactly as
/// <c>Ctrl+E</c> on the list already opens a whole request.
/// A terminal has one screen and one keyboard, so the app's loop has to stop while another program
/// holds them. The field therefore asks rather than launches: the request travels out to the screen,
/// which owns the suspend, the run and the resume that every external edit in this app goes through.
/// </remarks>
internal sealed class ContentField : EditorField
{
    /// <summary>The C0 control character a terminal sends for <c>Ctrl</c> plus a letter.</summary>
    private const char EditControlChar = (char)('E' & 0x1F);

    private readonly State<string> _text;
    private readonly ScrollableContent _view;
    private readonly Action<string> _set;
    private readonly Action<ExternalContentEdit> _edit;
    private ContentFormat _format;

    /// <summary>
    /// The document last handed to the editor. What comes back equal to it was not written by
    /// anyone, which matters because what goes out is not always what the field is holding.
    /// </summary>
    private string? _handed;

    /// <param name="format">
    /// How a document of the current content type is handed over. It follows the content type, so
    /// it can change while the form is open.
    /// </param>
    /// <param name="edit">
    /// Where a request to open the editor is sent. Launching a process is not something a field can
    /// do from inside a keystroke; the screen has to put the terminal down first.
    /// </param>
    public ContentField(
        string label,
        string initial,
        Action<string> set,
        ContentFormat format,
        Action<ExternalContentEdit> edit)
        : base(label)
    {
        _set = set;
        _edit = edit;
        _format = format;
        _text = new State<string>(initial);
        GrowsToFill = true;

        // The same rendering the request and response previews use, for the same reason: a body is
        // lines of text that have to be read and scrolled, and the list of them is already a shared
        // component that scrolls with the keys the rest of the app scrolls with.
        _view = new ScrollableContent(new ComputedVisual(Build));
        _view.AddCommand(EditCommand("ContentField.Edit", new KeyGesture(TerminalKey.Enter),
            CommandPresentation.CommandBar));
        // Beside Enter because it is the key that opens an editor everywhere else in this app. A
        // terminal sends Ctrl and a letter as the single C0 byte the letter maps to, so the gesture
        // carries that control character; the letter-and-modifier form is registered beside it,
        // unpresented, for a host that reports the two separately.
        _view.AddCommand(EditCommand("ContentField.Edit.Control",
            new KeyGesture(EditControlChar, TerminalModifiers.Ctrl), CommandPresentation.None));
        _view.AddCommand(EditCommand("ContentField.Edit.Letter",
            new KeyGesture('e', TerminalModifiers.Ctrl), CommandPresentation.None));

        Content = _view;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _view;

    public string Value => _text.Value;

    /// <summary>
    /// Follows the content type for a field whose type is itself editable. A format left behind
    /// after the type changed would open the next edit in the wrong language.
    /// </summary>
    public void UseFormat(ContentFormat format) => _format = format;

    public override void Commit()
    {
        _set(Value);
        base.Commit();
    }

    /// <summary>Writes the field from outside an edit, for the content another field decides.</summary>
    public void Set(string value)
    {
        if (_text.Value == value)
            return;
        _text.Value = value;
        _view.ScrollOffset = 0;
    }

    private Command EditCommand(string id, KeyGesture gesture, CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = "Edit",
        Gesture = gesture,
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        Execute = _ => Edit()
    };

    private void Edit()
    {
        EditorDocument document = _format.Prepare(Value);
        _handed = document.Text;
        _edit(new ExternalContentEdit(document, _format.Extension, Apply));
    }

    /// <remarks>
    /// What the editor saved is taken as it saved it. A trailing newline most editors add is a real
    /// change to a body that will be sent byte for byte, and quietly trimming it would be this form
    /// editing a document the reader had just finished editing.
    /// </remarks>
    private void Apply(string text)
    {
        // Opening a document and closing it again is not an edit, even when what was opened was a
        // document this field supplied rather than the one it is holding.
        if (_text.Value == text || _handed == text)
            return;

        _text.Value = text;
        _view.ScrollOffset = 0;
        _set(text);
        Changed?.Invoke();
    }

    private Visual Build()
    {
        if (_text.Value.Length == 0)
        {
            return new TextBlock("No content. Press Enter to set.")
                .Style(StraumrStyles.MutedText)
                .Wrap(true)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch);
        }

        return new VStack(_text.Value.Replace("\r", string.Empty).Split('\n')
                .Select(line => new TextBlock(line.Length == 0 ? " " : line)
                    .Style(StraumrStyles.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }
}

/// <summary>
/// How a document of one content type is handed to an external editor.
/// </summary>
/// <param name="Extension">
/// The extension of the file the editor is handed, leading dot included. It is the only thing that
/// tells the editor which language it has been given.
/// </param>
/// <param name="Prepare">
/// What to open on, given what the field is holding: a document to start from when there is none
/// yet, a layout worth reading when there is, and where the caret belongs in either.
/// </param>
internal sealed record ContentFormat(string Extension, Func<string, EditorDocument> Prepare);

/// <summary>
/// A document on its way out to the reader's editor, and the way back in for what they saved.
/// </summary>
/// <param name="Extension">
/// The extension of the file the editor is handed, leading dot included.
/// </param>
/// <param name="Apply">
/// Called with what was saved, after the app has the terminal back. The field, not the screen,
/// decides what a returned document means.
/// </param>
internal sealed record ExternalContentEdit(
    EditorDocument Document, string Extension, Action<string> Apply);
