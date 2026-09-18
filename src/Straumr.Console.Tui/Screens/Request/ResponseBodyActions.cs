using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Enums;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Request;

internal readonly record struct ResponseBodyOptions(
    ResponseBodyFormat Format,
    bool Highlight,
    int HighlightLimit);

internal sealed class ResponseBodyActions
{
    private readonly PreviewPane _preview;
    private readonly Action<string, bool> _notify;
    private readonly Func<ResponseBodyOptions> _options;
    private readonly bool _bounded;
    private string? _body;
    private bool _pretty;
    private bool _highlighted;
    private bool _oversized;

    public ResponseBodyActions(PreviewPane preview, Action<string, bool> notify,
        Func<ResponseBodyOptions> options, bool bounded = true)
    {
        (_preview, _notify, _options) = (preview, notify, options);
        _bounded = bounded;
        AddCommand("Format", "Beautify / minify", 'b', ToggleFormat);
        AddCommand("Highlight", "Highlight", 'h', ToggleHighlight);
        AddCommand("Copy", "Copy body", 'y', Copy);
    }

    public void SetBody(string? body)
    {
        ResponseBodyOptions options = _options();
        _body = body;
        _pretty = false;
        if (options.Format is not ResponseBodyFormat.None
            && RequestEditingHelpers.TryFormatJson(body, indented: options.Format is ResponseBodyFormat.Beautify,
                out string? formatted))
        {
            _body = formatted;
            _pretty = options.Format is ResponseBodyFormat.Beautify;
        }

        UpdatePreview();
        Highlight(options.Highlight && !_oversized);
    }

    private void UpdatePreview()
    {
        string drawn = Drawn();
        _oversized = drawn.Length > _options().HighlightLimit;
        _preview.SetPageText(0, drawn);
    }

    private string Drawn() =>
        _bounded ? ContentFormatting.Preview(_body) : string.IsNullOrEmpty(_body) ? "No body." : _body;

    private void Highlight(bool on)
    {
        _highlighted = on && RequestEditingHelpers.IsJson(_body);
        _preview.SetPageHighlighter(0, _highlighted ? Line : null);
    }

    private static StyledRun[] Line(string line) =>
        line == ContentFormatting.Truncated
            ? [new StyledRun(0, line.Length, StraumrStyles.CodeNote)]
            : JsonHighlighting.Line(line);

    private void AddCommand(string id, string label, char key, Action execute) =>
        _preview.Page(0).AddCommand(new Command
        {
            Id = $"ResponseBody.{id}", LabelMarkup = label, Gesture = new KeyGesture(key),
            Importance = CommandImportance.Primary, Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !string.IsNullOrEmpty(_body), IsVisible = _ => !string.IsNullOrEmpty(_body),
            Execute = _ => execute()
        });

    private void ToggleFormat()
    {
        if (!RequestEditingHelpers.TryFormatJson(_body, indented: !_pretty, out string? formatted))
        {
            _notify("This body is not valid JSON; formatting is unavailable.", true);
            return;
        }

        _body = formatted;
        _pretty = !_pretty;
        UpdatePreview();
        _notify(_pretty ? "JSON beautified" : "JSON minified", false);
    }

    private void ToggleHighlight()
    {
        bool wanted = !_highlighted;
        Highlight(wanted);
        if (wanted && !_highlighted)
        {
            _notify("This body is not valid JSON; highlighting is unavailable.", true);
            return;
        }

        _notify(_highlighted
            ? _oversized
                ? $"Highlighting on — {ContentFormatting.Size(Drawn().Length)} of text may scroll slowly"
                : "Highlighting on"
            : "Highlighting off", false);
    }

    private void Copy()
    {
        try
        {
            bool copied = _preview.Root.App?.Terminal.Clipboard.TrySetText(_body) == true;
            _notify(copied ? "Copied full response body" : "Clipboard is unavailable in this terminal.", !copied);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _notify($"Cannot copy body: {exception.Message}", true);
        }
    }
}
