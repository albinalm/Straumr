using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ContentField : EditorField
{
    private readonly Action<ExternalContentEditModel> _edit;
    private readonly Action<string> _set;
    private readonly State<string> _text;
    private readonly ScrollableContent _view;
    private ContentFormatModel _format;

    private string? _handed;

    public ContentField(
        string label,
        string initial,
        Action<string> set,
        ContentFormatModel format,
        Action<ExternalContentEditModel> edit)
        : base(label)
    {
        _set = set;
        _edit = edit;
        _format = format;
        _text = new State<string>(initial);
        GrowsToFill = true;

        _view = new ScrollableContent(new ComputedVisual(Build));
        _view.AddCommand(EditCommand("ContentField.Edit", CommandPresentation.CommandBar));
        _view.AddCommand(EditCommand("ContentField.Edit.Control",
            CommandPresentation.None));
        _view.AddCommand(EditCommand("ContentField.Edit.Letter",
            CommandPresentation.None));

        Content = _view;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _view;

    public string Value => _text.Value;

    public void UseFormat(ContentFormatModel format) => _format = format;

    public override void Commit()
    {
        _set(Value);
        base.Commit();
    }

    public void Set(string value)
    {
        if (_text.Value == value)
        {
            return;
        }

        _text.Value = value;
        _view.ScrollOffset = 0;
    }

    private Command EditCommand(string id, CommandPresentation presentation) => new()
    {
        Id = id,
        LabelMarkup = "Edit",
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        Execute = _ => Edit()
    };

    private void Edit()
    {
        EditorDocumentModel document = _format.Prepare(Value);
        _handed = document.Text;
        _edit(new ExternalContentEditModel(document, _format.Extension, Apply));
    }

    private void Apply(string text)
    {
        if (_text.Value == text || _handed == text)
        {
            return;
        }

        _text.Value = text;
        _view.ScrollOffset = 0;
        _set(text);
        Changed?.Invoke();
    }

    private Visual Build()
    {
        if (_text.Value.Length == 0)
        {
            return new TextBlock($"No content. Press {TuiKeybindHelpers.Hint("ContentField.Edit")} to set.")
                .Style(StraumrStyleService.MutedText)
                .Wrap(true)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch);
        }

        return new VStack(_text.Value.Replace("\r", string.Empty).Split('\n')
                .Select(line => new TextBlock(line.Length == 0 ? " " : line)
                    .Style(StraumrStyleService.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);
    }
}
